#!/usr/bin/env node
/**
 * Bootstrap OCCAM_SETUP contract (shared by get-ff-occam.sh / .ps1 / welcome CLI).
 *
 *   unset | auto | 1  → auto (never prompt)
 *   manual | 2        → manual
 *   ask               → prompt only when truly interactive; else auto
 */
/**
 * @param {NodeJS.ProcessEnv} [env]
 * @returns {"auto"|"manual"|"ask"}
 */
export function readSetupIntent(env = process.env) {
  const raw = env.OCCAM_SETUP?.trim().toLowerCase() ?? "";
  if (!raw || raw === "auto" || raw === "1") {
    return "auto";
  }
  if (raw === "manual" || raw === "2") {
    return "manual";
  }
  if (raw === "ask") {
    return "ask";
  }
  throw new Error(`invalid OCCAM_SETUP=${raw} (use auto|manual|ask)`);
}

/**
 * @param {string} raw
 * @returns {"auto"|"manual"}
 */
export function parseSetupChoice(raw) {
  const choice = (raw ?? "").trim() || "1";
  if (choice === "2" || /^manual$/i.test(choice)) {
    return "manual";
  }
  return "auto";
}

/**
 * @param {{ env?: NodeJS.ProcessEnv; interactive?: boolean }} [opts]
 * @returns {"auto"|"manual"|"ask"}
 */
export function resolveSetupIntent(opts = {}) {
  const intent = readSetupIntent(opts.env ?? process.env);
  if (intent === "ask" && opts.interactive !== true) {
    return "auto";
  }
  return intent;
}
