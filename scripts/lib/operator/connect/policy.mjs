/**
 * Desktop vs CI/server connect policy.
 * CI/server installs must not mutate desktop host configs by default.
 *
 * Explicit OCCAM_CONNECT=auto|on does NOT bypass CI unless OCCAM_CONNECT_FORCE=1.
 */

/**
 * @param {NodeJS.ProcessEnv} [env]
 */
export function isCiLike(env = process.env) {
  return (
    env.CI === "1" ||
    env.CI === "true" ||
    env.GITHUB_ACTIONS === "true" ||
    env.OCCAM_CONNECT_CI === "1"
  );
}

/**
 * @param {NodeJS.ProcessEnv} [env]
 */
function wantsForceMutation(env = process.env) {
  const v = env.OCCAM_CONNECT_FORCE?.trim().toLowerCase() ?? "";
  return v === "1" || v === "true" || v === "yes";
}

/**
 * @param {NodeJS.ProcessEnv} [env]
 * @param {{ interactive?: boolean }} [opts]
 */
export function resolveConnectMode(env = process.env, opts = {}) {
  const explicit = env.OCCAM_CONNECT?.trim().toLowerCase() ?? "";
  const ci = isCiLike(env);
  const force = wantsForceMutation(env);

  if (explicit === "off" || explicit === "0" || explicit === "none") {
    return { mode: "off", mutateHosts: false, reason: "OCCAM_CONNECT=off" };
  }
  if (explicit === "detect" || explicit === "report") {
    return { mode: "detect-only", mutateHosts: false, reason: "OCCAM_CONNECT=detect" };
  }

  if (explicit === "on" || explicit === "1" || explicit === "auto") {
    if (ci && !force) {
      return {
        mode: "ci",
        mutateHosts: false,
        reason:
          "CI/server — OCCAM_CONNECT=auto ignored without OCCAM_CONNECT_FORCE=1",
      };
    }
    return {
      mode: "auto",
      mutateHosts: true,
      reason: ci ? "OCCAM_CONNECT=auto with OCCAM_CONNECT_FORCE=1" : "OCCAM_CONNECT=auto",
    };
  }

  if (ci) {
    return { mode: "ci", mutateHosts: false, reason: "CI/server — no desktop mutation by default" };
  }

  // Default desktop: auto-connect safe Tier A adapters when not CI.
  if (opts.interactive === true || process.stdin.isTTY) {
    return { mode: "auto", mutateHosts: true, reason: "desktop interactive default" };
  }

  // Non-TTY without CI (e.g. curl|bash): allow auto-connect — North Star one-liner.
  // Operators opt out with OCCAM_CONNECT=off.
  return { mode: "auto", mutateHosts: true, reason: "desktop bootstrap default" };
}
