#!/usr/bin/env node
/**
 * Install-time connect orchestration — reuses the connect platform engine.
 * detect → (optional choose) → runConnect → quiet Ready semantics.
 *
 * Multi-host safety:
 *   1 host  → auto-connect (acceptable)
 *   N hosts → never silently mutate all; interactive confirm or OCCAM_CONNECT_ALL=1
 */
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import {
  listHostAdapters,
  runConnect,
  resolveConnectMode,
  selectAutoConnectAdapters,
} from "./connect/index.mjs";
import {
  parseHostChoice,
  parseYesNoDefaultYes,
  renderHostChoiceMenu,
  renderInstallConnectSection,
  resolveInstallOutcome,
} from "./install-ux.mjs";

/**
 * Detected hosts eligible for auto-connect (Tier A, medium+ confidence).
 * @param {string} occamHome
 */
export function listInstallConnectCandidates(occamHome) {
  const adapters = listHostAdapters({ occamHome });
  const auto = selectAutoConnectAdapters(adapters, { explicit: false });
  return auto
    .map((a) => {
      const d = a.detect();
      return {
        id: a.id,
        name: a.name,
        confidence: d.confidence,
        detected: d.detected === true,
      };
    })
    .filter((h) => h.detected);
}

/**
 * All detected hosts (any tier) for display — human names only.
 * @param {string} occamHome
 */
export function listDetectedHosts(occamHome) {
  const adapters = listHostAdapters({ occamHome });
  /** @type {Array<{ id: string, name: string, confidence: string }>} */
  const out = [];
  for (const a of adapters) {
    const d = a.detect();
    if (d.detected) {
      out.push({ id: a.id, name: a.name, confidence: d.confidence || "low" });
    }
  }
  return out;
}

/**
 * Explicit automation opt-in to mutate every detected Tier-A host without a prompt.
 * @param {NodeJS.ProcessEnv} [env]
 */
export function allowConnectAll(env = process.env) {
  return env.OCCAM_CONNECT_ALL === "1" || env.OCCAM_CONNECT_ALL === "true";
}

/**
 * @param {Array<{ name: string }>} hosts
 */
export function renderMultiHostConfirmPrompt(hosts) {
  const names = hosts.map((h) => h.name);
  let list;
  if (names.length === 2) list = `${names[0]} and ${names[1]}`;
  else list = `${names.slice(0, -1).join(", ")}, and ${names[names.length - 1]}`;
  return `Connect Occam to ${list}? [Y/n] `;
}

/**
 * @param {{
 *   detected: Array<{ id: string, name: string }>,
 *   detail?: string,
 * }} opts
 */
function skippedMultiHostResult(opts) {
  const outcome = resolveInstallOutcome({
    installOk: true,
    skippedConnect: true,
    connectReport: {
      hosts: opts.detected.map((h) => ({ ...h, detected: true })),
      connections: [],
    },
  });
  const detail =
    opts.detail ||
    "Multiple AI apps detected — re-run with OCCAM_SETUP=manual, or set OCCAM_CONNECT_ALL=1 for automation.";
  const text = [
    "Connecting to your AI app",
    "",
    "Detected:",
    ...opts.detected.map((h) => `  ${h.name}`),
    "",
    outcome.headline,
    detail,
    "",
    "Documentation:",
    "https://contextforgeai.github.io/occam/",
  ].join("\n");
  return {
    connectReport: null,
    outcome: { ...outcome, detail },
    transcript: text,
    only: [],
    skipped: true,
  };
}

/**
 * @param {{
 *   occamHome: string,
 *   setupMode?: "auto"|"manual",
 *   verbose?: boolean,
 *   interactive?: boolean,
 *   askQuestion?: (prompt: string) => Promise<string>,
 *   forceConnect?: boolean,
 *   skipOccamVerify?: boolean,
 *   connectAll?: boolean,
 *   env?: NodeJS.ProcessEnv,
 * }} opts
 */
export async function runInstallConnectFlow(opts) {
  const occamHome = opts.occamHome;
  const setupMode = opts.setupMode === "manual" ? "manual" : "auto";
  const interactive = opts.interactive === true;
  const env = opts.env ?? process.env;
  const connectAll = opts.connectAll === true || allowConnectAll(env);
  const detectedAll = listDetectedHosts(occamHome);
  const candidates = listInstallConnectCandidates(occamHome);

  /** @type {string[]|"skip"|null} */
  let only = null;

  if (candidates.length === 0) {
    const outcome = resolveInstallOutcome({
      installOk: true,
      connectReport: {
        hosts: detectedAll.map((h) => ({ ...h, detected: true })),
        connections: [],
      },
    });
    const text = renderInstallConnectSection({
      detected: detectedAll,
      connectReport: null,
      outcome,
    });
    return {
      connectReport: null,
      outcome,
      transcript: text,
      only: [],
      skipped: true,
    };
  }

  // ---- Manual: always prefer an explicit host choice when interactive ----
  if (setupMode === "manual") {
    if (interactive || opts.askQuestion) {
      const menu = renderHostChoiceMenu(candidates);
      let choice = null;
      if (opts.askQuestion) {
        choice = parseHostChoice(await opts.askQuestion(`${menu}? `), candidates);
      } else {
        const rl = createInterface({ input, output });
        try {
          choice = parseHostChoice(await rl.question(`${menu}? `), candidates);
        } finally {
          rl.close();
        }
      }
      if (choice === "skip" || choice === null) {
        return skippedMultiHostResult({
          detected: detectedAll,
          detail: "Skipped host connection. Run `occam connect` when ready.",
        });
      }
      only = choice === "all" ? candidates.map((c) => c.id) : choice;
    } else if (connectAll) {
      only = candidates.map((c) => c.id);
    } else if (candidates.length === 1) {
      only = [candidates[0].id];
    } else {
      return skippedMultiHostResult({ detected: detectedAll });
    }
  } else if (candidates.length === 1) {
    // Auto + exactly one host → connect (no prompt required).
    only = [candidates[0].id];
  } else if (connectAll) {
    // Explicit automation opt-in.
    only = candidates.map((c) => c.id);
  } else if (interactive || opts.askQuestion) {
    // Auto + multiple + TTY → confirm before mutating every config.
    const prompt = renderMultiHostConfirmPrompt(candidates);
    let yes = true;
    if (opts.askQuestion) {
      const parsed = parseYesNoDefaultYes(await opts.askQuestion(prompt));
      yes = parsed !== false;
    } else {
      const rl = createInterface({ input, output });
      try {
        const parsed = parseYesNoDefaultYes(await rl.question(prompt));
        yes = parsed !== false;
      } finally {
        rl.close();
      }
    }
    if (!yes) {
      // Offer the compact menu as a second step.
      const menu = renderHostChoiceMenu(candidates);
      let choice = null;
      if (opts.askQuestion) {
        choice = parseHostChoice(await opts.askQuestion(`${menu}? `), candidates);
      } else {
        const rl = createInterface({ input, output });
        try {
          choice = parseHostChoice(await rl.question(`${menu}? `), candidates);
        } finally {
          rl.close();
        }
      }
      if (choice === "skip" || choice === null) {
        return skippedMultiHostResult({
          detected: detectedAll,
          detail: "Skipped host connection. Run `occam connect` when ready.",
        });
      }
      only = choice === "all" ? candidates.map((c) => c.id) : choice;
    } else {
      only = candidates.map((c) => c.id);
    }
  } else {
    // Non-interactive auto with multiple hosts and no OCCAM_CONNECT_ALL → do not mutate all.
    return skippedMultiHostResult({ detected: detectedAll });
  }

  const connectMode = resolveConnectMode(env);
  const connectReport = await runConnect({
    occamHome,
    connectMode: opts.forceConnect
      ? { mode: "auto", mutateHosts: true, reason: "install force" }
      : connectMode,
    only,
    force: opts.forceConnect === true,
    skipOccamVerify: opts.skipOccamVerify === true,
  });

  const outcome = resolveInstallOutcome({ installOk: true, connectReport });
  return {
    connectReport,
    outcome,
    transcript: renderInstallConnectSection({
      detected: detectedAll,
      connectReport,
      outcome,
    }),
    only,
    skipped: false,
  };
}
