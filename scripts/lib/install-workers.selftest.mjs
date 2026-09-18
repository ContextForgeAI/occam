#!/usr/bin/env node
/**
 * Selftest: empty/corrupt workers node_modules → ensureWorkersDeps → extract works.
 *
 *   node scripts/lib/install-workers.selftest.mjs
 *
 * Marker: INSTALL_WORKERS_SELFTEST_OK
 */
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ensureWorkersDeps, workersDepsHealthy } from "./ensure-workers-deps.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const staging = join(repoRoot, "artifacts", "install-workers-selftest");

function fail(msg) {
  console.error(`[install-workers.selftest] FAIL ${msg}`);
  process.exit(1);
}

function main() {
  rmSync(staging, { recursive: true, force: true });
  mkdirSync(staging, { recursive: true });

  // Copy workers sources without node_modules (tarball-shaped tree).
  cpSync(join(repoRoot, "workers"), join(staging, "workers"), {
    recursive: true,
    filter: (src) => !src.split(/[/\\]/).includes("node_modules"),
  });
  // Copy ensure helper + package scripts path layout expected by OCCAM_HOME.
  mkdirSync(join(staging, "scripts", "lib"), { recursive: true });
  cpSync(join(here, "ensure-workers-deps.mjs"), join(staging, "scripts", "lib", "ensure-workers-deps.mjs"));

  if (workersDepsHealthy(join(staging, "workers"))) {
    fail("staging unexpectedly healthy before install");
  }

  // Corrupt trap: empty node_modules must still trigger repair.
  mkdirSync(join(staging, "workers", "node_modules"), { recursive: true });
  writeFileSync(join(staging, "workers", "node_modules", ".keep"), "");
  if (workersDepsHealthy(join(staging, "workers"))) {
    fail("empty node_modules reported healthy");
  }

  const ensured = ensureWorkersDeps(staging, { quiet: true });
  if (!ensured.ok) {
    fail(`ensureWorkersDeps: ${ensured.detail}`);
  }
  if (!workersDepsHealthy(join(staging, "workers"))) {
    fail("markers still unhealthy after ensure");
  }

  const extract = join(staging, "workers", "http-extract", "extract.mjs");
  const run = spawnSync(process.execPath, [extract, "https://example.com"], {
    cwd: staging,
    env: { ...process.env, OCCAM_HOME: staging },
    encoding: "utf8",
  });
  if ((run.status ?? 1) !== 0) {
    fail(`extract exit ${run.status}: ${run.stderr || run.stdout}`);
  }
  let parsed;
  try {
    parsed = JSON.parse((run.stdout || "").trim().split("\n").pop());
  } catch (err) {
    fail(`extract stdout not JSON: ${err.message}\n${run.stdout}`);
  }
  if (parsed?.ok !== true) {
    fail(`extract ok!=true: ${JSON.stringify(parsed)}`);
  }

  // Idempotent second pass skips install.
  const again = ensureWorkersDeps(staging, { quiet: true });
  if (!again.ok || !again.skipped) {
    fail(`expected skipped healthy ensure, got ${JSON.stringify(again)}`);
  }

  console.log("INSTALL_WORKERS_SELFTEST_OK");
}

main();
