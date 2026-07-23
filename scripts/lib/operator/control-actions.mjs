import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { dispatchSubcommand } from "./occam-cli-dispatch.mjs";
import { findSubcommand } from "./occam-cli-subcommands.mjs";
import { hostTargetToConnectionKind, getConnectionNextSteps } from "./mcp-snippet.mjs";
import {
  defaultOnboardPath,
  loadOnboardConfig,
  readOccamVersion,
} from "./onboard-schema.mjs";
import { checkForUpdate, readInstalledVersion } from "./update-check.mjs";

/**
 * @param {string} occamHome
 */
function readOnboardMeta(occamHome) {
  const path = defaultOnboardPath();
  if (!existsSync(path)) {
    return { path, profile: null, hostTarget: null, configured: false };
  }

  try {
    const parsed = JSON.parse(readFileSync(path, "utf8"));
    return {
      path,
      profile: typeof parsed.profile === "string" ? parsed.profile : null,
      hostTarget: typeof parsed.hostTarget === "string" ? parsed.hostTarget : null,
      configured: true,
    };
  } catch {
    return { path, profile: null, hostTarget: null, configured: false };
  }
}

/**
 * @param {string} occamHome
 * @param {{ fetch?: typeof fetch }} [opts]
 */
export async function showStatus(occamHome, opts = {}) {
  const installed = readInstalledVersion(occamHome);
  const onboard = readOnboardMeta(occamHome);
  const onboardEnv = loadOnboardConfig(onboard.path);
  let update = null;

  try {
    update = await checkForUpdate({ occamHome, fetch: opts.fetch });
  } catch (err) {
    update = {
      installed,
      latest: installed,
      updateAvailable: false,
      error: err instanceof Error ? err.message : String(err),
    };
  }

  return {
    ok: true,
    message: `FF-Occam ${installed} at ${occamHome}`,
    data: {
      occamHome,
      version: installed,
      generator: readOccamVersion(occamHome),
      onboard,
      onboardEnvKeys: Object.keys(onboardEnv.env),
      update,
    },
  };
}

/**
 * @param {string} occamHome
 * @param {string[]} [extraArgs]
 */
export function runDoctor(occamHome, extraArgs = []) {
  const sub = findSubcommand("doctor");
  if (!sub) {
    return { ok: false, message: "doctor subcommand missing" };
  }

  const code = dispatchSubcommand(sub, occamHome, extraArgs);
  return {
    ok: code === 0,
    message: code === 0 ? "doctor: OK" : `doctor failed (exit ${code})`,
    data: { exitCode: code },
  };
}

/**
 * @param {string} occamHome
 * @param {string[]} [extraArgs]
 */
export function runOnboard(occamHome, extraArgs = []) {
  const sub = findSubcommand("onboard");
  if (!sub) {
    return { ok: false, message: "onboard subcommand missing" };
  }

  const code = dispatchSubcommand(sub, occamHome, extraArgs);
  return {
    ok: code === 0,
    message: code === 0 ? "onboard complete" : `onboard failed (exit ${code})`,
    data: { exitCode: code },
  };
}

/**
 * @param {string} occamHome
 * @param {string[]} [extraArgs]
 */
export function runHelp(occamHome, extraArgs = ["next-steps"]) {
  const sub = findSubcommand("help");
  if (!sub) {
    return { ok: false, message: "help subcommand missing" };
  }

  const code = dispatchSubcommand(sub, occamHome, extraArgs);
  return {
    ok: code === 0,
    message: code === 0 ? "help printed" : `help failed (exit ${code})`,
    data: { exitCode: code },
  };
}

/**
 * @param {string} occamHome
 * @param {string[]} [extraArgs]
 */
export function runRefresh(occamHome, extraArgs = []) {
  const sub = findSubcommand("refresh");
  if (!sub) {
    return { ok: false, message: "refresh subcommand missing" };
  }

  const code = dispatchSubcommand(sub, occamHome, extraArgs);
  const onboard = readOnboardMeta(occamHome);
  const connectionKind = hostTargetToConnectionKind(onboard.hostTarget ?? "cursor");
  const reloadHints = getConnectionNextSteps(connectionKind).filter((line) =>
    /reload|restart/i.test(line),
  );

  return {
    ok: code === 0,
    message:
      code === 0
        ? ["Occam processes stopped and doctor re-ran.", ...reloadHints].join("\n")
        : `refresh failed (exit ${code})`,
    data: { exitCode: code, reloadHints },
  };
}

/**
 * @param {string} occamHome
 * @param {string[]} [extraArgs]
 */
export function runSmoke(occamHome, extraArgs = []) {
  const sub = findSubcommand("smoke");
  if (!sub) {
    return { ok: false, message: "smoke subcommand missing" };
  }

  const code = dispatchSubcommand(sub, occamHome, extraArgs);
  return {
    ok: code === 0,
    message: code === 0 ? "hermes-smoke: PASS" : `smoke failed (exit ${code})`,
    data: { exitCode: code },
  };
}

/**
 * @param {string} occamHome
 * @param {{ fetch?: typeof fetch }} [opts]
 */
export async function runUpdateCheck(occamHome, opts = {}) {
  const update = await checkForUpdate({ occamHome, fetch: opts.fetch });
  return {
    ok: !update.error || update.updateAvailable !== undefined,
    message: update.upgradeHint,
    data: update,
  };
}

/**
 * @param {'doctor'|'onboard'|'help'|'refresh'|'smoke'|'update'|'status'} action
 * @param {string} occamHome
 * @param {{ fetch?: typeof fetch, args?: string[] }} [opts]
 */
export async function runControlAction(action, occamHome, opts = {}) {
  const args = opts.args ?? [];

  switch (action) {
    case "doctor":
      return runDoctor(occamHome, args);
    case "onboard":
      return runOnboard(occamHome, args);
    case "help":
      return runHelp(occamHome, args.length ? args : ["next-steps"]);
    case "refresh":
      return runRefresh(occamHome, args);
    case "smoke":
      return runSmoke(occamHome, args);
    case "update":
      return runUpdateCheck(occamHome, opts);
    case "status":
      return showStatus(occamHome, opts);
    default:
      return { ok: false, message: `unknown action: ${action}` };
  }
}

/**
 * @param {string} occamHome
 */
export function spawnOccamHelpCatalog(occamHome) {
  const helpPath = join(occamHome, "scripts", "occam-help.mjs");
  if (!existsSync(helpPath)) {
    return { ok: false, message: `missing ${helpPath}` };
  }

  const result = spawnSync(process.execPath, [helpPath], {
    cwd: occamHome,
    env: { ...process.env, OCCAM_HOME: occamHome },
    stdio: "inherit",
  });

  return {
    ok: (result.status ?? 1) === 0,
    message: "help catalog printed",
    data: { exitCode: result.status ?? 1 },
  };
}
