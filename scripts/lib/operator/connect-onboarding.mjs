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
  FIRST_SUCCESS_URL,
} from "./install-ux.mjs";
import { canPromptInteractively, askControllingTty, isControllingTtyError } from "./tty.mjs";

/**
 * Explicit automation opt-in to mutate every detected Tier-A host without a prompt.
 * @param {NodeJS.ProcessEnv} [env]
 */
export function allowConnectAll(env = process.env) {
  return env.OCCAM_CONNECT_ALL === "1" || env.OCCAM_CONNECT_ALL === "true";
}

/**
 * Parse `OCCAM_CONNECT_ONLY` — comma/space-separated host adapter ids (opt-in single-or-few).
 * Does not replace `OCCAM_HOST` (legacy snippet only). Empty / unset → null.
 * @param {NodeJS.ProcessEnv} [env]
 * @returns {string[]|null}
 */
export function parseConnectOnlyIds(env = process.env) {
  const raw = env.OCCAM_CONNECT_ONLY?.trim();
  if (!raw) return null;
  const ids = raw
    .split(/[,;\s]+/)
    .map((s) => s.trim())
    .filter(Boolean);
  return ids.length ? ids : null;
}

/**
 * Match requested connect-only ids against Tier-A candidates (case-insensitive id).
 * @param {Array<{ id: string, name: string }>} candidates
 * @param {string[]} requested
 * @returns {{ matched: string[], missing: string[] }}
 */
export function matchConnectOnlyCandidates(candidates, requested) {
  /** @type {string[]} */
  const matched = [];
  /** @type {string[]} */
  const missing = [];
  for (const req of requested) {
    const hit = candidates.find((c) => c.id.toLowerCase() === req.toLowerCase());
    if (hit) {
      if (!matched.includes(hit.id)) matched.push(hit.id);
    } else {
      missing.push(req);
    }
  }
  return { matched, missing };
}

const MULTI_HOST_AUTOMATION_HINT =
  "Automation: OCCAM_CONNECT_ONLY=<id> (one host) or OCCAM_CONNECT_ALL=1 (all Tier-A)";

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
  const headline = opts.cancelled
    ? "Cancelled."
    : source === "install"
      ? "Occam is installed."
      : detail.split("\n")[0] || "No AI app configurations were changed.";

  /** @type {string[]} */
  const lines = [];
  if (opts.cancelled) {
    lines.push("Cancelled.");
    lines.push("");
    lines.push("No AI app configurations were changed.");
    if (source === "install") {
      lines.push("");
      lines.push("When you're ready:");
      lines.push("  occam connect");
    }
  } else if (source === "install") {
    lines.push("Occam is installed.");
    lines.push("");
    lines.push(...detail.split("\n"));
  } else {
    // Print detail once — do not echo the first line twice.
    lines.push(...detail.split("\n"));
  }
  lines.push("");
  lines.push("First success guide:");
  lines.push(FIRST_SUCCESS_URL);

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
  // curl|bash: stdin is the script pipe — read the controlling terminal synchronously.
  if (!input.isTTY) {
    try {
      return askControllingTty(prompt);
    } catch (err) {
      if (isControllingTtyError(err)) {
        throw Object.assign(new Error("Interactive terminal unavailable. No AI app configurations were changed."), {
          code: "ERR_TTY_UNAVAILABLE",
          cause: err,
        });
      }
      throw err;
    }
  }
  const rl = createInterface({ input, output });
  try {
    return await rl.question(prompt);
  } finally {
    try {
      rl.close();
    } catch {
      /* ignore */
    }
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
 *   connectOnly?: string[],
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
  // Explicit boolean wins; otherwise stdio TTY or controlling /dev/tty (curl|bash).
  const interactive =
    typeof opts.interactive === "boolean"
      ? opts.interactive
      : typeof opts.askQuestion === "function" || canPromptInteractively({ env: opts.env ?? process.env });
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
      "First success guide:",
      FIRST_SUCCESS_URL,
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
    const ollama = runtimes.some(
      (r) => r.id === "ollama" || /ollama/i.test(r.name || ""),
    );
    /** @type {string[]} */
    const zeroLines = [
      detectedAll.length
        ? "Occam found AI tools, but none are ready for automatic connection yet."
        : "No compatible AI apps were found.",
      "",
      "Occam is installed, but it is not connected to an AI app yet.",
      "No AI app configurations were changed.",
      "",
      "What to do now:",
    ];
    if (ollama) {
      zeroLines.push("Ollama is installed. To use a local model with Occam:");
      zeroLines.push("  occam chat");
      zeroLines.push("");
      zeroLines.push("Or connect a supported chat app later:");
      zeroLines.push("  occam connect");
    } else {
      zeroLines.push("1. Install a supported AI app, then run: occam connect");
      zeroLines.push("2. Or follow the first-success guide below.");
    }
    zeroLines.push("");
    zeroLines.push("First success guide:");
    zeroLines.push(FIRST_SUCCESS_URL);
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
      connectReport: {
        hosts: detectedAll.map((h) => ({ ...h, detected: true })),
        connections: [],
        runtimes,
        ready: false,
        status: "Installed",
        message: "no connectable host",
      },
      outcome: resolveInstallOutcome({ installOk: true, skippedConnect: true }),
      transcript: zeroLines.join("\n"),
      only: [],
      skipped: true,
      mutated: false,
    };
  }

  /** @type {string[]|null} */
  let only = Array.isArray(opts.only) && opts.only.length ? [...opts.only] : null;

  // OCCAM_CONNECT_ONLY — explicit opt-in for one (or few) hosts; same consent as --only.
  // OCCAM_CONNECT_ALL wins when both are set (connect every Tier-A candidate).
  if (!only) {
    const fromOpts =
      Array.isArray(opts.connectOnly) && opts.connectOnly.length ? opts.connectOnly : null;
    const fromEnv = parseConnectOnlyIds(env);
    const requested = fromOpts || fromEnv;
    if (requested?.length) {
      if (connectAll) {
        emit(
          "[occam.connect] OCCAM_CONNECT_ALL=1 takes precedence; ignoring OCCAM_CONNECT_ONLY",
        );
      } else {
        const { matched, missing } = matchConnectOnlyCandidates(candidates, requested);
        if (matched.length === 0) {
          const found = candidates.map((c) => c.id).join(", ") || "(none)";
          return skippedResult({
            detected: candidates,
            source,
            detail:
              `OCCAM_CONNECT_ONLY=${requested.join(",")} — none of those hosts were detected among Tier-A candidates (${found}).\n` +
              `No AI app configurations were changed.\n\n` +
              (source === "install"
                ? "When you're ready:\n  occam connect\n  # or: occam connect --only <id>"
                : "Re-run: occam connect --only <id>"),
          });
        }
        if (missing.length) {
          emit(
            `[occam.connect] OCCAM_CONNECT_ONLY: not detected (skipped): ${missing.join(", ")}`,
          );
        }
        const names = candidates.filter((c) => matched.includes(c.id)).map((c) => c.name);
        emit("");
        emit(
          names.length === 1
            ? `OCCAM_CONNECT_ONLY → ${names[0]}.`
            : `OCCAM_CONNECT_ONLY → ${names.join(", ")}.`,
        );
        emit(`Connecting Occam to ${names.join(", ")}...`);
        only = matched;
      }
    }
  }

  // Explicit --only / CONNECT_ONLY bypasses consent (operator already chose).
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
      // Non-interactive multi-host without ALL / ONLY — never mutate.
      return skippedResult({
        detected: candidates,
        source,
        detail:
          source === "install"
            ? `Multiple AI apps detected.\nNo AI app configurations were changed.\n\nWhen you're ready:\n  occam connect\n\n${MULTI_HOST_AUTOMATION_HINT}`
            : `Multiple AI apps detected.\nNo AI app configurations were changed.\n\nRe-run interactively, or:\n  ${MULTI_HOST_AUTOMATION_HINT}`,
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
