#!/usr/bin/env node
/**
 * Heavy-sites benchmark for http_then_browser escalation behavior.
 *
 * Usage:
 *   node scripts/run-heavy-browser-escalation-bench.mjs
 *   node scripts/run-heavy-browser-escalation-bench.mjs --rounds=5
 *   node scripts/run-heavy-browser-escalation-bench.mjs --corpus=corpora/heavy-browser-escalation.jsonl
 */
import { spawn } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const rounds = Math.max(1, Number((process.argv.find((a) => a.startsWith("--rounds=")) || "--rounds=3").split("=")[1] || 3));
const corpusRel = (process.argv.find((a) => a.startsWith("--corpus=")) || "--corpus=corpora/heavy-browser-escalation.jsonl").split("=")[1];
const corpusPath = join(root, corpusRel);
const REQUEST_TIMEOUT_MS = 240_000;
const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const outPath = join(root, "artifacts", "heavy-browser-bench", `${stamp}.json`);

function loadJsonl(path) {
  return readFileSync(path, "utf8")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function pct(values, p) {
  if (!values.length) return 0;
  const s = [...values].sort((a, b) => a - b);
  const idx = Math.floor((s.length - 1) * p);
  return s[idx];
}

function parseToolJson(result) {
  if (result?.isError) {
    const text = result?.content?.find((c) => c.type === "text")?.text ?? "tool_error";
    return { parsed: null, isError: true, raw: text };
  }
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return { parsed: null, isError: false, raw: result };
  try {
    return { parsed: JSON.parse(text), isError: false, raw: text };
  } catch {
    return { parsed: null, isError: false, raw: text };
  }
}

function isBrowserBackend(name) {
  if (!name) return false;
  const v = String(name).toLowerCase();
  return v.includes("playwright") || v.includes("browser") || v.includes("chromium");
}

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;

  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
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
          reject(new Error(`MCP timeout ${REQUEST_TIMEOUT_MS}ms: ${method}`));
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

async function main() {
  const corpus = loadJsonl(corpusPath);
  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new McpStdioClient(proc);
  const rows = [];

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "heavy-browser-escalation-bench", version: "1.0" },
    });
    client.notify("notifications/initialized");

    for (let round = 1; round <= rounds; round += 1) {
      for (const item of corpus) {
        const backendPolicy = item.backend || "http_then_browser";
        const startedAt = Date.now();
        try {
          const result = await client.request("tools/call", {
            name: "occam_transcode",
            arguments: {
              url: item.url,
              backend_policy: backendPolicy,
            },
          });
          const elapsedMs = Date.now() - startedAt;
          const parsed = parseToolJson(result).parsed;
          const ok = parsed?.ok === true;
          const backendUsed = parsed?.backend ?? null;
          const failureCode = parsed?.failure?.code ?? parsed?.failureCode ?? null;
          rows.push({
            round,
            id: item.id,
            url: item.url,
            backendPolicy,
            elapsedMs,
            ok,
            backendUsed,
            escalatedToBrowser: isBrowserBackend(backendUsed),
            markdownLen: ok ? (parsed?.markdown?.length || 0) : 0,
            failureCode,
          });
          console.log(`[heavy-bench] r${round} ${item.id}: ok=${ok} backend=${backendUsed ?? "-"} t=${elapsedMs}ms`);
        } catch (error) {
          const elapsedMs = Date.now() - startedAt;
          rows.push({
            round,
            id: item.id,
            url: item.url,
            backendPolicy,
            elapsedMs,
            ok: false,
            backendUsed: null,
            escalatedToBrowser: false,
            markdownLen: 0,
            failureCode: String(error?.message || error),
          });
          console.log(`[heavy-bench] r${round} ${item.id}: error t=${elapsedMs}ms`);
        }
      }
    }
  } finally {
    proc.kill();
  }

  const allLatency = rows.map((r) => r.elapsedMs);
  const okRows = rows.filter((r) => r.ok);
  const okLatency = okRows.map((r) => r.elapsedMs);
  const escalatedRows = okRows.filter((r) => r.escalatedToBrowser);
  const escalatedLatency = escalatedRows.map((r) => r.elapsedMs);

  const summary = {
    generatedAt: new Date().toISOString(),
    corpus: corpusRel,
    rounds,
    totalRuns: rows.length,
    okRuns: okRows.length,
    successRatePct: rows.length ? Number(((okRows.length / rows.length) * 100).toFixed(1)) : 0,
    escalationRatePctAll: rows.length
      ? Number(((rows.filter((r) => r.escalatedToBrowser).length / rows.length) * 100).toFixed(1))
      : 0,
    escalationRatePctOk: okRows.length
      ? Number(((escalatedRows.length / okRows.length) * 100).toFixed(1))
      : 0,
    p50MsAll: pct(allLatency, 0.5),
    p95MsAll: pct(allLatency, 0.95),
    p50MsOk: pct(okLatency, 0.5),
    p95MsOk: pct(okLatency, 0.95),
    p50MsEscalatedOk: pct(escalatedLatency, 0.5),
    p95MsEscalatedOk: pct(escalatedLatency, 0.95),
    topFailures: Object.entries(
      rows
        .filter((r) => !r.ok)
        .reduce((acc, r) => {
          const key = r.failureCode || "unknown";
          acc[key] = (acc[key] || 0) + 1;
          return acc;
        }, {}),
    )
      .sort((a, b) => b[1] - a[1])
      .slice(0, 8)
      .map(([code, count]) => ({ code, count })),
  };

  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, JSON.stringify({ summary, rows }, null, 2), "utf8");

  console.log("\n=== heavy-browser-escalation summary ===");
  console.log(JSON.stringify(summary, null, 2));
  console.log(`OUT=${outPath}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
