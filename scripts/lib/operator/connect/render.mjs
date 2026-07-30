import { partitionSupportedHosts } from "./registry.mjs";
import { FIRST_SUCCESS_URL } from "../install-ux.mjs";

/**
 * Wrap names onto lines that fit a normal terminal.
 * @param {string[]} names
 * @param {number} width
 */
function wrapList(names, width) {
  /** @type {string[]} */
  const lines = [];
  let current = "";
  for (const name of names) {
    const next = current ? `${current} · ${name}` : name;
    if (next.length > width && current) {
      lines.push(current);
      current = name;
    } else {
      current = next;
    }
  }
  if (current) lines.push(current);
  return lines;
}

/**
 * Classify a connection row for human outcome groups.
 * @param {object} c
 */
function classifyConnection(c) {
  const rolledBack =
    c.rollback?.ok === true ||
    c.readyState?.rolledBack === true ||
    /was undone|not available in .+ yet/i.test(c.readyState?.message || "");
  const restart =
    c.readyState?.requiresRestart === true ||
    /restart required/i.test(c.readyState?.status || "") ||
    c.hostVerify?.requiresRestart === true;
  const actionRequired =
    (c.readyState?.requiresUserAction === true && c.readyState?.hostBlocked === true) ||
    /action required/i.test(c.readyState?.status || "");
  const already =
    c.apply?.action === "noop" ||
    (c.apply?.ok === true && c.apply?.applied === false && c.apply?.action !== "skip-unmanaged");
  const applyFail = c.apply && c.apply.ok === false && c.apply.action !== "skip-unmanaged";
  const ready = c.readyState?.ready === true;
  const configuredOnly =
    !ready &&
    !restart &&
    !actionRequired &&
    !rolledBack &&
    (c.readyState?.status === "Configured" ||
      (c.hostVerify?.ok === true && c.readyState?.configured === true));

  if (rolledBack) {
    return {
      group: "not_connected",
      line: `• ${c.name}`,
      next: null,
      detail: humanizeError(c.readyState?.message || ""),
    };
  }
  if (applyFail) {
    return {
      group: "action",
      line: `! ${c.name} — ${humanizeError(c.apply?.error || c.readyState?.message || "needs attention")}`,
      next: c.apply?.error || c.readyState?.message || `Fix ${c.name} configuration`,
    };
  }
  if (c.apply?.action === "skip-unmanaged") {
    return {
      group: "action",
      line: `! ${c.name} — existing configuration needs your decision`,
      next: `Review ${c.name} MCP settings, or re-run with --force if Occam should own it`,
    };
  }
  if (actionRequired) {
    return {
      group: "action",
      line: `! ${c.name} — ${humanizeError(c.readyState?.message || "needs your action")}`,
      next: c.readyState?.message || `Complete the action required in ${c.name}`,
    };
  }
  if (restart && (c.hostVerify?.ok || already || c.apply?.ok)) {
    return {
      group: "restart",
      line: `↻ ${c.name}`,
      next: `Restart or reload ${c.name}, open a new chat, then ask: "Use Occam to read https://example.com"`,
    };
  }
  if (ready) {
    return {
      group: "ready",
      line: `✓ ${c.name}`,
      next: `In ${c.name}, open a new chat and ask: "Use Occam to read https://example.com"`,
    };
  }
  if (configuredOnly || already) {
    return {
      group: "configured",
      line: `✓ ${c.name}`,
      next: `Open or reload ${c.name}, start a new chat, then ask: "Use Occam to read https://example.com"`,
    };
  }
  return {
    group: "action",
    line: `! ${c.name} — ${humanizeError(c.readyState?.message || "needs attention")}`,
    next: c.readyState?.message || `Check ${c.name}`,
  };
}

/**
 * @param {string} raw
 */
function humanizeError(raw) {
  const s = String(raw || "").trim();
  if (/already exists/i.test(s)) return "already connected";
  if (/rolled back|was undone|not available in .+ yet/i.test(s)) {
    return s.length > 200 ? `${s.slice(0, 197)}…` : s;
  }
  if (/host discovery not confirmed/i.test(s) || /Config written \(/i.test(s)) {
    return "could not confirm the app loaded Occam; connection was not kept";
  }
  if (/trust|enable|permission|folder/i.test(s)) return s.length > 120 ? `${s.slice(0, 117)}…` : s;
  if (/claude mcp add failed/i.test(s)) return "existing configuration needs attention";
  return s.length > 160 ? `${s.slice(0, 157)}…` : s || "needs attention";
}

/**
 * @param {object} report
 */
function hasOllamaRuntime(report) {
  return (report.runtimes || []).some(
    (r) => r?.detected !== false && (r.id === "ollama" || /ollama/i.test(r.name || "")),
  );
}

/**
 * Concrete “what do I do now?” block — one path the user can actually use.
 * @param {object} report
 * @param {{ ready: string[], restart: string[], configured: string[], notConnected: string[], action: string[], nextFromRows: string[] }} groups
 */
function renderWhatNext(report, groups) {
  /** @type {string[]} */
  const lines = [];
  lines.push("What to do now:");

  if (groups.ready.length) {
    const names = (report.connections || [])
      .filter((c) => classifyConnection(c).group === "ready")
      .map((c) => c.name)
      .filter(Boolean);
    const app = names[0] || "your AI app";
    lines.push(`1. Open ${app} and start a new chat.`);
    lines.push(`2. Ask: "Use Occam to read https://example.com"`);
    lines.push("3. Success looks like a real page summary (not “I don’t know Occam”).");
    lines.push("");
    lines.push(`Guide: ${FIRST_SUCCESS_URL}`);
    return lines;
  }

  if (groups.restart.length) {
    const names = (report.connections || [])
      .filter((c) => classifyConnection(c).group === "restart")
      .map((c) => c.name)
      .filter(Boolean);
    const app = names[0] || "the named app";
    lines.push(`1. Restart or reload ${app}.`);
    lines.push(`2. Open a new chat and ask: "Use Occam to read https://example.com"`);
    lines.push("");
    lines.push(`Guide: ${FIRST_SUCCESS_URL}`);
    return lines;
  }

  if (groups.configured.length && !groups.action.length) {
    const names = (report.connections || [])
      .filter((c) => classifyConnection(c).group === "configured")
      .map((c) => c.name)
      .filter(Boolean);
    const app = names[0] || "the configured app";
    lines.push(`1. Open or reload ${app}.`);
    lines.push(`2. Start a new chat and ask: "Use Occam to read https://example.com"`);
    lines.push("3. No further `occam connect` run is required for this app.");
    lines.push("");
    lines.push(`Guide: ${FIRST_SUCCESS_URL}`);
    return lines;
  }

  if (hasOllamaRuntime(report)) {
    lines.push("Occam is installed, but it is not connected to an AI chat app yet.");
    lines.push("");
    lines.push("Ollama is installed. To use a local model with Occam:");
    lines.push("  occam chat");
    lines.push("");
    lines.push("Or install/connect a supported app, then run:");
    lines.push("  occam connect");
    lines.push("");
    lines.push(`Guide: ${FIRST_SUCCESS_URL}`);
    return lines;
  }

  lines.push("Occam is installed, but it is not connected to an AI app yet.");
  lines.push("");
  lines.push("Available next steps:");
  lines.push("1. Install a supported AI app from the Occam host list.");
  lines.push("2. Run: occam connect");
  lines.push("3. Follow the first-success guide:");
  lines.push(`   ${FIRST_SUCCESS_URL}`);
  if (groups.notConnected.length) {
    lines.push("");
    lines.push(
      "Do not test Occam inside an app listed under “Not connected” — that connection was not kept.",
    );
  }
  return lines;
}

/**
 * Default product-facing connect summary (no internal levels / confidence).
 * @param {Awaited<ReturnType<import('./orchestrator.mjs').runConnect>>} report
 * @param {{ selectedNames?: string[], source?: string }} [opts]
 */
export function renderHumanConnectSummary(report, opts = {}) {
  const lines = [];
  lines.push("Occam — Connect");
  lines.push("");

  const connections = report.connections || [];
  if (!connections.length) {
    lines.push(report.status || "No apps connected.");
    if (report.message) lines.push(report.message);
    lines.push("");
    lines.push(...renderWhatNext(report, {
      ready: [],
      restart: [],
      configured: [],
      notConnected: [],
      action: [],
      nextFromRows: [],
    }));
    return lines.join("\n");
  }

  /** @type {string[]} */
  const ready = [];
  /** @type {string[]} */
  const configured = [];
  /** @type {string[]} */
  const restart = [];
  /** @type {string[]} */
  const action = [];
  /** @type {string[]} */
  const notConnected = [];
  /** @type {string[]} */
  const notConnectedDetail = [];
  /** @type {string[]} */
  const nextFromRows = [];

  for (const c of connections) {
    const row = classifyConnection(c);
    if (row.group === "ready") ready.push(row.line);
    else if (row.group === "configured") configured.push(row.line);
    else if (row.group === "restart") restart.push(row.line);
    else if (row.group === "not_connected") {
      notConnected.push(row.line);
      if (row.detail) notConnectedDetail.push(row.detail);
    } else action.push(row.line);
    if (row.next) nextFromRows.push(row.next);
  }

  if (ready.length) {
    lines.push("Connected and ready:");
    for (const l of ready) lines.push(l);
    lines.push("");
  }
  if (configured.length) {
    lines.push("Configured:");
    for (const l of configured) lines.push(l);
    lines.push("");
  }
  if (restart.length) {
    lines.push("Needs restart:");
    for (const l of restart) lines.push(l);
    lines.push("");
  }
  if (notConnected.length) {
    lines.push("Not connected:");
    for (const l of notConnected) lines.push(l);
    for (const d of notConnectedDetail) {
      lines.push(`  ${d}`);
    }
    lines.push("");
  }
  if (action.length) {
    lines.push("Needs your action:");
    for (const l of action) lines.push(l);
    lines.push("");
  }

  lines.push(
    ...renderWhatNext(report, {
      ready,
      restart,
      configured,
      notConnected,
      action,
      nextFromRows,
    }),
  );
  lines.push("");

  const readyCount = ready.length;
  const attention = restart.length + action.length + notConnected.length;
  if (readyCount && !attention && !configured.length) {
    lines.push("All selected apps are ready.");
    lines.push("Ready.");
  } else if (readyCount && attention) {
    lines.push(
      `${readyCount} app${readyCount === 1 ? "" : "s"} ready; ${attention} not ready yet.`,
    );
    lines.push("Action required.");
  } else if (restart.length && !action.length && !notConnected.length && !readyCount) {
    lines.push("Almost ready.");
  } else if (configured.length && !attention && !readyCount) {
    lines.push("Configured.");
  } else if (!readyCount) {
    lines.push("No AI app is ready to use Occam yet.");
    lines.push("Action required.");
  }

  return lines.join("\n");
}

/**
 * Human-readable connect transcript.
 * Default = product-facing summary; verbose = engineering detail.
 * @param {Awaited<ReturnType<import('./orchestrator.mjs').runConnect>>} report
 * @param {{ verbose?: boolean }} [opts]
 */
export function renderConnectTranscript(report, opts = {}) {
  if (!opts.verbose) {
    return renderHumanConnectSummary(report);
  }

  const lines = [];
  lines.push("Occam — Connect");
  lines.push("");
  lines.push(`✓ Occam home: ${report.occamHome}`);
  if (report.launch?.launcherPath) {
    lines.push(`✓ Stable launcher: node ${report.launch.launcherPath}`);
  }
  if (report.mode?.mode) {
    lines.push(`  Mode: ${report.mode.mode} (${report.mode.reason || ""})`);
  }
  lines.push("");

  lines.push("Detecting AI tools...");
  for (const h of report.hosts) {
    if (h.detected) {
      const kindLabel =
        h.kind === "IDE_EXTENSION"
          ? "IDE / MCP host"
          : h.kind === "AI_AGENT"
            ? "AI agent"
            : "MCP host";
      lines.push(`✓ ${h.name} — ${kindLabel} (${h.confidence})`);
    } else if (h.residue) {
      lines.push(`· ${h.name} — stale residue only (not connectable)`);
      for (const s of h.residueSignals || []) {
        lines.push(`    ${s}`);
      }
    }
  }
  for (const r of report.runtimes) {
    lines.push(`✓ ${r.name} — model runtime (not an MCP registration target)`);
  }
  if (!report.hosts.some((h) => h.detected) && report.runtimes.length === 0) {
    lines.push("· No supported MCP hosts or model runtimes detected");
  }
  lines.push("");

  if (report.mutateHosts && report.skipped?.length) {
    lines.push("Left untouched (needs your decision):");
    for (const s of report.skipped) {
      lines.push(`· ${s.name} — ${s.reason}`);
      if (s.hint) lines.push(`  → ${s.hint}`);
      for (const candidate of s.candidates || []) {
        lines.push(`    ${candidate}`);
      }
    }
    lines.push("");
  }

  if (report.mutateHosts && report.connections.length) {
    lines.push("Connecting Occam...");
    for (const c of report.connections) {
      if (c.apply?.action === "skip-unmanaged") {
        lines.push(`· ${c.name} — skipped unmanaged registration`);
      } else if (c.apply?.ok) {
        const act = c.apply.applied
          ? c.apply.action === "noop"
            ? "already connected"
            : c.plan?.requiresRestart || c.apply.requiresRestart
              ? "configured"
              : c.apply.action || "configured"
          : "already connected";
        lines.push(`✓ ${c.name} — ${act}`);
      } else {
        lines.push(`✗ ${c.name} — ${c.apply?.error || c.readyState?.message || "failed"}`);
      }
    }
    lines.push("");
    lines.push("Verifying...");
    if (report.occamVerify?.skipped) {
      lines.push("· Occam MCP verify skipped");
    } else if (report.occamVerify?.ok) {
      lines.push(
        `✓ Occam MCP initialize + tools/list (${report.occamVerify.toolCount} tools)`,
      );
    } else {
      lines.push(`✗ Occam MCP verify failed: ${report.occamVerify?.error || "unknown"}`);
    }
    for (const c of report.connections) {
      const restart =
        c.readyState?.requiresRestart === true ||
        /restart required/i.test(c.readyState?.status || "");
      const actionRequired =
        c.readyState?.requiresUserAction === true &&
        c.readyState?.hostBlocked === true;
      if (c.rollback?.ok) {
        lines.push(`↩ ${c.name} — not connected (change undone)`);
        if (c.readyState?.message) lines.push(`  ℹ ${c.readyState.message}`);
      } else if (actionRequired) {
        lines.push(
          `⚠ ${c.name} — action required: ${c.readyState?.message || "host policy blocks activation"}`,
        );
      } else if (c.hostVerify?.ok && restart) {
        lines.push(`↻ ${c.name} restart required`);
        if (c.readyState?.sessionHint) {
          lines.push(`  ℹ ${c.readyState.sessionHint}`);
        }
      } else if (c.hostVerify?.ok) {
        lines.push(`✓ ${c.name} sees Occam (level ${c.hostVerify.level})`);
        if (c.readyState?.sessionHint) {
          lines.push(`  ℹ ${c.readyState.sessionHint}`);
        }
      } else if (c.hostVerify) {
        lines.push(
          `⚠ ${c.name} host verify: ${c.hostVerify.message || "incomplete"} (level ${c.hostVerify.level})`,
        );
      }
      if (c.rollback && !c.rollback.ok) {
        lines.push(
          `  ✗ ${c.name} rollback ${c.rollback.kind} failed: ${c.rollback.error || "unknown"}`,
        );
      }
    }
    lines.push("");
  }

  if (report.ready) {
    lines.push("Ready.");
    lines.push('Try: "Use Occam to read https://example.com"');
  } else if (report.status === "Almost ready" || report.status === "Action required") {
    lines.push(`${report.status}.`);
    if (report.message) lines.push(report.message);
  } else {
    const status = report.status === "Partial" ? "Action required" : report.status;
    lines.push(`${status}.`);
    if (report.message) lines.push(report.message);
    if (!report.hosts.some((h) => h.detected)) {
      const { automatic, assisted } = partitionSupportedHosts(report.hosts);
      lines.push("");
      lines.push("Supported MCP hosts:");
      for (const line of wrapList(automatic, 72)) lines.push(`  ${line}`);
      if (assisted.length) {
        lines.push(`  Manual setup: ${assisted.join(" · ")}`);
      }
    }
  }

  lines.push("");
  lines.push(`Guide: ${FIRST_SUCCESS_URL}`);

  return lines.join("\n");
}
