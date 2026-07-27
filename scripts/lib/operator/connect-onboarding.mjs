#!/usr/bin/env node
/**
 * Shared first-run / reconnect onboarding — used by:
 *   - post-install-ux.mjs (one-line install)
 *   - occam-connect.mjs (standalone `occam connect`)
 *
 * Flow: discover (read-only) → plan → consent → connect → human summary.
 * Mutation never happens before consent when multiple registration targets exist.
 */
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import {
  listHostAdapters,
  runConnect,
  resolveConnectMode,
  selectAutoConnectAdapters,
  detectAllRuntimes,
  renderConnectTranscript,
  renderHumanConnectSummary,
} from "./connect/index.mjs";
import {
  parseHostChoice,
  parseYesNoDefaultNo,
  renderHostChoiceMenu,
  renderConnectPlan,
  renderDiscoverySection,
  resolveInstallOutcome,
  okLine,
  progressLine,
  DOCS_URL,
} from "./install-ux.mjs";
import { canPromptInteractively, openControllingTty } from "./tty.mjs";

/**
 * Explicit automation opt-in to mutate every detected Tier-A host without a prompt.
 * @param {NodeJS.ProcessEnv} [env]
 */
export function allowConnectAll(env = process.env) {
  return env.OCCAM_CONNECT_ALL === "1" || env.OCCAM_CONNECT_ALL === "true";
}

/**
 * Registration targets eligible for auto-connect (Tier A, medium+).
 * @param {string} occamHome
 */
export function listConnectCandidates(occamHome) {
  const adapters = listHostAdapters({ occamHome });
  return selectAutoConnectAdapters(adapters, { explicit: false })
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
 * Detected MCP hosts (any tier) — for display / diagnostics.
 * @param {string} occamHome
 */
export function listDetectedHosts(occamHome) {
  const adapters = listHostAdapters({ occamHome });
  /** @type {Array<{ id: string, name: string, confidence: string, connectionMethod?: string, supportTier?: string }>} */
  const out = [];
  for (const a of adapters) {
    const d = a.detect();
    if (d.detected) {
      out.push({
        id: a.id,
        name: a.name,
        confidence: d.confidence || "low",
        connectionMethod: a.connectionMethod,
        supportTier: a.supportTier,
      });
    }
  }
  return out;
}

/**
 * @param {Array<{ name: string }>} hosts
 */
export function renderMultiHostConfirmPrompt(hosts) {
  const n = hosts.length;
  return `Connect Occam to all ${n} apps? [y/N] `;
}

/**
 * @param {{
 *   detected: Array<{ id: string, name: string }>,
 *   detail?: string,
 *   cancelled?: boolean,
 * }} opts
 */
function skippedResult(opts) {
  const source = opts.source || "connect";
  const detail =
    opts.detail ||
    (opts.cancelled
      ? "Cancelled.\nNo AI app configurations were changed."
      : source === "install"
        ? "No AI app configurations were changed.\n\nWhen you're ready:\n  occam connect"
        : "No AI app configurations were changed.");
  const outcome = resolveInstallOutcome({
    installOk: true,
    skippedConnect: true,
    connectReport: {
      hosts: opts.detected.map((h) => ({ ...h, detected: true })),
      connections: [],
    },
  });
  const headline = source === "install" ? "Occam is installed." : "Cancelled.";
  const lines = [
    opts.cancelled && source !== "install" ? "Cancelled." : source === "install" ? "Occam is installed." : detail.split("\n")[0],
    "",
    ...(opts.cancelled && source !== "install"
      ? ["No AI app configurations were changed."]
      : detail.split("\n").filter((l, i) => !(i === 0 && l === "Cancelled."))),
    "",
    "Documentation:",
    DOCS_URL,
  ];
  // De-dupe empty noise
  const cleaned = lines.join("\n").replace(/\n{3,}/g, "\n\n").trim() + "\n";
  return {
    connectReport: null,
    outcome: { ...outcome, detail, headline },
    transcript: cleaned,
    only: [],
    skipped: true,
    cancelled: opts.cancelled === true,
    mutated: false,
  };
}

/**
 * @param {string} prompt
 * @param {{ askQuestion?: (p: string) => Promise<string>, emit?: (line: string) => void }} opts
 */
async function ask(prompt, opts) {
  if (opts.askQuestion) {
    // Injected askers do not print the prompt — surface it for transcripts/tests.
    if (typeof opts.emit === "function") {
      opts.emit(String(prompt).replace(/\s+$/, ""));
    }
    return opts.askQuestion(prompt);
  }
  // curl|bash: stdin is the script pipe — prefer the controlling terminal.
  const tty = input.isTTY ? null : openControllingTty();
  const rl = createInterface({
    input: tty?.input || input,
    output: tty?.output || output,
  });
  try {
    return await rl.question(prompt);
  } finally {
    rl.close();
    tty?.close();
  }
}

/**
 * Shared onboarding orchestration for install + `occam connect`.
 *
 * @param {{
 *   occamHome: string,
 *   setupMode?: "auto"|"manual",
 *   verbose?: boolean,
 *   interactive?: boolean,
 *   askQuestion?: (prompt: string) => Promise<string>,
 *   forceConnect?: boolean,
 *   skipOccamVerify?: boolean,
 *   connectAll?: boolean,
 *   detectOnly?: boolean,
 *   only?: string[],
 *   env?: NodeJS.ProcessEnv,
 *   emit?: (line: string) => void,
 *   source?: "install"|"connect",
 * }} opts
 */
export async function runConnectOnboarding(opts) {
  const occamHome = opts.occamHome;
  const setupMode = opts.setupMode === "manual" ? "manual" : "auto";
  // Prefer explicit flag; otherwise detect stdio TTY or controlling /dev/tty (curl|bash).
  const interactive =
    opts.interactive === true ||
    (opts.interactive !== false &&
      (typeof opts.askQuestion === "function" || canPromptInteractively()));
  const verbose = opts.verbose === true;
  const env = opts.env ?? process.env;
  const connectAll = opts.connectAll === true || allowConnectAll(env);
  const emit = opts.emit || ((line) => console.log(line));
  const source = opts.source || "connect";

  const candidates = listConnectCandidates(occamHome);
  const detectedAll = listDetectedHosts(occamHome);
  const runtimes = detectAllRuntimes().filter((r) => r.detected !== false);

  // ---- Read-only discovery (never mutates) ----
  emit("");
  emit(renderDiscoverySection({ candidates, runtimes, verbose }));

  if (opts.detectOnly) {
    const outcome = resolveInstallOutcome({
      installOk: true,
      skippedConnect: true,
      connectReport: {
        hosts: detectedAll.map((h) => ({ ...h, detected: true })),
        connections: [],
      },
    });
    /** @type {string[]} */
    const lines = [
      "Detection only — no AI app configurations were changed.",
      "",
      "Documentation:",
      DOCS_URL,
    ];
    if (verbose) {
      const detectReport = await runConnect({
        occamHome,
        connectMode: { mode: "detect-only", mutateHosts: false, reason: "--detect-only" },
        skipOccamVerify: true,
      });
      lines.unshift(renderConnectTranscript(detectReport, { verbose: true }), "");
    }
    return {
      connectReport: null,
      outcome,
      transcript: lines.join("\n"),
      only: [],
      skipped: true,
      mutated: false,
    };
  }

  if (candidates.length === 0) {
    /** @type {string[]} */
    const zeroLines = [
      detectedAll.length
        ? "Occam found AI tools, but none are ready for automatic connection yet."
        : "No compatible AI apps were found.",
      "",
      "Occam is installed.",
      "No AI app configurations were changed.",
      "",
      "When you're ready:",
      "  occam connect",
      "",
      "Documentation:",
      DOCS_URL,
    ];
    if (verbose && detectedAll.length) {
      zeroLines.splice(
        1,
        0,
        "",
        "Detected (not auto-connected):",
        ...detectedAll.map((h) => `  ${h.name}`),
      );
    }
    return {
      connectReport: null,
      outcome: resolveInstallOutcome({ installOk: true, skippedConnect: true }),
      transcript: zeroLines.join("\n"),
      only: [],
      skipped: true,
      mutated: false,
    };
  }

  /** @type {string[]|null} */
  let only = Array.isArray(opts.only) && opts.only.length ? [...opts.only] : null;

  // Explicit --only bypasses consent (operator already chose).
  if (!only) {
    if (setupMode === "manual" && (interactive || opts.askQuestion)) {
      emit("");
      emit(renderConnectPlan(candidates));
      emit("");
      emit(renderHostChoiceMenu(candidates));
      const choice = parseHostChoice(
        await ask("Enter numbers separated by commas, or q to cancel: ", opts),
        candidates,
      );
      if (choice === "skip" || choice === null) {
        return skippedResult({
          detected: candidates,
          cancelled: true,
          source,
          detail: "Cancelled.\nNo AI app configurations were changed.",
        });
      }
      only = choice === "all" ? candidates.map((c) => c.id) : choice;
    } else if (candidates.length === 1 && setupMode === "auto") {
      emit("");
      emit(`Found ${candidates[0].name}.`);
      emit(`Connecting Occam to ${candidates[0].name}...`);
      only = [candidates[0].id];
    } else if (connectAll) {
      only = candidates.map((c) => c.id);
    } else if (interactive || opts.askQuestion) {
      emit("");
      emit(renderConnectPlan(candidates));
      emit("");
      const prompt = renderMultiHostConfirmPrompt(candidates);
      const parsed = parseYesNoDefaultNo(await ask(prompt, opts));
      const yes = parsed === true;
      if (!yes) {
        emit("");
        emit(renderHostChoiceMenu(candidates));
        const choice = parseHostChoice(
          await ask("Enter numbers separated by commas, or q to cancel: ", opts),
          candidates,
        );
        if (choice === "skip" || choice === null) {
          return skippedResult({
            detected: candidates,
            cancelled: true,
            source,
            detail:
              source === "install"
                ? "Cancelled.\nNo AI app configurations were changed.\n\nWhen you're ready:\n  occam connect"
                : "Cancelled.\nNo AI app configurations were changed.",
          });
        }
        only = choice === "all" ? candidates.map((c) => c.id) : choice;
      } else {
        only = candidates.map((c) => c.id);
      }
    } else if (setupMode === "manual" && candidates.length === 1) {
      only = [candidates[0].id];
    } else if (setupMode === "manual" && connectAll) {
      only = candidates.map((c) => c.id);
    } else {
      // Non-interactive multi-host without OCCAM_CONNECT_ALL — never mutate.
      return skippedResult({
        detected: candidates,
        source,
        detail:
          source === "install"
            ? "Multiple AI apps detected.\nNo AI app configurations were changed.\n\nWhen you're ready:\n  occam connect\n\nAutomation: set OCCAM_CONNECT_ALL=1"
            : "Multiple AI apps detected.\nNo AI app configurations were changed.\n\nRe-run interactively, or set OCCAM_CONNECT_ALL=1",
      });
    }
  }

  if (!only || only.length === 0) {
    return skippedResult({ detected: candidates, cancelled: true, source });
  }

  const selectedNames = candidates.filter((c) => only.includes(c.id)).map((c) => c.name);
  emit("");
  emit("Connecting Occam...");

  const connectMode = resolveConnectMode(env);
  let verifyBannerShown = false;
  const connectReport = await runConnect({
    occamHome,
    connectMode: opts.forceConnect
      ? { mode: "auto", mutateHosts: true, reason: "force connect" }
      : connectMode,
    only,
    force: opts.forceConnect === true,
    skipOccamVerify: opts.skipOccamVerify === true,
    onProgress: (ev) => {
      if (!ev || verbose) return;
      if (ev.phase === "configure-start") {
        emit(progressLine(`Checking ${ev.name} configuration…`));
      }
      if (ev.phase === "configure-done") {
        if (ev.ok === false) emit(`✗ ${ev.name} — ${ev.message || "failed"}`);
        else if (ev.already) emit(okLine(`${ev.name} — already connected`));
        else emit(okLine(`${ev.name} configured`));
      }
      if (ev.phase === "verify-start") {
        // Occam self-check runs before host configure; keep "Verifying..." for host checks.
        if (ev.name === "Occam") return;
        if (!verifyBannerShown) {
          emit("");
          emit("Verifying...");
          verifyBannerShown = true;
        }
        emit(progressLine(`Checking ${ev.name}…`));
      }
      if (ev.phase === "verify-done" && ev.name !== "Occam") {
        if (ev.restart) emit(`↻ ${ev.name} — restart required`);
        else if (ev.action) emit(`! ${ev.name} — ${ev.message || "needs your action"}`);
        else if (ev.configured) emit(okLine(`${ev.name} — configured`));
        else if (ev.ok) emit(okLine(`${ev.name} verified`));
        else emit(`! ${ev.name} — ${ev.message || "needs attention"}`);
      }
    },
  });

  const outcome = resolveInstallOutcome({ installOk: true, connectReport });
  const human = renderHumanConnectSummary(connectReport, {
    selectedNames,
    source,
  });
  /** @type {string[]} */
  const parts = [human];
  if (verbose) {
    parts.push("", "── verbose ──", renderConnectTranscript(connectReport, { verbose: true }));
  }

  return {
    connectReport,
    outcome,
    transcript: parts.join("\n"),
    only,
    skipped: false,
    mutated: true,
  };
}

/** @deprecated Prefer listConnectCandidates */
export const listInstallConnectCandidates = listConnectCandidates;
