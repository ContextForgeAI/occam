#!/usr/bin/env node
/**
 * Agent-First MVP Phase 3 — single gate entrypoint (Hermes CI / maintainer).
 *
 * Usage:
 *   node scripts/run-agent-mvp-gate.mjs
 *   node scripts/run-agent-mvp-gate.mjs --latency
 *   node scripts/run-agent-mvp-gate.mjs --skip-refresh
 *
 * Requires published AOT (no OCCAM_FORCE_DOTNET_RUN=1).
 * Last stdout line: JSON summary { ok, hermes, popularHosts, latency, passBar }.
 */
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { isPublishExeLocked } from "./lib/stop-occam-processes.mjs";
import { resolveHostBinary } from "./lib/resolve-host-binary.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const args = new Set(process.argv.slice(2));
const runLatency = args.has("--latency");
const skipRefresh = args.has("--skip-refresh");
const PASS_BAR = 12;

function log(msg) {
  console.error(`[mvp-gate] ${msg}`);
}

function parseLastJsonLine(stdout) {
  const lines = stdout.trim().split(/\n+/).filter(Boolean);
  for (let i = lines.length - 1; i >= 0; i--) {
    try {
      return JSON.parse(lines[i]);
    } catch {
      continue;
    }
  }
  return null;
}

function runStep(label, script, extraArgs = []) {
  log(`running ${label} ...`);
  const env = { ...process.env, OCCAM_HOME: root };
  delete env.OCCAM_FORCE_DOTNET_RUN;
  const r = spawnSync(process.execPath, [script, ...extraArgs], {
    cwd: root,
    env,
    encoding: "utf8",
    maxBuffer: 20 * 1024 * 1024,
  });
  if (r.stdout) process.stdout.write(r.stdout);
  if (r.stderr) process.stderr.write(r.stderr);
  const parsed = parseLastJsonLine(r.stdout ?? "");
  const ok = r.status === 0;
  log(`${label}: ${ok ? "PASS" : "FAIL"} (exit ${r.status ?? 1})`);
  return { ok, exitCode: r.status ?? 1, parsed };
}

function main() {
  if (process.env.OCCAM_FORCE_DOTNET_RUN === "1") {
    log("OCCAM_FORCE_DOTNET_RUN=1 — gate requires published AOT; unset and re-run");
    process.exit(2);
  }

  const hostBinary = resolveHostBinary(root);
  if (!hostBinary) {
    log("no published host binary — run scripts/occam-doctor.ps1 (or .sh) first");
    process.exit(2);
  }
  log(`host binary: ${hostBinary}`);

  if (!skipRefresh && isPublishExeLocked(root)) {
    log("publish exe locked — close Cursor/Hermes MCP, then run: occam refresh --smoke");
    log("(continuing gate with existing publish; use refresh-host before release claims)");
  }

  const hermesScript = join(root, "scripts", "hermes-smoke.mjs");
  const popularScript = join(root, "scripts", "run-agent-popular-hosts.mjs");
  const latencyScript = join(root, "scripts", "run-agent-mvp-latency.mjs");

  const hermes = runStep("hermes-smoke", hermesScript);
  const popularHosts = runStep("popular-hosts", popularScript);
  const latency = runLatency
    ? runStep("latency", latencyScript)
    : { ok: null, exitCode: null, parsed: null };

  const popularPassed = popularHosts.parsed?.passed ?? popularHosts.parsed?.rollup?.passed ?? null;
  const popularTotal = popularHosts.parsed?.total ?? popularHosts.parsed?.rollup?.total ?? 15;

  const summary = {
    ok:
      hermes.ok &&
      popularHosts.ok &&
      (!runLatency || latency.ok === true),
    hermes: hermes.ok,
    popularHosts: {
      ok: popularHosts.ok,
      passed: popularPassed,
      total: popularTotal,
    },
    latency: runLatency
      ? {
          ok: latency.ok,
          rollup: latency.parsed?.rollup ?? null,
          outPath: latency.parsed?.outPath ?? null,
        }
      : null,
    passBar: PASS_BAR,
    hostBinary,
    occamForceDotnetRun: false,
  };

  console.log(JSON.stringify(summary));
  process.exit(summary.ok ? 0 : 1);
}

main();
