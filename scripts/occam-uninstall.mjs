#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { runRemovalCli } from "./lib/operator/uninstall.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const occamHome = resolve(process.env.OCCAM_HOME?.trim() || join(scriptDir, ".."));
const selfPath = resolve(fileURLToPath(import.meta.url));

/**
 * When invoked directly from inside OCCAM_HOME (not via CLI staging), re-exec
 * from a temp copy so Windows can delete the install tree.
 */
function maybeReexecOutsideInstall() {
  if (process.env.OCCAM_UNINSTALL_REEXEC === "1") return false;
  const homeKey = resolve(occamHome).toLowerCase();
  const selfKey = selfPath.toLowerCase();
  if (!selfKey.startsWith(homeKey + "\\") && !selfKey.startsWith(homeKey + "/")) {
    return false;
  }
  const stagingRoot = mkdtempSync(join(tmpdir(), "occam-uninstall-reexec-"));
  const scriptsSrc = join(occamHome, "scripts");
  const scriptsDst = join(stagingRoot, "scripts");
  cpSync(scriptsSrc, scriptsDst, { recursive: true });
  const staged = join(scriptsDst, "occam-uninstall.mjs");
  if (!existsSync(staged)) {
    try {
      rmSync(stagingRoot, { recursive: true, force: true });
    } catch {
      /* ignore */
    }
    return false;
  }
  const result = spawnSync(process.execPath, [staged, ...process.argv.slice(2)], {
    cwd: tmpdir(),
    env: { ...process.env, OCCAM_HOME: occamHome, OCCAM_UNINSTALL_REEXEC: "1" },
    stdio: "inherit",
  });
  try {
    rmSync(stagingRoot, { recursive: true, force: true, maxRetries: 5, retryDelay: 50 });
  } catch {
    /* best-effort */
  }
  process.exit(result.status ?? 1);
}

maybeReexecOutsideInstall();

runRemovalCli("uninstall", process.argv.slice(2), { occamHome })
  .then((code) => {
    process.exitCode = code;
  })
  .catch((err) => {
    console.error(err instanceof Error ? err.message : String(err));
    process.exitCode = 1;
  });
