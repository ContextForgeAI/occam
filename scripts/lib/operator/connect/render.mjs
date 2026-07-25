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
 * Human-readable connect transcript (English operator UX).
 * @param {Awaited<ReturnType<import('./orchestrator.mjs').runConnect>>} report
 */
export function renderConnectTranscript(report) {
  const lines = [];
  lines.push("FF-Occam — Connect");
  lines.push("");
  lines.push(`✓ Occam home: ${report.occamHome}`);
  if (report.launch?.launcherPath) {
    lines.push(`✓ Stable launcher: node ${report.launch.launcherPath}`);
  }
  lines.push(`  Mode: ${report.mode.mode} (${report.mode.reason})`);
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
            ? "already configured"
            : c.plan?.requiresRestart || c.apply.requiresRestart
              ? "configured"
              : c.apply.action || "configured"
          : "already configured";
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
    lines.push(`${report.status}.`);
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
