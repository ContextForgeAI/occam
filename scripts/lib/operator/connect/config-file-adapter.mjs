/**
 * Generic CONFIG_FILE adapter factory — profile + codec + config-engine.
 * Host-specific argv / paths / restart live in the profile, not orchestrator.
 */
import { existsSync, readFileSync } from "node:fs";
import {
  applyMergeToDoc,
  backupMcpConfig,
  commitMcpRegistration,
  inspectManagedEntry,
  loadMcpConfig,
  mcpEntriesEqual,
  redactMcpEntry,
  rollbackMcpRegistration,
  writeMcpConfigAtomic,
  STDIO_ENTRY_CODEC,
} from "./config-engine.mjs";
import { OCCAM_MCP_SERVER_NAME } from "./kinds.mjs";
import { buildStableLaunchSpec, stdioFromSpec } from "./launch-spec.mjs";
import { looksLikeOccamManagedEntry } from "./ownership.mjs";
import { VERIFICATION_LEVELS } from "./verification.mjs";

/**
 * @typedef {import('./config-engine.mjs').EntryCodec} EntryCodec
 * @typedef {import('./config-engine.mjs').McpStdioOrUrlEntry} McpStdioOrUrlEntry
 *
 * @typedef {{
 *   path: string|null,
 *   ambiguous?: boolean,
 *   candidates?: string[],
 *   existing?: string[],
 *   reason?: string,
 * }} ResolvedConfigTarget
 *
 * @typedef {{
 *   id: string,
 *   name: string,
 *   kind?: string,
 *   supportTier?: 'A'|'B'|'C'|'D',
 *   platforms?: string[],
 *   rootKey: string,
 *   codec?: EntryCodec,
 *   connectionMethod?: 'CONFIG_FILE'|'ASSISTED',
 *   requiresRestart?: boolean,
 *   sessionHint?: string,
 *   preferWrapper?: boolean,
 *   includeCwd?: boolean,
 *   resolveConfigTarget: (ctx: { occamHome: string, workspaceRoot?: string }) => ResolvedConfigTarget,
 *   detectExtra?: (ctx: object, target: ResolvedConfigTarget) => {
 *     detected?: boolean,
 *     confidence?: 'high'|'medium'|'low',
 *     executable?: string|null,
 *     signals?: string[],
 *   },
 * }} ConfigHostProfile
 */

/**
 * Whether the config file already carries the host root key, so rollback knows
 * if an empty root object would be a leftover of ours.
 * @param {string} configPath
 * @param {string} rootKey
 */
function hasRootKeyOnDisk(configPath, rootKey) {
  if (!existsSync(configPath)) return false;
  try {
    const doc = JSON.parse(readFileSync(configPath, "utf8"));
    return Boolean(doc) && typeof doc === "object" && Object.hasOwn(doc, rootKey);
  } catch {
    return false;
  }
}

/**
 * @param {ConfigHostProfile} profile
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createConfigFileAdapter(profile, ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;
  const codec = profile.codec || STDIO_ENTRY_CODEC;
  const rootKey = profile.rootKey;
  const requiresRestart = profile.requiresRestart !== false;
  const sessionHint =
    profile.sessionHint ||
    `Reload or restart ${profile.name} to activate Occam`;
  const connectionMethod = profile.connectionMethod || "CONFIG_FILE";

  /** @type {{ backupPath?: string|null, previousRaw?: string|null, previousMissing?: boolean, rootKey?: string }|null} */
  let lastSnap = null;

  function resolveTarget() {
    return profile.resolveConfigTarget({
      occamHome,
      workspaceRoot: ctx.workspaceRoot,
    });
  }

  function desiredStdio() {
    const spec = buildStableLaunchSpec(occamHome);
    return stdioFromSpec(spec, {
      preferWrapper: profile.preferWrapper === true,
      includeCwd: profile.includeCwd !== false,
    });
  }

  return {
    id: profile.id,
    name: profile.name,
    kind: profile.kind || "MCP_HOST",
    connectionMethod,
    supportTier: profile.supportTier || "A",
    maxVerificationLevel: VERIFICATION_LEVELS.CONFIG_VALID,
    platforms: profile.platforms || ["win32", "darwin", "linux"],
    profile,

    detect() {
      const target = resolveTarget();
      const extra = profile.detectExtra?.(ctx, target) || {};
      const pathExists = Boolean(target.path && existsSync(target.path));
      const detected =
        extra.detected === true ||
        pathExists ||
        (target.existing?.length ?? 0) > 0 ||
        target.ambiguous === true;
      /** @type {'high'|'medium'|'low'} */
      let confidence = extra.confidence || "low";
      if (!extra.confidence) {
        if (target.ambiguous) confidence = "medium";
        else if (pathExists) confidence = "high";
        else if (detected) confidence = "medium";
      }
      return {
        id: profile.id,
        name: profile.name,
        kind: profile.kind || "MCP_HOST",
        detected,
        confidence,
        executable: extra.executable ?? null,
        configPath: target.path,
        ambiguous: target.ambiguous === true,
        candidates: target.candidates || [],
        signals: extra.signals || [],
      };
    },

    inspect() {
      const target = resolveTarget();
      if (!target.path) {
        return {
          registered: false,
          entry: null,
          path: null,
          ambiguous: target.ambiguous === true,
          candidates: target.candidates || [],
        };
      }
      const loaded = loadMcpConfig(target.path, { rootKey });
      const inspected = inspectManagedEntry(loaded, serverName, { codec });
      return {
        ...inspected,
        redacted: redactMcpEntry(inspected.entry),
        ambiguous: target.ambiguous === true,
        candidates: target.candidates || [],
      };
    },

    plan(opts = {}) {
      const target = resolveTarget();
      const desired = desiredStdio();
      if (connectionMethod === "ASSISTED") {
        return {
          adapterId: profile.id,
          serverName,
          connectionMethod,
          canAutoConfigure: false,
          requiresRestart,
          action: "assisted",
          skipReason:
            target.reason ||
            `${profile.name} requires assisted/manual MCP registration`,
          configPath: target.path,
          desired,
          current: null,
          managed: false,
        };
      }
      if (target.ambiguous || !target.path) {
        return {
          adapterId: profile.id,
          serverName,
          connectionMethod,
          canAutoConfigure: false,
          requiresRestart,
          action: "ambiguous",
          skipReason:
            target.reason ||
            `Ambiguous ${profile.name} config path — choose one explicitly`,
          configPath: null,
          candidates: target.candidates || target.existing || [],
          desired,
          current: null,
          managed: false,
        };
      }
      const loaded = loadMcpConfig(target.path, { rootKey });
      if (!loaded.ok) {
        return {
          adapterId: profile.id,
          serverName,
          connectionMethod,
          canAutoConfigure: false,
          requiresRestart,
          action: loaded.jsonc ? "jsonc" : "refuse",
          skipReason: loaded.jsonc
            ? `${profile.name} config uses comments (JSONC). Occam will not rewrite it — add ${serverName} manually to keep your comments.`
            : loaded.error || `malformed ${profile.name} config`,
          configPath: target.path,
          desired,
          current: null,
          managed: false,
        };
      }
      const current = inspectManagedEntry(loaded, serverName, { codec });
      const matches = current.entry && mcpEntriesEqual(current.entry, desired);
      const managed = looksLikeOccamManagedEntry(current.entry, occamHome);
      /** @type {string} */
      let action = "add";
      if (matches) action = "noop";
      else if (current.registered) {
        action = managed || opts.force ? "update" : "skip-unmanaged";
      }
      return {
        adapterId: profile.id,
        serverName,
        connectionMethod,
        canAutoConfigure: true,
        requiresRestart,
        sessionHint,
        verificationLevel: VERIFICATION_LEVELS.CONFIG_VALID,
        desired,
        current: current.entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in ${profile.name} does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath: target.path,
      };
    },

    apply(opts = {}) {
      const plan = this.plan({ force: opts.force });
      if (plan.action === "assisted" || plan.action === "ambiguous" || plan.action === "jsonc") {
        return {
          ok: false,
          applied: false,
          action: plan.action,
          error: plan.skipReason,
          requiresUserAction: true,
          hostBlocked: true,
          configured: false,
          plan,
        };
      }
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
          requiresRestart,
          sessionHint,
          plan,
        };
      }
      const previousRootMissing = !hasRootKeyOnDisk(plan.configPath, rootKey);
      const result = commitMcpRegistration({
        configPath: plan.configPath,
        rootKey,
        serverName,
        desired: plan.desired,
        occamHome,
        force: opts.force,
        codec,
      });
      if (result.applied) {
        let appliedRaw = null;
        try {
          appliedRaw = readFileSync(plan.configPath, "utf8");
        } catch {
          /* rollback falls back to surgical mode */
        }
        lastSnap = {
          backupPath: result.backupPath,
          previousRaw: result.previousRaw,
          previousMissing: result.previousMissing,
          previousEntry: plan.current ?? null,
          previousRootMissing,
          appliedRaw,
          rootKey,
        };
      }
      return {
        ...result,
        requiresRestart,
        sessionHint,
      };
    },

    /**
     * @param {McpStdioOrUrlEntry} entry
     */
    restoreEntry(entry) {
      const target = resolveTarget();
      if (!target.path) return { ok: false, error: "no config path" };
      const loaded = loadMcpConfig(target.path, { rootKey });
      if (!loaded.ok) return { ok: false, error: loaded.error || "malformed config" };
      if (!entry?.command) return { ok: false, error: "no previous entry to restore" };
      const backup = backupMcpConfig(target.path);
      const previousRaw = loaded.exists ? readFileSync(target.path, "utf8") : null;
      const doc = applyMergeToDoc(loaded, serverName, entry, { codec });
      writeMcpConfigAtomic(target.path, doc);
      lastSnap = {
        backupPath: backup.backupPath,
        previousRaw,
        previousMissing: !loaded.exists,
        rootKey,
      };
      return { ok: true, applied: true, action: "restore" };
    },

    verifyHost() {
      const plan = this.plan();
      if (plan.action === "assisted" || plan.action === "ambiguous" || plan.action === "jsonc") {
        return {
          ok: false,
          configured: false,
          requiresUserAction: true,
          hostBlocked: true,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: plan.skipReason,
        };
      }
      if (!plan.configPath) {
        return {
          ok: false,
          configured: false,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: `${profile.name} config path unavailable`,
        };
      }
      const loaded = loadMcpConfig(plan.configPath, { rootKey });
      if (!loaded.ok) {
        return {
          ok: false,
          configured: false,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: loaded.error || `${profile.name} config unreadable`,
        };
      }
      const inspected = inspectManagedEntry(loaded, serverName, { codec });
      if (!inspected.registered) {
        return {
          ok: false,
          configured: false,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: `${serverName} missing from ${profile.name} config`,
        };
      }
      const matches = mcpEntriesEqual(inspected.entry, plan.desired);
      return {
        ok: matches,
        configured: matches,
        level: matches ? VERIFICATION_LEVELS.CONFIG_VALID : VERIFICATION_LEVELS.INSTALLED,
        requiresRestart,
        sessionHint,
        message: matches
          ? `${profile.name} has Occam registration (reload/restart may be required)`
          : `${profile.name} entry does not match stable launcher`,
      };
    },

    rollback() {
      const target = resolveTarget();
      if (!target.path) return { ok: false, error: "no config path" };
      if (lastSnap) {
        // Hosts rewrite their own config while running (observed live in Claude
        // Desktop). Restore the exact previous bytes only while the file is
        // still the one we wrote; otherwise undo just our entry so host-side
        // changes made after apply survive.
        let untouched = false;
        try {
          untouched =
            lastSnap.appliedRaw != null &&
            readFileSync(target.path, "utf8") === lastSnap.appliedRaw;
        } catch {
          untouched = false;
        }
        const current =
          lastSnap.previousMissing || untouched
            ? { ok: false }
            : loadMcpConfig(target.path, { rootKey });
        if (current.ok && current.doc) {
          const backup = backupMcpConfig(target.path);
          const servers = { ...(current.doc[rootKey] || {}) };
          if (lastSnap.previousEntry?.command) {
            servers[serverName] = codec.encode(lastSnap.previousEntry);
          } else {
            delete servers[serverName];
          }
          const restoredDoc = { ...current.doc, [rootKey]: servers };
          if (lastSnap.previousRootMissing && Object.keys(servers).length === 0) {
            delete restoredDoc[rootKey];
          }
          writeMcpConfigAtomic(target.path, restoredDoc);
          lastSnap = null;
          return { ok: true, removed: true, surgical: true, backupPath: backup.backupPath };
        }
        const r = rollbackMcpRegistration(target.path, lastSnap);
        lastSnap = null;
        return { ok: r.ok !== false, removed: true, result: r, error: r.error };
      }
      const loaded = loadMcpConfig(target.path, { rootKey });
      if (!loaded.ok) return { ok: false, error: loaded.error || "malformed config" };
      if (!inspectManagedEntry(loaded, serverName, { codec }).registered) {
        return { ok: true, removed: false };
      }
      const previousRaw = loaded.exists ? readFileSync(target.path, "utf8") : null;
      const backup = backupMcpConfig(target.path);
      const servers = { ...(loaded.doc?.[rootKey] || {}) };
      delete servers[serverName];
      const doc = { ...loaded.doc, [rootKey]: servers };
      writeMcpConfigAtomic(target.path, doc);
      lastSnap = {
        backupPath: backup.backupPath,
        previousRaw,
        previousMissing: !loaded.exists,
        rootKey,
      };
      return { ok: true, removed: true };
    },
  };
}
