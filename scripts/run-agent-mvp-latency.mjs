#!/usr/bin/env node
/**
 * Agent-First MVP Phase 3 — informal latency spot check via subprocess stdio MCP.
 * Usage: node scripts/run-agent-mvp-latency.mjs [--out artifacts/agent-mvp-gate/<ts>-latency.json]
 * 1 warmup + 3 timed calls per row; pass when median elapsed_ms <= target_p50_ms.
 */
import { spawn } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const corpusPath = join(root, "corpora", "agent-mvp-latency.jsonl");
const TIMED_RUNS = 3;
const REQUEST_TIMEOUT_MS = 180_000;

const ts = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const outArg = process.argv.find((a) => a.startsWith("--out="));
const outPath =
  outArg?.slice("--out=".length) ||
  join(root, "artifacts", "agent-mvp-gate", `${ts}-latency.json`);

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

  close() {
    this.#proc.stdin.end();
  }
}

function loadJsonl(path) {
  return readFileSync(path, "utf8")
    .trim()
    .split(/\n+/)
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function median(nums) {
  if (nums.length === 0) return null;
  const sorted = [...nums].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
}

async function timedCall(client, row) {
  const args = { url: row.url, ...row.args };
  const started = performance.now();
  await client.request("tools/call", { name: row.tool, arguments: args });
  return Math.round(performance.now() - started);
}

async function runRow(client, row) {
  const entry = {
    id: row.id,
    url: row.url,
    tool: row.tool,
    backend_policy: row.args?.backend_policy ?? null,
    target_p50_ms: row.target_p50_ms,
    warmup_ms: null,
    samples_ms: [],
    median_ms: null,
    pass: false,
  };

  try {
    entry.warmup_ms = await timedCall(client, row);
    for (let i = 0; i < TIMED_RUNS; i++) {
      entry.samples_ms.push(await timedCall(client, row));
    }
    entry.median_ms = median(entry.samples_ms);
    entry.pass = entry.median_ms <= row.target_p50_ms;
  } catch (err) {
    entry.error = String(err?.message ?? err);
    entry.pass = false;
  }

  const status = entry.pass ? "PASS" : entry.error ? "ERROR" : "SLOW";
  console.log(
    `[latency] ${row.id}: median=${entry.median_ms ?? "n/a"}ms target=${row.target_p50_ms}ms → ${status}`,
  );
  return entry;
}

async function main() {
  const rows = loadJsonl(corpusPath);
  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: {
      ...process.env,
      OCCAM_HOME: root,
      Logging__LogLevel__Default: "None",
      WT_OCCAM_BANNER: "0",
    },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new McpStdioClient(proc);
  const killTimer = setTimeout(() => proc.kill(), 30 * 60_000);

  const report = {
    ok: false,
    startedAt: new Date().toISOString(),
    occamHome: root,
    corpus: corpusPath,
    timedRunsPerRow: TIMED_RUNS,
    rows: [],
  };

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "agent-mvp-latency", version: "1.0" },
    });
    client.notify("notifications/initialized");

    for (const row of rows) {
      report.rows.push(await runRow(client, row));
    }
  } finally {
    clearTimeout(killTimer);
    client.close();
    proc.kill();
  }

  const passed = report.rows.filter((r) => r.pass).length;
  report.rollup = { total: report.rows.length, passed };
  report.ok = passed === report.rows.length;
  report.finishedAt = new Date().toISOString();

  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, JSON.stringify(report, null, 2), "utf8");
  console.log(`[latency] wrote ${outPath}`);
  console.log(JSON.stringify({ ok: report.ok, rollup: report.rollup, outPath }));
  process.exit(report.ok ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
