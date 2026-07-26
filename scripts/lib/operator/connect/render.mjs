import { partitionSupportedHosts } from "./registry.mjs";

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
      next: `Restart ${c.name}`,
    };
  }
  if (ready || (c.hostVerify?.ok && !restart && !actionRequired)) {
    return { group: "ready", line: `✓ ${c.name}`, next: null };
  }
  if (already) {
    return { group: "ready", line: `✓ ${c.name}`, next: null };
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
  if (/trust|enable|permission|folder/i.test(s)) return s.length > 120 ? `${s.slice(0, 117)}…` : s;
  if (/claude mcp add failed/i.test(s)) return "existing configuration needs attention";
  return s.length > 120 ? `${s.slice(0, 117)}…` : s || "needs attention";
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
    return lines.join("\n");
  }

  /** @type {string[]} */
  const ready = [];
  /** @type {string[]} */
  const restart = [];
  /** @type {string[]} */
  const action = [];
  /** @type {string[]} */
  const nextSteps = [];

  for (const c of connections) {
    const row = classifyConnection(c);
    if (row.group === "ready") ready.push(row.line);
    else if (row.group === "restart") restart.push(row.line);
    else action.push(row.line);
    if (row.next) nextSteps.push(row.next);
  }

  if (ready.length) {
    lines.push("Connected and ready:");
    for (const l of ready) lines.push(l);
    lines.push("");
  }
  if (restart.length) {
    lines.push("Needs restart:");
    for (const l of restart) lines.push(l);
    lines.push("");
  }
  if (action.length) {
    lines.push("Needs your action:");
    for (const l of action) lines.push(l);
    lines.push("");
  }

  if (nextSteps.length) {
    lines.push("Next steps:");
    let i = 1;
    for (const step of nextSteps) {
      lines.push(`${i}. ${step}.`);
      i += 1;
    }
    if (action.length || restart.length) {
      lines.push(`${i}. Run \`occam connect\` again to verify everything.`);
    }
    lines.push("");
  }

  const status = report.status === "Partial" ? "Action required" : report.status || "Action required";
  const readyCount = ready.length;
  const attention = restart.length + action.length;
  if (report.occamVerify?.ok !== false || report.occamVerify?.skipped) {
    lines.push("Occam itself is working.");
  }
  if (readyCount && attention) {
    lines.push(
      `${readyCount} app${readyCount === 1 ? "" : "s"} ready now; ${attention} need${attention === 1 ? "s" : ""} attention.`,
    );
  } else if (readyCount && !attention) {
    lines.push("All selected apps are ready.");
  }
  lines.push("");
  if (status === "Ready") lines.push("Ready.");
  else if (status === "Almost ready") lines.push("Almost ready.");
  else lines.push("Action required.");

  if (report.ready) {
    lines.push('Try: "Use Occam to read https://example.com"');
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
      if (actionRequired) {
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
      if (c.rollback) {
        if (c.rollback.ok) {
          lines.push(`  ↩ ${c.name} rolled back (${c.rollback.kind})`);
        } else {
          lines.push(
            `  ✗ ${c.name} rollback ${c.rollback.kind} failed: ${c.rollback.error || "unknown"}`,
          );
        }
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

  return lines.join("\n");
}
