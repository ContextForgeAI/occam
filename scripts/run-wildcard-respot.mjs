#!/usr/bin/env node
/** Re-spot FAIL_HONEST wildcard rows — live stdio MCP. */
import { spawn } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const ids = process.argv.slice(2);
if (ids.length === 0) {
  console.error("Usage: node scripts/run-wildcard-respot.mjs <id>...");
  process.exit(1);
}
const ts = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const outPath = join(root, "artifacts", "quality-audit", `wildcard-respot-${ts}.json`);
const REQUEST_TIMEOUT_MS = 180_000;

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;
  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (c) => this.#onData(c.toString()));
    proc.stderr.on("data", () => {});
  }
  #sendLine(obj) {
    this.#proc.stdin.write(`${JSON.stringify(obj)}\n`);
  }
  #onData(chunk) {
    this.#buffer += chunk;
    for (;;) {
      const nl = this.#buffer.indexOf("\n");
      if (nl === -1) break;
      const line = this.#buffer.slice(0, nl).trim();
      this.#buffer = this.#buffer.slice(nl + 1);
      if (!line) continue;
      let msg;
      try {
        msg = JSON.parse(line);
      } catch {
        continue;
      }
      if (msg.id != null && this.#pending.has(msg.id)) {
        const { resolve, reject } = this.#pending.get(msg.id);
        this.#pending.delete(msg.id);
        if (msg.error) reject(new Error(JSON.stringify(msg.error)));
        else resolve(msg.result);
      }
    }
  }
  notify(method, params = {}) {
    this.#sendLine({ jsonrpc: "2.0", method, params });
  }
  request(method, params = {}) {
    const id = this.#id++;
    this.#sendLine({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.#pending.has(id)) {
          this.#pending.delete(id);
          reject(new Error(`timeout ${method}`));
        }
      }, REQUEST_TIMEOUT_MS);
      this.#pending.set(id, {
        resolve: (v) => {
          clearTimeout(timer);
          resolve(v);
        },
        reject: (e) => {
          clearTimeout(timer);
          reject(e);
        },
      });
    });
  }
}

function parseToolJson(result) {
  if (result?.isError) {
    return { parsed: null, isError: true };
  }
  const text = result?.content?.find((c) => c.type === "text")?.text;
  try {
    return { parsed: JSON.parse(text), isError: false };
  } catch {
    return { parsed: null, isError: true };
  }
}

function loadRows() {
  const load = (p) =>
    readFileSync(join(root, p), "utf8")
      .trim()
      .split(/\n+/)
      .map((l) => JSON.parse(l));
  return [...load("corpora/quality-audit-wildcard.jsonl"), ...load("corpora/quality-audit-wildcard-digest.jsonl")];
}

function verdict(summary) {
  if (summary.transcodeOk) return "WORKS";
  const code = summary.failureCode || summary.probeCode;
  const honest = new Set([
    "http_403",
    "http_404",
    "http_401",
    "http_429",
    "timeout",
    "network_error",
    "extraction_failed",
    "workers_unavailable",
    "thin_extract",
    "captcha_or_challenge",
  ]);
  if (!code || honest.has(code)) return "FAIL_HONEST";
  return "FAIL_BUG";
}

async function main() {
  const byId = Object.fromEntries(loadRows().map((r) => [r.id, r]));
  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new McpStdioClient(proc);
  const session = { vpn: "on", startedAt: new Date().toISOString(), rows: [] };
  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "wildcard-respot", version: "1.0" },
    });
    client.notify("notifications/initialized");
    for (const id of ids) {
      const row = byId[id];
      if (!row) {
        console.error("missing id", id);
        continue;
      }
      const entry = { id, url: row.url, backend: row.backend };
      const probeRes = await client.request("tools/call", {
        name: "occam_probe",
        arguments: { url: row.url },
      });
      entry.probe = parseToolJson(probeRes).parsed;
      const txRes = await client.request("tools/call", {
        name: "occam_transcode",
        arguments: { url: row.url, backend_policy: row.backend },
      });
      entry.transcode = parseToolJson(txRes).parsed;
      const p = entry.probe;
      const t = entry.transcode;
      entry.summary = {
        probeOk: p?.ok === true,
        probeCode: p?.failureCode ?? null,
        transcodeOk: t?.ok === true,
        failureCode: t?.failure?.code ?? t?.failureCode ?? null,
        backendUsed: t?.backend ?? null,
        mdLen: typeof t?.markdown === "string" ? t.markdown.length : 0,
      };
      entry.summary.verdict = verdict(entry.summary);
      session.rows.push(entry);
      console.log(`[respot] ${id}: ${entry.summary.verdict} (${entry.summary.failureCode || "ok"})`);
    }
  } finally {
    proc.kill();
  }
  session.finishedAt = new Date().toISOString();
  session.rollup = {
    works: session.rows.filter((r) => r.summary.verdict === "WORKS").length,
    failHonest: session.rows.filter((r) => r.summary.verdict === "FAIL_HONEST").length,
    failBug: session.rows.filter((r) => r.summary.verdict === "FAIL_BUG").length,
  };
  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, JSON.stringify(session, null, 2));
  console.log("wrote", outPath);
  console.log(JSON.stringify(session.rollup));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
