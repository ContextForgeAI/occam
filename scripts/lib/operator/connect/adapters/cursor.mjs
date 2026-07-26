/**
 * Cursor adapter — CONFIG_FILE via shared McpConfigEngine.
 *
 * Live findings (2026-07-25):
 * - User: ~/.cursor/mcp.json → mcpServers
 * - Workspace: <repo>/.cursor/mcp.json (may use ${workspaceFolder})
 * - Desktop auto-connect writes **user** config with absolute stable launcher paths
 * - CLI cannot observe Cursor's in-session MCP load (max Level = CONFIG_VALID)
 * - requiresRestart only with evidence: config mutated during this connect run
 */
import { existsSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import {
  commitMcpRegistration,
  inspectManagedEntry,
  loadMcpConfig,
  mcpEntriesEqual,
  redactMcpEntry,
  rollbackMcpRegistration,
  writeMcpConfigAtomic,
  applyMergeToDoc,
  backupMcpConfig,
} from "../config-engine.mjs";
import { OCCAM_MCP_SERVER_NAME } from "../kinds.mjs";
import { buildStableLaunchSpec, stdioFromSpec } from "../launch-spec.mjs";
import { looksLikeOccamManagedEntry } from "../ownership.mjs";
import { which } from "../process.mjs";
import { VERIFICATION_LEVELS } from "../verification.mjs";

export const CURSOR_ADAPTER_ID = "cursor";
export const CURSOR_ROOT_KEY = "mcpServers";

export function cursorUserConfigPath() {
  return join(homedir(), ".cursor", "mcp.json");
}

/**
 * @param {string} [workspaceRoot]
 */
export function cursorWorkspaceConfigPath(workspaceRoot) {
  if (!workspaceRoot) return null;
  return join(workspaceRoot, ".cursor", "mcp.json");
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createCursorAdapter(ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;
  const configPath = cursorUserConfigPath();

  /** @type {{ backupPath?: string|null, previousRaw?: string|null, previousMissing?: boolean }|null} */
  let lastSnap = null;

  return {
    id: CURSOR_ADAPTER_ID,
    name: "Cursor",
    kind: "IDE_EXTENSION",
    connectionMethod: "CONFIG_FILE",
    supportTier: "A",
    maxVerificationLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    platforms: ["win32", "darwin", "linux"],

    detect() {
      const bin = which("cursor");
      const userPath = configPath;
      const wsPath = cursorWorkspaceConfigPath(ctx.workspaceRoot || occamHome);
      const detected =
        Boolean(bin) || existsSync(userPath) || Boolean(wsPath && existsSync(wsPath));
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (bin && existsSync(userPath)) confidence = "high";
      else if (bin || existsSync(userPath)) confidence = "medium";
      else if (wsPath && existsSync(wsPath)) confidence = "medium";
      return {
        id: CURSOR_ADAPTER_ID,
        name: "Cursor",
        kind: "IDE_EXTENSION",
        detected,
        confidence,
        executable: bin,
        configPath: userPath,
      };
    },

    inspect() {
      const loaded = loadMcpConfig(configPath, { rootKey: CURSOR_ROOT_KEY });
      const inspected = inspectManagedEntry(loaded, serverName);
      return {
        ...inspected,
        redacted: redactMcpEntry(inspected.entry),
      };
    },

    plan(opts = {}) {
      const spec = buildStableLaunchSpec(occamHome);
      // Absolute node+launcher for user-global Cursor (no ${workspaceFolder}).
      const desired = stdioFromSpec(spec, { preferWrapper: false, includeCwd: true });
      const loaded = loadMcpConfig(configPath, { rootKey: CURSOR_ROOT_KEY });
      if (!loaded.ok) {
        return {
          adapterId: CURSOR_ADAPTER_ID,
          serverName,
          connectionMethod: "CONFIG_FILE",
          canAutoConfigure: false,
          requiresRestart: false,
          action: "refuse",
          skipReason: loaded.error || "malformed Cursor mcp.json",
          configPath,
          desired,
          current: null,
          managed: false,
        };
      }
      const current = inspectManagedEntry(loaded, serverName);
      const matches = current.entry && mcpEntriesEqual(current.entry, desired);
      const managed = looksLikeOccamManagedEntry(current.entry, occamHome);

      /** @type {string} */
      let action = "add";
      if (matches) action = "noop";
      else if (current.registered) {
        action = managed || opts.force ? "update" : "skip-unmanaged";
      }

      const wouldMutate = action === "add" || action === "update";
      return {
        adapterId: CURSOR_ADAPTER_ID,
        serverName,
        connectionMethod: "CONFIG_FILE",
        canAutoConfigure: true,
        // Evidence-based: restart only if this plan would rewrite config.
        requiresRestart: wouldMutate,
        sessionHint: wouldMutate
          ? "Reload MCP servers in Cursor (or restart Cursor) to activate Occam"
          : "Occam is configured for Cursor; reload MCP only if tools are missing in an already-open session",
        verificationLevel: VERIFICATION_LEVELS.CONFIG_VALID,
        desired,
        current: current.entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in Cursor user mcp.json does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath,
      };
    },

    apply(opts = {}) {
      const plan = this.plan({ force: opts.force });
      if (plan.action === "refuse") {
        return { ok: false, applied: false, action: "refuse", error: plan.skipReason, plan };
      }
      if (plan.action === "skip-unmanaged") {
        return {
          ok: false,
          applied: false,
          action: "skip-unmanaged",
          error: plan.skipReason || "unmanaged registration",
          plan,
        };
      }
      if (plan.action === "noop" && !opts.force) {
        return {
          ok: true,
          applied: false,
          action: "noop",
          requiresRestart: false,
          configured: true,
          sessionHint: plan.sessionHint,
          plan,
        };
      }

      const result = commitMcpRegistration({
        configPath,
        rootKey: CURSOR_ROOT_KEY,
        serverName,
        desired: plan.desired,
        occamHome,
        force: opts.force,
      });
      if (result.applied) {
        lastSnap = {
          backupPath: result.backupPath,
          previousRaw: result.previousRaw,
          previousMissing: result.previousMissing,
          rootKey: CURSOR_ROOT_KEY,
        };
      }
      return {
        ...result,
        // Evidence: we changed Cursor config during this invocation.
        requiresRestart: result.applied === true,
        configured: true,
        sessionHint: plan.sessionHint,
      };
    },

    /**
     * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }} entry
     */
    restoreEntry(entry) {
      const loaded = loadMcpConfig(configPath, { rootKey: CURSOR_ROOT_KEY });
      if (!loaded.ok) return { ok: false, error: loaded.error || "malformed config" };
      if (!entry?.command) return { ok: false, error: "no previous entry to restore" };
      const backup = backupMcpConfig(configPath);
      const previousRaw = loaded.exists ? readFileSync(configPath, "utf8") : null;
      const doc = applyMergeToDoc(loaded, serverName, entry);
      writeMcpConfigAtomic(configPath, doc);
      lastSnap = {
        backupPath: backup.backupPath,
        previousRaw,
        previousMissing: !loaded.exists,
      };
      return { ok: true, applied: true, action: "restore" };
    },

    verifyHost() {
      const loaded = loadMcpConfig(configPath, { rootKey: CURSOR_ROOT_KEY });
      if (!loaded.ok) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: loaded.error || "Cursor mcp.json unreadable",
        };
      }
      const plan = this.plan();
      const inspected = inspectManagedEntry(loaded, serverName);
      if (!inspected.registered) {
        return {
          ok: false,
          configured: false,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: `${serverName} missing from Cursor user mcp.json`,
        };
      }
      const matches = mcpEntriesEqual(inspected.entry, plan.desired);
      // Do NOT set requiresRestart here — Cursor runtime load is not observable
      // from the CLI. Restart evidence comes only from apply() when config mutated.
      return {
        ok: matches,
        configured: matches,
        level: matches ? VERIFICATION_LEVELS.CONFIG_VALID : VERIFICATION_LEVELS.INSTALLED,
        requiresRestart: false,
        sessionHint: matches
          ? "Occam is configured for Cursor; reload MCP in an open session if tools are missing"
          : undefined,
        message: matches
          ? "Cursor mcp.json has Occam registration"
          : "Cursor mcp.json entry does not match stable launcher",
      };
    },

    rollback() {
      if (lastSnap) {
        const r = rollbackMcpRegistration(configPath, lastSnap);
        return { ok: r.ok !== false, removed: true, result: r, error: r.error };
      }
      // Fallback: remove managed entry only.
      const loaded = loadMcpConfig(configPath, { rootKey: CURSOR_ROOT_KEY });
      if (!loaded.ok) return { ok: false, error: loaded.error || "malformed config" };
      if (!inspectManagedEntry(loaded, serverName).registered) {
        return { ok: true, removed: false };
      }
      const previousRaw = loaded.exists ? readFileSync(configPath, "utf8") : null;
      const backup = backupMcpConfig(configPath);
      const servers = { ...(loaded.doc?.[CURSOR_ROOT_KEY] || {}) };
      delete servers[serverName];
      const doc = { ...loaded.doc, [CURSOR_ROOT_KEY]: servers };
      writeMcpConfigAtomic(configPath, doc);
      lastSnap = {
        backupPath: backup.backupPath,
        previousRaw,
        previousMissing: !loaded.exists,
      };
      return { ok: true, removed: true };
    },
  };
}
