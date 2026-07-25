/**
 * Stable Occam MCP launch specification — single source of truth for host registration.
 * Never register temp extract paths, CI worktrees, or disposable version dirs.
 *
 * Host adapters choose how to project this spec (wrapper vs node, cwd, etc.).
 * Core does not name products here.
 */
import { existsSync } from "node:fs";
import { join } from "node:path";
import { platform } from "node:os";
import { OCCAM_MANAGED_ENV_KEY, OCCAM_MANAGED_MARKER } from "./kinds.mjs";

/**
 * @param {string} occamHome
 */
export function normalizeOccamHome(occamHome) {
  return String(occamHome || "").trim().replace(/[\\/]+$/, "");
}

/**
 * @param {string} occamHome
 * @returns {{
 *   command: string,
 *   args: string[],
 *   env: Record<string, string>,
 *   cwd: string,
 *   wrapperPath: string|null,
 *   launcherPath: string,
 *   wrapperUsable: boolean,
 * }}
 */
export function buildStableLaunchSpec(occamHome) {
  const home = normalizeOccamHome(occamHome);
  if (!home) {
    throw new Error("OCCAM_HOME is required for stable launch spec");
  }

  const launcherPath = join(home, "scripts", "launch-mcp-host.mjs");
  const wrapperPath = join(home, "scripts", "occam-wrapper.sh");
  const isWin = platform() === "win32";
  const wrapperExists = existsSync(wrapperPath);
  // Windows hosts spawn stdio without a POSIX shell — wrapper is not usable there.
  const wrapperUsable = !isWin && wrapperExists;

  /** @type {Record<string, string>} */
  const env = {
    OCCAM_HOME: home,
    OCCAM_BANNER: "0",
    WT_OCCAM_BANNER: "0",
    [OCCAM_MANAGED_ENV_KEY]: OCCAM_MANAGED_MARKER,
  };

  return {
    command: "node",
    args: [launcherPath],
    env,
    cwd: home,
    wrapperPath: wrapperExists ? wrapperPath : null,
    launcherPath,
    wrapperUsable,
  };
}

/**
 * Project stable launch into a stdio MCP registration shape.
 * @param {ReturnType<typeof buildStableLaunchSpec>} spec
 * @param {{ preferWrapper?: boolean, includeCwd?: boolean }} [opts]
 */
export function stdioFromSpec(spec, opts = {}) {
  const preferWrapper = opts.preferWrapper === true && spec.wrapperUsable && spec.wrapperPath;
  /** @type {{ command: string, args: string[], env: Record<string, string>, cwd?: string }} */
  const out = preferWrapper
    ? {
        command: /** @type {string} */ (spec.wrapperPath),
        args: [],
        env: { ...spec.env },
      }
    : {
        command: spec.command,
        args: [...spec.args],
        env: { ...spec.env },
      };
  if (opts.includeCwd) {
    out.cwd = spec.cwd;
  }
  return out;
}

/**
 * @param {string} occamHome
 */
export function assertLaunchable(occamHome) {
  const spec = buildStableLaunchSpec(occamHome);
  if (!existsSync(spec.launcherPath)) {
    throw new Error(`missing stable launcher: ${spec.launcherPath}`);
  }
  return spec;
}
