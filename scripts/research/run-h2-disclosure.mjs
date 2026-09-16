#!/usr/bin/env node
/**
 * H2 progressive-disclosure experiment runner.
 *
 * Default mode is **mock** (deterministic selection model) — proves the scorecard pipeline.
 * Pass `--live` only when OCCAM_HOME workers exist; live results are written separately and
 * never mixed with mock numbers.
 *
 * Usage:
 *   node scripts/research/run-h2-disclosure.mjs
 *   node scripts/research/run-h2-disclosure.mjs --seed 42 --out artifacts/research/h2-mock.jsonl
 *   node scripts/research/run-h2-disclosure.mjs --live   # real cascade/transcode (slow)
 *
 * Marker: H2_PIPELINE_OK (mock mode, deterministic expectations hold)
 */
import { createWriteStream, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import {
  CAPABILITIES,
  SURFACES,
  mulberry32,
  simulateTaskSuccess,
} from "./mock-agents.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "../..");
const args = new Set(process.argv.slice(2));
const live = args.has("--live");
const seedArg = process.argv.indexOf("--seed");
const seed = seedArg >= 0 ? Number(process.argv[seedArg + 1]) || 1 : 1;
const outIdx = process.argv.indexOf("--out");
const outPath =
  outIdx >= 0
    ? process.argv[outIdx + 1]
    : join(root, "docs/research/results", live ? "h2-live.jsonl" : "h2-mock.jsonl");

const tasksPath = join(root, "docs/research/tasks/h2-web-reading.jsonl");
const tasks = readFileSync(tasksPath, "utf8")
  .split(/\r?\n/)
  .filter(Boolean)
  .map((line) => JSON.parse(line));

mkdirSync(dirname(outPath), { recursive: true });
const out = createWriteStream(outPath, { encoding: "utf8" });

const commit = spawnSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" })
  .stdout?.trim() || "unknown";

const meta = {
  protocol: "occam-h2-scorecard-v1",
  mode: live ? "live" : "mock",
  seed: live ? null : seed,
  hostCommit: commit,
  taskCount: tasks.length,
  surfaces: SURFACES,
  capabilities: CAPABILITIES,
  collectedAt: new Date().toISOString(),
  note: live
    ? "Live fetch arm. Do not mix with mock rows in analysis."
    : "Mock selection model only — not LLM measurements. H2 remains unmeasured against real models.",
};
out.write(`${JSON.stringify({ type: "meta", ...meta })}\n`);

const rand = mulberry32(seed);
const summary = [];

for (const capability of CAPABILITIES) {
  for (const surface of SURFACES) {
    let ok = 0;
    let irrelevant = 0;
    let missing = 0;
    let recovered = 0;
    const latencies = [];

    for (const task of tasks) {
      let row;
      if (live) {
        row = runLive(task, surface);
      } else {
        const sim = simulateTaskSuccess(task, capability, surface, rand);
        row = {
          type: "trial",
          taskId: task.id,
          capability,
          surface,
          ok: sim.ok,
          tool: sim.tool,
          irrelevant: !!sim.irrelevant,
          missingCapability: !!sim.missingCapability,
          recovered: !!sim.recovered,
          callsToSuccess: sim.callsToSuccess,
          reason: sim.reason,
          latencyMs: null,
        };
      }
      out.write(`${JSON.stringify(row)}\n`);
      if (row.ok) ok++;
      if (row.irrelevant) irrelevant++;
      if (row.missingCapability) missing++;
      if (row.recovered) recovered++;
      if (typeof row.latencyMs === "number") latencies.push(row.latencyMs);
    }

    const cell = {
      type: "cell",
      capability,
      surface,
      n: tasks.length,
      successRate: ok / tasks.length,
      ok,
      irrelevantRate: irrelevant / tasks.length,
      missingCapabilityCount: missing,
      recoveredCount: recovered,
      latencyP50Ms: percentile(latencies, 0.5),
    };
    summary.push(cell);
    out.write(`${JSON.stringify(cell)}\n`);
  }
}

out.end();

// Pipeline assertions (mock only): weak improves as surface shrinks; strong not monotone-helped by minimal.
let pipelineOk = !live;
if (!live) {
  const rate = (cap, surf) =>
    summary.find((c) => c.capability === cap && c.surface === surf)?.successRate ?? 0;
  const weakFull = rate("weak", "full");
  const weakMinimal = rate("weak", "minimal");
  const strongFull = rate("strong", "full");
  const strongMinimal = rate("strong", "minimal");
  // Weak should do better (or equal) on minimal than full — selection failure removed.
  if (!(weakMinimal >= weakFull - 1e-9)) pipelineOk = false;
  // Distinguishing prediction: strong on minimal should not dominate strong on full
  // (digest/search tasks are impossible on minimal).
  if (!(strongMinimal < strongFull - 1e-9)) pipelineOk = false;

  const summaryPath = outPath.replace(/\.jsonl$/, "-summary.json");
  writeFileSync(
    summaryPath,
    `${JSON.stringify(
      {
        protocol: "occam-h2-summary-v1",
        mode: "mock",
        seed,
        hostCommit: commit,
        weakMinimal,
        weakFull,
        strongMinimal,
        strongFull,
        cells: summary,
        pipelineOk,
      },
      null,
      2,
    )}\n`,
  );
}

if (pipelineOk) {
  console.log("H2_PIPELINE_OK");
  console.error(`[h2] wrote ${outPath}`);
  process.exit(0);
}

if (live) {
  console.error(`[h2] live scorecard written to ${outPath} (no H2_PIPELINE_OK — live is measurement, not selftest)`);
  process.exit(0);
}

console.error("H2_PIPELINE_FAIL: mock expectations not met");
process.exit(1);

function percentile(values, p) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const idx = Math.floor((sorted.length - 1) * p);
  return sorted[idx];
}

function runLive(task, surface) {
  const env = {
    ...process.env,
    OCCAM_HOME: process.env.OCCAM_HOME || root,
    OCCAM_PROFILE: surface,
  };
  const start = Date.now();
  const needs = task.needs?.[0] || "read";
  let result = { ok: false, failure: "not_attempted" };

  if (needs === "digest" || needs === "search") {
    // Live multi-tool arms need MCP; cascade CLI covers read-only for now.
    result = { ok: false, failure: "live_arm_requires_mcp_for_digest_search" };
  } else {
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
    const proc = spawnSync("dotnet", args, { env, encoding: "utf8", maxBuffer: 4 * 1024 * 1024 });
    const line = (proc.stdout || "")
      .split(/\r?\n/)
      .filter((l) => l.startsWith("{"))
      .pop();
    try {
      const parsed = JSON.parse(line || "{}");
      const md = parsed.markdown || "";
      const must = task.must_contain || [];
      const hit = must.every((s) => md.toLowerCase().includes(String(s).toLowerCase()));
      result = {
        ok: !!parsed.ok && hit,
        failure: parsed.ok ? (hit ? null : "must_contain_miss") : parsed.failure?.code || "extract_failed",
        backend: parsed.backend || null,
      };
    } catch {
      result = { ok: false, failure: "json_parse_error" };
    }
  }

  return {
    type: "trial",
    taskId: task.id,
    capability: null,
    surface,
    ok: result.ok,
    tool: "occam",
    irrelevant: false,
    missingCapability: needs !== "read",
    recovered: false,
    callsToSuccess: result.ok ? 1 : null,
    reason: result.failure,
    backend: result.backend || null,
    latencyMs: Date.now() - start,
  };
}
