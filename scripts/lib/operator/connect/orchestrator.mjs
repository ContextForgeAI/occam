/**
 * Connect orchestrator: detect → plan → apply → verify → Ready.
 * Core stays product-agnostic — session hints / restart flags come from adapters.
 *
 * Per-host transaction (minimal):
 *   inspect/plan → apply → verify → commit on Ready
 *   broken verify after mutation → remove (add) or restore previous (update)
 *   restart / trust / permission / host-policy blocker → preserve registration
 * Peer hosts are independent (no global all-or-nothing rollback).
 */
import { assertLaunchable } from "./launch-spec.mjs";
import { verifyOccamMcp } from "./occam-verify.mjs";
import { resolveConnectMode } from "./policy.mjs";
import {
  describeSupportedHosts,
  detectAllRuntimes,
  listHostAdapters,
  selectAutoConnectAdapters,
} from "./registry.mjs";
import {
  aggregateConnectionReady,
  evaluateReadyState,
  VERIFICATION_LEVELS,
} from "./verification.mjs";
import { decidePostVerifyCleanup } from "./ownership.mjs";
import { OCCAM_MCP_SERVER_NAME } from "./kinds.mjs";

/**
 * @param {{
 *   occamHome: string,
 *   mutateHosts?: boolean,
 *   force?: boolean,
 *   only?: string[],
 *   connectMode?: ReturnType<typeof resolveConnectMode>,
 *   skipOccamVerify?: boolean,
 *   onProgress?: (ev: {
 *     phase: string,
 *     name?: string,
 *     ok?: boolean,
 *     already?: boolean,
 *     restart?: boolean,
 *     action?: boolean,
 *     message?: string,
 *   }) => void,
 * }} opts
 */
/**
 * Why a detected host was left alone, plus the one step that would connect it.
 * @param {{ id: string, name: string, connectionMethod?: string, supportTier?: string, detect: () => object, plan: (o?: object) => object }} adapter
 * @param {boolean} explicitHosts
 */
function describeSkippedHost(adapter, explicitHosts) {
  const detection = adapter.detect();
  /** @type {{ id: string, name: string, reason: string, hint: string|null, candidates?: string[] }} */
  const row = {
    id: adapter.id,
    name: adapter.name,
    reason: "not eligible for automatic connection",
    hint: null,
  };

  if (detection.ambiguous === true) {
    let reason = null;
    try {
      reason = adapter.plan().skipReason;
    } catch {
      /* fall back to the generic reason */
    }
    row.reason = "config location is ambiguous";
    row.hint = reason || "Choose the active config file, then re-run connect";
    row.candidates = detection.candidates || [];
    return row;
  }
  if (adapter.connectionMethod === "ASSISTED") {
    row.reason = "manual setup only";
    try {
      row.hint = adapter.plan().skipReason;
    } catch {
      row.hint = null;
    }
    return row;
  }
  if (adapter.supportTier === "B" && !explicitHosts) {
    row.reason = "not validated end-to-end yet";
    row.hint = `Connect it explicitly: occam connect --only ${adapter.id}`;
    return row;
  }
  if (detection.confidence === "low") {
    row.reason = "install signals too weak to write a config safely";
    row.hint = `Connect it explicitly: occam connect --only ${adapter.id}`;
  }
  return row;
}

export async function runConnect(opts) {
  const occamHome = opts.occamHome;
  const mode = opts.connectMode ?? resolveConnectMode(process.env);
  const mutateHosts = opts.mutateHosts ?? mode.mutateHosts;
  const progress = typeof opts.onProgress === "function" ? opts.onProgress : () => {};

  /** @type {object} */
  const report = {
    schema_version: "1.0",
    occamHome,
    serverName: OCCAM_MCP_SERVER_NAME,
    mode,
    mutateHosts,
    launch: null,
    runtimes: [],
    hosts: [],
    connections: [],
    skipped: [],
    occamVerify: null,
    ready: false,
    status: "Not ready",
    message: "",
  };

  try {
    report.launch = assertLaunchable(occamHome);
  } catch (err) {
    report.message = err instanceof Error ? err.message : String(err);
    report.status = "Not ready";
    return report;
  }

  report.runtimes = detectAllRuntimes();

  const adapters = listHostAdapters({
    occamHome,
    only: opts.only,
  });

  for (const adapter of adapters) {
    const detection = adapter.detect();
    report.hosts.push({
      id: adapter.id,
      name: adapter.name,
      kind: adapter.kind,
      supportTier: adapter.supportTier,
      connectionMethod: adapter.connectionMethod,
      ...detection,
      inspect: detection.detected ? adapter.inspect() : null,
    });
  }

  const explicitHosts = Array.isArray(opts.only) && opts.only.length > 0;
  const targets = mutateHosts
    ? selectAutoConnectAdapters(adapters, { explicit: explicitHosts })
    : [];

  if (mutateHosts) {
    // A detected host that is deliberately not auto-connected must say why,
    // otherwise the run ends in a bare "Not ready" with nothing to act on.
    const targetIds = new Set(targets.map((a) => a.id));
    report.skipped = adapters
      .filter((a) => !targetIds.has(a.id) && a.detect().detected)
      .map((adapter) => describeSkippedHost(adapter, explicitHosts));
  }

  if (!mutateHosts) {
    report.message = mode.reason || "host mutation disabled";
  }

  const occamVerify = opts.skipOccamVerify
    ? { ok: true, level: VERIFICATION_LEVELS.TOOLS_LIST_OK, toolCount: 15, skipped: true }
    : await (async () => {
        progress({ phase: "verify-start", name: "Occam" });
        const v = await verifyOccamMcp(occamHome);
        progress({
          phase: "verify-done",
          name: "Occam",
          ok: v.ok === true,
          message: v.error || undefined,
        });
        return v;
      })();
  report.occamVerify = occamVerify;

  if (targets.length === 0 && mutateHosts) {
    const detectedHosts = report.hosts.filter((h) => h.detected);
    if (detectedHosts.length === 0 && report.runtimes.length > 0) {
      report.status = "Installed — runtime only";
      report.message =
        "No supported MCP host detected. Model runtimes were found but do not receive Occam MCP registration.";
      report.ready = false;
      return report;
    }
    if (detectedHosts.length === 0) {
      report.status = "Installed — no MCP host";
      report.message = `Occam installed. No supported MCP host detected. Supported: ${describeSupportedHosts(report.hosts)}.`;
      report.ready = false;
      return report;
    }
    report.status = "Action required";
    report.message = report.skipped.length
      ? report.skipped
          .map((s) => `${s.name}: ${s.hint || s.reason}`)
          .join(" · ")
      : "Detected hosts were not eligible for automatic connection.";
    report.ready = false;
    return report;
  }

  for (const adapter of targets) {
    /** @type {Record<string, unknown>} */
    const row = {
      id: adapter.id,
      name: adapter.name,
      plan: null,
      apply: null,
      hostVerify: null,
      readyState: null,
      rollback: null,
    };

    try {
      progress({ phase: "configure-start", name: adapter.name });
      const plan = adapter.plan({ force: opts.force });
      row.plan = {
        action: plan.action,
        requiresRestart: plan.requiresRestart,
        sessionHint: plan.sessionHint ?? null,
        configPath: plan.configPath,
        reloadNote: plan.reloadNote ?? null,
        managed: plan.managed === true,
      };

      if (plan.action === "skip-unmanaged") {
        row.apply = { ok: true, applied: false, action: "skip-unmanaged", error: null };
        row.readyState = {
          ready: false,
          status: "Skipped — unmanaged registration",
          message:
            plan.skipReason ||
            `Existing ${OCCAM_MCP_SERVER_NAME} looks user-owned; pass --force to overwrite`,
        };
        progress({
          phase: "configure-done",
          name: adapter.name,
          ok: false,
          action: true,
          message: "existing configuration needs your decision",
        });
        report.connections.push(row);
        continue;
      }

      if (plan.action === "assisted" || plan.action === "ambiguous" || plan.action === "jsonc") {
        row.apply = {
          ok: false,
          applied: false,
          action: plan.action,
          error: plan.skipReason || plan.action,
        };
        row.readyState = {
          configured: false,
          requiresUserAction: true,
          hostBlocked: true,
          ready: false,
          // Nothing was written here, so do not claim it is configured.
          status: "Action required — manual setup",
          message: plan.skipReason || `${adapter.name} requires user action`,
        };
        progress({
          phase: "configure-done",
          name: adapter.name,
          ok: false,
          action: true,
          message: plan.skipReason || "needs your action",
        });
        report.connections.push(row);
        continue;
      }

      const previousEntry = plan.current ? structuredClone(plan.current) : null;
      const applied = adapter.apply({ force: opts.force });
      row.apply = {
        ok: applied.ok,
        applied: applied.applied,
        action: applied.action,
        error: applied.error ?? null,
        requiresRestart: applied.requiresRestart ?? plan.requiresRestart,
      };

      progress({
        phase: "configure-done",
        name: adapter.name,
        ok: applied.ok !== false,
        already: applied.action === "noop" || (applied.ok && applied.applied === false),
        message: applied.error || undefined,
      });

      if (!applied.ok) {
        if (applied.requiresUserAction === true) {
          row.readyState = {
            configured: applied.configured === true,
            requiresUserAction: true,
            hostBlocked: true,
            ready: false,
            status: "Configured — action required",
            message: applied.error || "host requires user action before auto-connect",
          };
        } else {
          row.readyState = {
            ready: false,
            status: "Apply failed",
            message: applied.error || "apply failed",
          };
        }
        report.connections.push(row);
        continue;
      }

      progress({ phase: "verify-start", name: adapter.name });
      const hostVerify = adapter.verifyHost();
      row.hostVerify = {
        ok: hostVerify.ok,
        level: hostVerify.level,
        toolCount: hostVerify.toolCount ?? null,
        message: hostVerify.message ?? null,
        requiresRestart: hostVerify.requiresRestart === true,
        configured: hostVerify.configured === true,
        requiresUserAction: hostVerify.requiresUserAction === true,
        hostBlocked: hostVerify.hostBlocked === true,
        sessionHint: hostVerify.sessionHint ?? plan.sessionHint ?? null,
      };

      const readyState = evaluateReadyState({
        occamLevel: /** @type {import('./verification.mjs').VerificationLevel} */ (
          occamVerify.level
        ),
        hostLevel: /** @type {import('./verification.mjs').VerificationLevel} */ (
          hostVerify.level
        ),
        maxHostLevel: adapter.maxVerificationLevel,
        // Restart only with evidence: this apply mutated config, or verify proved it.
        requiresRestart:
          applied.requiresRestart === true || hostVerify.requiresRestart === true,
        configured: hostVerify.configured === true || hostVerify.ok === true,
        requiresUserAction: hostVerify.requiresUserAction === true,
        hostBlocked: hostVerify.hostBlocked === true,
        actionMessage: hostVerify.message,
      });
      if (hostVerify.sessionHint || plan.sessionHint) {
        readyState.sessionHint = hostVerify.sessionHint || plan.sessionHint;
      }

      progress({
        phase: "verify-done",
        name: adapter.name,
        ok: hostVerify.ok === true && readyState.ready !== false,
        configured:
          readyState.status === "Configured" ||
          (hostVerify.configured === true && readyState.requiresRestart !== true),
        restart:
          readyState.requiresRestart === true ||
          /restart required/i.test(readyState.status || ""),
        action:
          hostVerify.requiresUserAction === true && hostVerify.hostBlocked === true,
        message: readyState.message || hostVerify.message || undefined,
      });

      const cleanup = decidePostVerifyCleanup({
        applied: applied.applied === true,
        action: applied.action || plan.action,
        // Preserve valid registrations blocked by restart or host action.
        // Only broken registrations roll back.
        verifyOk: hostVerify.ok === true,
        configured: hostVerify.configured === true || hostVerify.ok === true,
        requiresRestart:
          applied.requiresRestart === true || hostVerify.requiresRestart === true,
        requiresUserAction: hostVerify.requiresUserAction === true,
        hostBlocked: hostVerify.hostBlocked === true,
      });

      if (cleanup !== "none") {
        /** @type {{ ok: boolean, kind: string, error?: string, result?: unknown }} */
        let rollbackResult;
        try {
          if (cleanup === "remove") {
            const r = adapter.rollback();
            rollbackResult = {
              ok: r.ok !== false,
              kind: "remove",
              error: r.error,
              result: r,
            };
          } else if (cleanup === "restore" && previousEntry && typeof adapter.restoreEntry === "function") {
            const r = adapter.restoreEntry(previousEntry);
            rollbackResult = {
              ok: r.ok !== false,
              kind: "restore",
              error: r.error,
              result: r,
            };
          } else if (cleanup === "restore") {
            rollbackResult = {
              ok: false,
              kind: "restore",
              error: "adapter cannot restore previous entry",
            };
          } else {
            rollbackResult = { ok: true, kind: cleanup };
          }
        } catch (err) {
          rollbackResult = {
            ok: false,
            kind: cleanup,
            error: err instanceof Error ? err.message : String(err),
          };
        }
        row.rollback = rollbackResult;

        // Keep original verify failure primary; surface rollback separately.
        readyState.ready = false;
        if (!readyState.status || readyState.status === "Ready") {
          readyState.status = "Configured — host discovery incomplete";
        }
        const verifyMsg = readyState.message || hostVerify.message || "verify failed";
        if (rollbackResult.ok) {
          readyState.message = `${verifyMsg}; rolled back (${rollbackResult.kind})`;
        } else {
          readyState.message = `${verifyMsg}; rollback ${rollbackResult.kind} failed: ${
            rollbackResult.error || "unknown"
          }`;
          readyState.rollbackFailed = true;
        }
      }

      row.readyState = readyState;
    } catch (err) {
      row.readyState = {
        ready: false,
        status: "Error",
        message: err instanceof Error ? err.message : String(err),
      };
      progress({
        phase: "configure-done",
        name: adapter.name,
        ok: false,
        message: err instanceof Error ? err.message : String(err),
      });
    }

    report.connections.push(row);
  }

  if (report.connections.length > 0) {
    const agg = aggregateConnectionReady(report.connections);
    report.ready = agg.ready;
    report.status = agg.status;
    report.message = agg.message;
  } else if (!mutateHosts) {
    report.ready = false;
    report.status = "Installed — connect skipped";
    report.message = mode.reason;
  }

  return report;
}
