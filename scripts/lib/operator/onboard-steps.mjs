import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { renderOnboardWelcome, renderStepPrompt } from "./render/onboard-tty-renderer.mjs";

/** @typedef {'cursor'|'hermes'|'openclaw'|'claude-desktop'|'generic-stdio'|'cli-only'} HostTarget */
/** @typedef {'bundled'|'system-dev'} BrowserChoice */
/** @typedef {'default'|'hermes-headless'|'mass-scrape'} OnboardProfile */

/** @type {OnboardProfile[]} */
export const PROFILE_IDS = ["default", "hermes-headless", "mass-scrape"];

/**
 * @param {OnboardProfile} profile
 * @returns {Record<string, string>}
 */
export function applyProfile(profile) {
  /** @type {Record<string, string>} */
  const env = {};

  switch (profile) {
    case "hermes-headless":
      env.OCCAM_BANNER = "0";
      env.WT_OCCAM_BANNER = "0";
      env.OCCAM_BROWSER_PROFILE = "shared";
      env.OCCAM_BROWSER_POOL_SIZE = "1";
      break;
    case "mass-scrape":
      env.OCCAM_BROWSER_PROFILE = "isolated";
      env.OCCAM_BROWSER_DAEMON = "0";
      env.OCCAM_BROWSER_MAX_PARALLEL = "4";
      env.OCCAM_BROWSER_POOL_SIZE = "4";
      env.OCCAM_DIGEST_MAX_PARALLEL = "4";
      break;
    case "default":
    default:
      env.OCCAM_BROWSER_POOL_SIZE = "1";
      break;
  }

  return env;
}

/**
 * @param {BrowserChoice} browser
 * @returns {Record<string, string>}
 */
export function applyBrowserChoice(browser) {
  if (browser === "system-dev") {
    return { OCCAM_BROWSER_CHANNEL: "chrome" };
  }

  return {};
}

/**
 * @param {boolean} useProxy
 * @returns {Record<string, string>}
 */
export function applyProxyChoice(useProxy) {
  if (!useProxy) {
    return {};
  }

  return {
    OCCAM_PROXY_LIST_FILE: "~/.occam/proxy-list.txt",
  };
}

/** @typedef {{ id: string, label: string, validate: (v: string) => string|null }} OnboardStepDef */

const HOST_TARGET_IDS = ["cursor", "hermes", "openclaw", "claude-desktop", "generic-stdio", "cli-only"];

/** @type {OnboardStepDef[]} */
export const STEP_DEFS = [
  {
    id: "occamHome",
    label: "OCCAM_HOME path",
    validate: (v) => (v.trim() ? null : "OCCAM_HOME is required"),
  },
  {
    id: "hostTarget",
    label: "Primary MCP host · cursor | hermes | openclaw | claude-desktop | generic-stdio | cli-only",
    validate: (v) =>
      HOST_TARGET_IDS.includes(v.trim().toLowerCase())
        ? null
        : `Choose ${HOST_TARGET_IDS.join(", ")}`,
  },
  // Prefer `occam connect` for live host registration; onboard hostTarget is snippet preference only.
  {
    id: "browser",
    label: "Browser · bundled | system-dev",
    validate: (v) =>
      ["bundled", "system-dev"].includes(v.trim().toLowerCase()) ? null : "Choose bundled or system-dev",
  },
  {
    id: "proxy",
    label: "Proxy pool · yes | no",
    validate: (v) => {
      const n = v.trim().toLowerCase();
      return n === "yes" || n === "no" || n === "y" || n === "n" ? null : "Answer yes or no";
    },
  },
  {
    id: "profile",
    label: "Profile · default | hermes-headless | mass-scrape",
    validate: (v) => (PROFILE_IDS.includes(v.trim()) ? null : `Choose ${PROFILE_IDS.join(", ")}`),
  },
];

/**
 * @param {Record<string, string>} answers
 */
export function normalizeAnswers(answers) {
  const profile = /** @type {OnboardProfile} */ (
    PROFILE_IDS.includes(answers.profile?.trim()) ? answers.profile.trim() : "default"
  );
  const browser = answers.browser?.trim().toLowerCase() === "system-dev" ? "system-dev" : "bundled";
  const proxyRaw = answers.proxy?.trim().toLowerCase();
  const useProxy = proxyRaw === "yes" || proxyRaw === "y";
  const hostTarget = /** @type {HostTarget} */ (
    HOST_TARGET_IDS.includes(answers.hostTarget?.trim().toLowerCase())
      ? answers.hostTarget.trim().toLowerCase()
      : "cursor"
  );

  return {
    occamHome: answers.occamHome?.trim() ?? "",
    hostTarget,
    browser,
    useProxy,
    profile,
  };
}

/**
 * @param {import("node:readline/promises").Interface} rl
 * @param {OnboardStepDef} step
 * @param {number} index 1-based
 * @param {number} total
 * @param {string} defaultValue
 */
export async function promptStep(rl, step, index, total, defaultValue) {
  const prompt = renderStepPrompt(step.id, index, total, step.label, defaultValue);
  const raw = await rl.question(prompt);
  const value = raw.trim() || defaultValue;
  const err = step.validate(value);
  if (err) {
    throw new Error(err);
  }

  return value;
}

export async function collectInteractiveAnswers(defaults, options = {}) {
  if (!options.skipWelcome) {
    console.log(renderOnboardWelcome());
  }

  const rl = createInterface({ input, output });
  /** @type {Record<string, string>} */
  const answers = { ...defaults };
  const skip = new Set(options.skipSteps || []);
  // Installer already knows OCCAM_HOME — never re-ask.
  if (defaults.occamHome?.trim()) {
    skip.add("occamHome");
    answers.occamHome = defaults.occamHome.trim();
  }
  const steps = STEP_DEFS.filter((s) => !skip.has(s.id));
  const total = steps.length;

  try {
    for (let i = 0; i < steps.length; i++) {
      const step = steps[i];
      const def = defaults[step.id] ?? "";
      answers[step.id] = await promptStep(rl, step, i + 1, total, def);
    }
  } finally {
    rl.close();
  }

  return answers;
}
