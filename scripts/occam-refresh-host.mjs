#!/usr/bin/env node
/**
 * Stop running MCP host → occam-doctor (AOT publish) → optional hermes-smoke.
 *
 * Cursor/Hermes MCP reload is NOT automatable — script prints the operator step.
 *
 * Usage:
 *   node scripts/occam-refresh-host.mjs
 *   node scripts/occam-refresh-host.mjs --dry-run
 *   node scripts/occam-refresh-host.mjs --skip-stop --smoke
 *   node scripts/occam-refresh-host.mjs --include-dotnet
 */
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  isPublishExeLocked,
  listOccamHostProcesses,
  publishExePath,
  stopOccamHostProcesses,
} from "./lib/stop-occam-processes.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
process.env.OCCAM_HOME = root;

const args = new Set(process.argv.slice(2));
const dryRun = args.has("--dry-run");
const skipStop = args.has("--skip-stop");
const skipDoctor = args.has("--skip-doctor");
const runSmoke = args.has("--smoke");
const includeDotnet = args.has("--include-dotnet");

function log(msg) {
  console.error(`[occam-refresh] ${msg}`);
}

function runDoctor() {
  if (process.platform === "win32") {
    const ps1 = join(root, "scripts", "occam-doctor.ps1");
    if (!fs.existsSync(ps1)) {
      throw new Error(`missing ${ps1}`);
    }
    const r = spawnSync(
      "powershell",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps1],
      { cwd: root, env: { ...process.env, OCCAM_HOME: root }, stdio: "inherit" },
    );
    if (r.status !== 0) {
      throw new Error(`occam-doctor.ps1 failed (exit ${r.status ?? 1})`);
    }
    return;
  }

  const sh = join(root, "scripts", "occam-doctor.sh");
  if (!fs.existsSync(sh)) {
    throw new Error(`missing ${sh}`);
  }
  const r = spawnSync("bash", [sh], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root },
    stdio: "inherit",
  });
  if (r.status !== 0) {
    throw new Error(`occam-doctor.sh failed (exit ${r.status ?? 1})`);
  }
}

function runHermesSmoke() {
  const smoke = join(root, "scripts", "hermes-smoke.mjs");
  if (!fs.existsSync(smoke)) {
    throw new Error(`missing ${smoke}`);
  }
  const env = { ...process.env, OCCAM_HOME: root };
  delete env.OCCAM_FORCE_DOTNET_RUN;
  const r = spawnSync(process.execPath, [smoke], { cwd: root, env, stdio: "inherit" });
  if (r.status !== 0) {
    throw new Error(`hermes-smoke.mjs failed (exit ${r.status ?? 1})`);
  }
}

function printReloadHint() {
  log("");
  log("Publish complete. Reload MCP in your host:");
  log("  Cursor: Settings → MCP → Reload (or restart Cursor)");
  log("  Hermes: restart the ff-occam MCP child process");
  log("");
  log("After reload, tools/list should show 9 occam_* tools with the new binary.");
  log(`Published host: ${publishExePath(root)}`);
}

try {
  log(`OCCAM_HOME=${root}`);

  const before = listOccamHostProcesses(root, { includeDotnet });
  if (before.length > 0) {
    log(`found ${before.length} MCP host process(es):`);
    for (const p of before) {
      log(`  PID ${p.pid} ${p.name}`);
    }
  } else {
    log("no running FFOccamMcp.Core / launch-mcp-host processes found");
  }

  if (dryRun) {
    const locked = isPublishExeLocked(root);
    log(`publish exe locked: ${locked}`);
    log(`publish path: ${publishExePath(root)}`);
    log("dry-run — no processes stopped, doctor not run");
    process.exit(0);
  }

  if (!skipStop) {
    const { stopped, stillLocked } = stopOccamHostProcesses(root, {
      includeDotnet,
      graceMs: 1500,
      force: true,
    });
    if (stopped.length > 0) {
      log(`stopped ${stopped.length} process(es)`);
    }
    if (stillLocked) {
      log("warning: publish binary still locked — close Cursor MCP or kill remaining PIDs, then re-run");
      const left = listOccamHostProcesses(root, { includeDotnet });
      for (const p of left) {
        log(`  still running PID ${p.pid} ${p.name}`);
      }
      process.exit(2);
    }
  } else {
    log("--skip-stop: not stopping running hosts");
    if (isPublishExeLocked(root)) {
      log("error: publish exe is locked — remove --skip-stop or close MCP host");
      process.exit(2);
    }
  }

  if (!skipDoctor) {
    log("running occam-doctor (dotnet publish) ...");
    runDoctor();
    log("doctor: OK");
  } else {
    log("--skip-doctor: publish step skipped");
  }

  if (runSmoke) {
    log("running hermes-smoke (fresh subprocess, uses published binary) ...");
    runHermesSmoke();
    log("hermes-smoke: PASS");
  }

  printReloadHint();
  process.exit(0);
} catch (err) {
  log(`error: ${err?.message ?? err}`);
  process.exit(1);
}
