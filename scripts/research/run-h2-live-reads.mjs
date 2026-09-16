#!/usr/bin/env node
/**
 * H2 live read-arm: cascade extract only (needs=read tasks).
 * Does NOT measure tool-selection — that requires an LLM behind MCP.
 * Surfaces are recorded for env parity; extract path is the same cascade CLI.
 *
 *   node scripts/research/run-h2-live-reads.mjs
 *   node scripts/research/run-h2-live-reads.mjs --out docs/research/results/h2-live-reads.jsonl
 *
 * Marker: H2_LIVE_READS_OK when every read task returns ok + must_contain hit.
 */
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "../..");
const outIdx = process.argv.indexOf("--out");
const outPath =
  outIdx >= 0
    ? process.argv[outIdx + 1]
    : join(root, "docs/research/results/h2-live-reads.jsonl");

const tasks = readFileSync(join(root, "docs/research/tasks/h2-web-reading.jsonl"), "utf8")
  .split(/\r?\n/)
  .filter(Boolean)
  .map((l) => JSON.parse(l))
  .filter((t) => (t.needs?.[0] || "read") === "read");

const commit =
  spawnSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).stdout?.trim() ||
  "unknown";

mkdirSync(dirname(outPath), { recursive: true });
const lines = [
  JSON.stringify({
    type: "meta",
    protocol: "occam-h2-live-reads-v1",
    hostCommit: commit,
    taskCount: tasks.length,
    collectedAt: new Date().toISOString(),
    note: "Extract success only. Not H2 tool-selection. Not mixed with mock rows.",
  }),
];

let okCount = 0;
const latencies = [];

for (const task of tasks) {
  const start = Date.now();
  const args = [
    "run",
    "--project",
    join(root, "src/FFOccamMcp.Core"),
    "-c",
    "Release",
    "--no-build",
    "--",
    "cascade",
    "run",
    `--url=${task.url}`,
    `--budget=${task.budget || 512}`,
  ];
  if (task.focus) args.push(`--task=${task.focus}`);

  const proc = spawnSync("dotnet", args, {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: process.env.OCCAM_HOME || root },
    encoding: "utf8",
    maxBuffer: 8 * 1024 * 1024,
  });
  const ms = Date.now() - start;
  const line = (proc.stdout || "")
    .split(/\r?\n/)
    .filter((l) => l.startsWith("{"))
    .pop();

  let parsed = {};
  try {
    parsed = JSON.parse(line || "{}");
  } catch {
    parsed = {};
  }

  const md = parsed.markdown || "";
  const must = task.must_contain || [];
  const hit = must.every((s) => md.toLowerCase().includes(String(s).toLowerCase()));
  const ok = !!parsed.ok && hit;
  if (ok) {
    okCount++;
    latencies.push(ms);
  }

  const row = {
    type: "trial",
    taskId: task.id,
    url: task.url,
    ok,
    mustContainHit: hit,
    extractOk: !!parsed.ok,
    backend: parsed.backend || null,
    failure: parsed.ok
      ? hit
        ? null
        : "must_contain_miss"
      : parsed.failure?.code || "extract_failed",
    latencyMs: ms,
    exitCode: proc.status,
  };
  lines.push(JSON.stringify(row));
  console.error(
    `h2-live-reads: ${ok ? "OK" : "FAIL"} ${ms}ms ${task.id} ${row.failure || ""}`.trim(),
  );
}

const sorted = [...latencies].sort((a, b) => a - b);
const summary = {
  protocol: "occam-h2-live-reads-summary-v1",
  hostCommit: commit,
  n: tasks.length,
  ok: okCount,
  fail: tasks.length - okCount,
  successRate: okCount / tasks.length,
  latencyP50Ms: sorted.length ? sorted[Math.floor((sorted.length - 1) * 0.5)] : null,
  latencyP95Ms: sorted.length ? sorted[Math.floor((sorted.length - 1) * 0.95)] : null,
  allOk: okCount === tasks.length,
};
// Sync writes: WriteStream + process.exit dropped the jsonl on Windows.
writeFileSync(outPath, `${lines.join("\n")}\n`);
writeFileSync(
  outPath.replace(/\.jsonl$/, "-summary.json"),
  `${JSON.stringify(summary, null, 2)}\n`,
);

if (summary.allOk) {
  console.log("H2_LIVE_READS_OK");
  process.exit(0);
}
console.error(`H2_LIVE_READS_FAIL ok=${okCount}/${tasks.length}`);
process.exit(1);
