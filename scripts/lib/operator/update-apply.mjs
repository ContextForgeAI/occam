/**
 * Apply an Occam release update (download + atomic tree replace via get-ff-occam).
 *
 * Decision logic lives in update-check.mjs (`decideUpdateAction`). This module
 * runs the bootstrap only when the decision is `upgrade`.
 *
 *   import { runOccamUpdate } from "./update-apply.mjs"
 */
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import { checkForUpdate, decideUpdateAction } from "./update-check.mjs";

/**
 * Copy local get-ff-occam bootstrap to a temp dir and run it with OCCAM_VERSION /
 * OCCAM_INSTALL_DIR so the live install tree can be replaced safely.
 *
 * @param {{ version: string, installDir: string, spawn?: typeof spawnSync }} opts
 * @returns {{ ok: boolean, detail: string }}
 */
export function defaultApplyReleaseUpdate(opts) {
  const version = String(opts.version || "").replace(/^v/i, "").trim();
  const installDir = opts.installDir;
  const spawn = opts.spawn ?? spawnSync;

  if (!version) {
    return { ok: false, detail: "missing target version" };
  }
  if (!installDir || !existsSync(installDir)) {
    return { ok: false, detail: `install dir missing: ${installDir}` };
  }

  const isWin = process.platform === "win32";
  const scriptName = isWin ? "get-ff-occam.ps1" : "get-ff-occam.sh";
  const src = join(installDir, "scripts", scriptName);
  if (!existsSync(src)) {
    return {
      ok: false,
      detail: `missing ${scriptName} under install (need a Level B release tree)`,
    };
  }

  const tmp = mkdtempSync(join(tmpdir(), "occam-update-"));
  try {
    const dest = join(tmp, scriptName);
    cpSync(src, dest);
    const env = {
      ...process.env,
      OCCAM_VERSION: version,
      OCCAM_INSTALL_DIR: installDir,
      OCCAM_HOME: installDir,
    };

    /** @type {import("node:child_process").SpawnSyncReturns<Buffer>} */
    let result;
    if (isWin) {
      result = spawn(
        "powershell.exe",
        ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", dest],
        { env, stdio: "inherit", shell: false },
      );
    } else {
      result = spawn("bash", [dest], { env, stdio: "inherit", shell: false });
    }

    if (result.error) {
      return { ok: false, detail: result.error.message };
    }
    const code = result.status ?? 1;
    return {
      ok: code === 0,
      detail: code === 0 ? `bootstrap ok (v${version})` : `bootstrap exit ${code}`,
    };
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
}

/**
 * @param {string} occamHome
 * @param {{
 *   force?: boolean,
 *   fetch?: typeof fetch,
 *   apply?: typeof defaultApplyReleaseUpdate,
 *   check?: typeof checkForUpdate,
 * }} [opts]
 */
export async function runOccamUpdate(occamHome, opts = {}) {
  const check = opts.check ?? checkForUpdate;
  const apply = opts.apply ?? defaultApplyReleaseUpdate;
  const force = opts.force === true;

  const update = await check({ occamHome, fetch: opts.fetch });
  const decision = decideUpdateAction({
    installed: update.installed,
    latest: update.latest,
    force,
    error: update.error,
  });

  if (decision.action === "noop") {
    return {
      ok: true,
      message: decision.message,
      data: { ...update, decision, applied: false },
    };
  }

  if (decision.action === "error") {
    return {
      ok: false,
      message: decision.message,
      data: { ...update, decision, applied: false },
    };
  }

  // upgrade
  const applied = await Promise.resolve(
    apply({
      version: decision.targetVersion,
      installDir: occamHome,
    }),
  );

  if (!applied.ok) {
    return {
      ok: false,
      message: `${decision.message} — apply failed: ${applied.detail}`,
      data: { ...update, decision, applied: false, applyDetail: applied.detail },
    };
  }

  return {
    ok: true,
    message: `${decision.message} — ${applied.detail}`,
    data: {
      ...update,
      decision,
      applied: true,
      applyDetail: applied.detail,
      installedAfter: decision.targetVersion,
    },
  };
}
