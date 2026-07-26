#!/usr/bin/env node
/**
 * Shared install UX helpers — quiet default, verbose internals, Ready semantics.
 * Presentation only: does not weaken doctor / smoke / connect checks.
 */

/** Strings that must NOT appear in a successful default (quiet) install transcript. */
export const FORBIDDEN_DEFAULT_OUTPUT = Object.freeze([
  "Level B bootstrap",
  "FF-Occam MCP doctor",
  "FF-Occam MCP doctor (L0 skeleton)",
  "host_target: hermes",
  "commit=unknown",
  "MCP host ready",
  "occam onboard",
  "Starting manual onboard wizard",
  "Applying auto setup",
  "private-ip (SSRF guard) module selftest",
  "  ok  127.0.0.1 is private",
  "Warning: Indexing all PDF objects",
  "playwright cache:",
  "Canonical launcher:",
  "Avoid on git clone:",
  "doctor: OK",
  "verify-install: commit=",
]);

export const DOCS_URL = "https://contextforgeai.github.io/occam/";
export const TRY_PROMPT = 'Try asking your agent:\n"Read https://developer.mozilla.org using Occam"';

/**
 * @param {NodeJS.ProcessEnv} [env]
 * @param {string[]} [argv]
 */
export function isInstallVerbose(env = process.env, argv = process.argv) {
  if (env.OCCAM_VERBOSE === "1" || env.OCCAM_VERBOSE === "true") return true;
  if (env.OCCAM_DEBUG === "1" || env.OCCAM_DEBUG === "true") return true;
  if (argv.includes("--verbose") || argv.includes("--debug")) return true;
  return false;
}

/**
 * Quiet is the default for install orchestration unless verbose/debug is set.
 * OCCAM_INSTALL_QUIET=0 forces verbose-style child output.
 * @param {NodeJS.ProcessEnv} [env]
 * @param {string[]} [argv]
 */
export function isInstallQuiet(env = process.env, argv = process.argv) {
  if (isInstallVerbose(env, argv)) return false;
  if (env.OCCAM_INSTALL_QUIET === "0" || env.OCCAM_INSTALL_QUIET === "false") return false;
  return true;
}

/**
 * @param {string} [version]
 */
export function renderProductHeader(version) {
  const v = (version || "").trim();
  return v ? `Occam ${v}` : "Occam";
}

/**
 * Whether install/welcome banners may emit ANSI.
 * Aligns with get-ff-occam.sh (`OCCAM_NO_COLOR`) and the NO_COLOR convention.
 * Palette stays the same 256-color rows used by get-install-welcome / OccamStderrAnsiSink.
 *
 * @param {{ isTTY?: boolean | undefined }} [stream]
 * @param {NodeJS.ProcessEnv} [env]
 */
export function shouldUseInstallColor(stream = process.stdout, env = process.env) {
  if (env.NO_COLOR != null && env.NO_COLOR !== "") return false;
  if (env.OCCAM_NO_COLOR === "1" || env.OCCAM_NO_COLOR === "true") return false;
  return stream?.isTTY === true;
}

export function renderInstallingHeader() {
  return "Installing Occam";
}

export function renderConnectingHeader() {
  return "Connecting to your AI app";
}

/** @param {string} message */
export function okLine(message) {
  return `✓ ${message}`;
}

/** @param {string} message */
export function warnLine(message) {
  return `⚠ ${message}`;
}

/** @param {string} message */
export function failLine(message) {
  return `✗ ${message}`;
}

/** Active-state line during long install/connect steps (quiet-safe). */
export function progressLine(message) {
  return `  ${message}`;
}

/**
 * Read-only discovery block — registration targets only by default.
 * @param {{
 *   candidates: Array<{ name: string }>,
 *   runtimes?: Array<{ name: string }>,
 *   verbose?: boolean,
 * }} opts
 */
export function renderDiscoverySection(opts) {
  const lines = ["Looking for AI apps..."];
  if (!opts.candidates.length) {
    lines.push("· None found for automatic connection");
  } else {
    for (const h of opts.candidates) {
      lines.push(okLine(h.name));
    }
    lines.push("");
    lines.push(
      opts.candidates.length === 1
        ? "Occam can connect to 1 app."
        : `Occam can connect to ${opts.candidates.length} apps.`,
    );
  }
  if (opts.verbose && opts.runtimes?.length) {
    lines.push("");
    lines.push("Related tools (not MCP registration targets):");
    for (const r of opts.runtimes) {
      lines.push(`  · ${r.name}`);
    }
  }
  return lines.join("\n");
}

/**
 * Pre-consent plan — only promise what connect actually does.
 * @param {Array<{ name: string }>} hosts
 */
export function renderConnectPlan(hosts) {
  const n = hosts.length;
  return [
    n === 1
      ? "Occam found 1 AI app it can configure."
      : `Occam found ${n} AI apps it can configure.`,
    "",
    "What will happen:",
    "• Occam will add or update its connection.",
    "• Existing configurations will be preserved when already correct.",
    "• Backups are created before config-file changes.",
    "• Some apps may need to be restarted afterward.",
    "",
    "No AI app configurations have been changed yet.",
  ].join("\n");
}

/**
 * @param {string} text
 * @returns {string[]}
 */
export function findForbiddenDefaultOutput(text) {
  return FORBIDDEN_DEFAULT_OUTPUT.filter((s) => text.includes(s));
}

/**
 * @param {string} text
 */
export function assertQuietTranscript(text) {
  const hits = findForbiddenDefaultOutput(text);
  if (hits.length) {
    throw new Error(`quiet install transcript leaked internals:\n  - ${hits.join("\n  - ")}`);
  }
}

/**
 * Public Ready / install outcome labels (user-facing).
 * @typedef {"INSTALLED"|"CONNECTED"|"READY"|"ACTION_REQUIRED"|"ALMOST_READY"|"FAILED"} InstallReadyState
 *
 * @param {{
 *   installOk: boolean,
 *   connectReport?: {
 *     ready?: boolean,
 *     status?: string,
 *     message?: string,
 *     hosts?: Array<{ detected?: boolean, name?: string }>,
 *     connections?: Array<{
 *       name?: string,
 *       apply?: { ok?: boolean, applied?: boolean, action?: string },
 *       readyState?: { ready?: boolean, requiresRestart?: boolean, requiresUserAction?: boolean, hostBlocked?: boolean, status?: string, message?: string },
 *     }>,
 *   } | null,
 *   skippedConnect?: boolean,
 * }} input
 */
export function resolveInstallOutcome(input) {
  if (!input.installOk) {
    return {
      state: /** @type {InstallReadyState} */ ("FAILED"),
      ready: false,
      headline: "Install failed.",
      detail: "",
    };
  }

  const report = input.connectReport;
  if (!report || input.skippedConnect) {
    return {
      state: /** @type {InstallReadyState} */ ("INSTALLED"),
      ready: false,
      headline: "Occam is installed.",
      detail: "Connect an AI app later with: occam connect",
    };
  }

  const detected = (report.hosts || []).filter((h) => h.detected);
  const connections = report.connections || [];
  const mutated = connections.some(
    (c) => c.apply && (c.apply.applied === true || c.apply.action === "noop" || c.apply.ok === true),
  );

  if (detected.length === 0 && connections.length === 0) {
    return {
      state: /** @type {InstallReadyState} */ ("INSTALLED"),
      ready: false,
      headline: "Occam is installed, but no supported AI app was detected.",
      detail:
        "Choose a host manually with `occam connect`, print a snippet with `occam connect --detect-only`, or finish without connecting.",
    };
  }

  if (report.ready === true || report.status === "Ready") {
    return {
      state: /** @type {InstallReadyState} */ ("READY"),
      ready: true,
      headline: "Ready.",
      detail: "",
    };
  }

  if (report.status === "Action required" || /action required/i.test(report.status || "")) {
    return {
      state: /** @type {InstallReadyState} */ ("ACTION_REQUIRED"),
      ready: false,
      headline: "Action required.",
      detail: report.message || "Configuration is valid but the host needs a trust or permission step.",
    };
  }

  if (report.status === "Almost ready" || /restart required/i.test(report.status || "")) {
    return {
      state: /** @type {InstallReadyState} */ ("ALMOST_READY"),
      ready: false,
      headline: "Almost ready.",
      detail: report.message || "Restart the named AI app to activate Occam.",
    };
  }

  if (mutated) {
    // Prefer a concrete human state over a vague "configured".
    return {
      state: /** @type {InstallReadyState} */ ("ACTION_REQUIRED"),
      ready: false,
      headline: "Action required.",
      detail: report.message || "Some apps need attention before Occam is fully ready.",
    };
  }

  return {
    state: /** @type {InstallReadyState} */ ("INSTALLED"),
    ready: false,
    headline: "Occam is installed.",
    detail: report.message || "No host was connected in this run.",
  };
}

/**
 * Compact connect section for the installer (human names, no Tier A/B).
 * @param {{
 *   detected: Array<{ id: string, name: string }>,
 *   connectReport?: object | null,
 *   outcome: ReturnType<typeof resolveInstallOutcome>,
 * }} opts
 */
export function renderInstallConnectSection(opts) {
  const lines = [];
  lines.push(renderConnectingHeader());
  lines.push("");

  if (opts.detected.length) {
    lines.push("Detected:");
    for (const h of opts.detected) {
      lines.push(`  ${h.name}`);
    }
    lines.push("");
  }

  const report = opts.connectReport;
  if (report?.connections?.length) {
    for (const c of report.connections) {
      if (c.apply?.ok === false) {
        lines.push(failLine(`${c.name} — ${c.apply?.error || "failed"}`));
        continue;
      }
      if (c.apply?.applied) {
        lines.push(okLine(`Occam configured for ${c.name}`));
      } else if (c.apply?.action === "noop") {
        lines.push(okLine(`${c.name} already configured`));
      }
      const restart =
        c.readyState?.requiresRestart === true ||
        /restart required/i.test(c.readyState?.status || "");
      const actionRequired =
        c.readyState?.requiresUserAction === true && c.readyState?.hostBlocked === true;
      if (actionRequired) {
        lines.push(warnLine(`${c.name} — action required`));
      } else if (c.hostVerify?.ok && restart) {
        lines.push(warnLine(`${c.name} — restart required`));
      } else if (c.hostVerify?.ok) {
        lines.push(okLine(`Connection verified (${c.name})`));
      }
    }
    lines.push("");
  }

  lines.push(opts.outcome.headline);
  if (opts.outcome.detail) lines.push(opts.outcome.detail);

  if (opts.outcome.ready) {
    lines.push("");
    lines.push(TRY_PROMPT);
  }

  lines.push("");
  lines.push("Documentation:");
  lines.push(DOCS_URL);
  return lines.join("\n");
}

/**
 * Build the multi-host picker menu text (1-based).
 * @param {Array<{ id: string, name: string }>} hosts
 */
export function renderHostChoiceMenu(hosts) {
  const lines = ["Choose apps to connect:", ""];
  hosts.forEach((h, i) => {
    lines.push(`${i + 1}. ${h.name}`);
  });
  lines.push("");
  return lines.join("\n");
}

/**
 * @param {string} raw
 * @param {Array<{ id: string, name: string }>} hosts
 * @returns {"all"|"skip"|string[]|null} host ids, all, skip, or null if invalid
 */
export function parseHostChoice(raw, hosts) {
  const choice = (raw ?? "").trim().toLowerCase();
  if (!choice || choice === "q" || choice === "quit" || choice === "skip" || choice === "n" || choice === "no") {
    return "skip";
  }
  if (choice === "all" || choice === "a") return "all";

  if (choice.includes(",")) {
    /** @type {string[]} */
    const ids = [];
    for (const part of choice.split(",")) {
      const n = Number(part.trim());
      if (!Number.isInteger(n) || n < 1 || n > hosts.length) return null;
      ids.push(hosts[n - 1].id);
    }
    return [...new Set(ids)];
  }

  const n = Number(choice);
  if (!Number.isInteger(n) || n < 1) return null;
  if (n >= 1 && n <= hosts.length) return [hosts[n - 1].id];
  return null;
}

/**
 * Confirm single-host connect. Empty / y / yes → true.
 * @param {string} raw
 */
export function parseYesNoDefaultYes(raw) {
  const v = (raw ?? "").trim().toLowerCase();
  if (!v || v === "y" || v === "yes") return true;
  if (v === "n" || v === "no") return false;
  return null;
}

/**
 * Multi-host safety confirm. Empty / n / no → false (default NO).
 * @param {string} raw
 */
export function parseYesNoDefaultNo(raw) {
  const v = (raw ?? "").trim().toLowerCase();
  if (!v || v === "n" || v === "no") return false;
  if (v === "y" || v === "yes") return true;
  return null;
}

/**
 * Release / non-git trees should not print commit=unknown.
 * @param {string | null | undefined} commit
 * @param {string | null | undefined} version
 */
export function formatVerifyRevision(commit, version) {
  const c = (commit || "").trim();
  if (c && c !== "unknown") return `commit=${c}`;
  const v = (version || "").trim();
  if (v) return `release=${v}`;
  return "release build";
}
