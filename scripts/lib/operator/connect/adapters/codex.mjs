/**
 * Codex CLI adapter — native CLI (`codex mcp add|list|get|remove`).
 *
 * Live findings (2026-07-25, Windows):
 * - Global MCP registry (no user/project scope flag)
 * - Add: `codex mcp add <name> --env KEY=VAL -- <command> <args...>`
 * - Inspect: `codex mcp get <name> --json`
 * - List: `codex mcp list --json`
 * - Verify: get JSON enabled + transport present (host discovers registration)
 */
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { OCCAM_MCP_SERVER_NAME } from "../kinds.mjs";
import { buildStableLaunchSpec, stdioFromSpec } from "../launch-spec.mjs";
import { looksLikeOccamManagedEntry } from "../ownership.mjs";
import {
  argsEqual,
  envEqual,
  normalizePathish,
  runCapture,
  which,
} from "../process.mjs";
import { VERIFICATION_LEVELS } from "../verification.mjs";

export const CODEX_ADAPTER_ID = "codex";

/**
 * @returns {{ command: string, prefixArgs: string[], label: string }|null}
 */
function resolveCodexInvoker() {
  const bin = which("codex");
  if (!bin) return null;
  return { command: bin, prefixArgs: [], label: bin };
}

export function codexHomeDir() {
  return join(homedir(), ".codex");
}

/**
 * @param {string} text
 */
export function parseCodexGetJson(text) {
  const start = text.indexOf("{");
  if (start < 0) return null;
  try {
    return JSON.parse(text.slice(start));
  } catch {
    return null;
  }
}

/**
 * @param {object|null} json
 */
export function codexJsonToEntry(json) {
  if (!json?.transport) return null;
  const t = json.transport;
  return {
    command: t.command,
    args: t.args || [],
    env: t.env || {},
    cwd: t.cwd || undefined,
  };
}

/**
 * @param {{ occamHome: string, serverName?: string }} ctx
 */
export function createCodexAdapter(ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;

  function invoker() {
    return resolveCodexInvoker();
  }

  /**
   * @param {string[]} subArgs
   * @param {{ timeoutMs?: number }} [opts]
   */
  function runCodex(subArgs, opts = {}) {
    const inv = invoker();
    if (!inv) {
      return {
        status: 127,
        stdout: "",
        stderr: "codex CLI not found",
        error: new Error("codex CLI not found"),
      };
    }
    return runCapture(inv.command, [...inv.prefixArgs, ...subArgs], {
      timeoutMs: opts.timeoutMs ?? 120_000,
    });
  }

  /**
   * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }} desired
   * @param {{ action?: string, plan?: object }} meta
   */
  function addStdio(desired, meta = {}) {
    const args = ["mcp", "add", serverName];
    for (const [k, v] of Object.entries(desired.env || {})) {
      args.push("--env", `${k}=${v}`);
    }
    args.push("--", desired.command, ...(desired.args || []));
    const result = runCodex(args, { timeoutMs: 180_000 });
    const combined = `${result.stdout}\n${result.stderr}`;
    const saved = /Added global MCP server/i.test(combined) || result.status === 0;
    if (!saved) {
      return {
        ok: false,
        applied: false,
        action: meta.action,
        error: `codex mcp add failed (exit ${result.status}): ${combined.trim().slice(0, 500)}`,
        plan: meta.plan,
        result,
      };
    }
    const after = inspectCodex();
    return {
      ok: after.registered,
      applied: true,
      action: meta.action,
      requiresRestart: false,
      plan: meta.plan,
      result,
      inspect: after,
    };
  }

  function inspectCodex() {
    if (!invoker()) {
      return { path: codexHomeDir(), entry: null, registered: false };
    }
    const got = runCodex(["mcp", "get", serverName, "--json"], { timeoutMs: 60_000 });
    const json = parseCodexGetJson(`${got.stdout}\n${got.stderr}`);
    if (!json || got.status !== 0) {
      return { path: codexHomeDir(), entry: null, registered: false, raw: got };
    }
    const entry = codexJsonToEntry(json);
    return {
      path: codexHomeDir(),
      entry,
      registered: Boolean(entry),
      enabled: json.enabled === true,
      json,
    };
  }

  return {
    id: CODEX_ADAPTER_ID,
    name: "Codex CLI",
    kind: "MCP_HOST",
    connectionMethod: "NATIVE_CLI",
    supportTier: "A",
    maxVerificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    platforms: ["win32", "darwin", "linux"],

    detect() {
      const inv = invoker();
      const home = codexHomeDir();
      const detected = Boolean(inv) || existsSync(home);
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (inv && existsSync(home)) confidence = "high";
      else if (inv) confidence = "medium";
      else if (existsSync(home)) confidence = "medium";
      return {
        id: CODEX_ADAPTER_ID,
        name: "Codex CLI",
        kind: "MCP_HOST",
        detected,
        confidence,
        executable: inv?.label ?? null,
        configPath: home,
      };
    },

    inspect() {
      return inspectCodex();
    },

    plan(opts = {}) {
      const spec = buildStableLaunchSpec(occamHome);
      const desired = stdioFromSpec(spec, { preferWrapper: false });
      const current = this.inspect();
      const matches =
        current.entry &&
        normalizePathish(current.entry.command || "") === normalizePathish(desired.command) &&
        argsEqual(current.entry.args || [], desired.args || []) &&
        envEqual(current.entry.env || {}, desired.env || {});
      const managed = looksLikeOccamManagedEntry(current.entry, occamHome);

      /** @type {string} */
      let action = "add";
      if (matches) action = "noop";
      else if (current.registered) {
        action = managed || opts.force ? "update" : "skip-unmanaged";
      }

      return {
        adapterId: CODEX_ADAPTER_ID,
        serverName,
        connectionMethod: "NATIVE_CLI",
        canAutoConfigure: true,
        requiresRestart: false,
        verificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
        desired,
        current: current.entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in Codex does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath: current.path,
      };
    },

    apply(opts = {}) {
      if (!invoker()) {
        return { ok: false, applied: false, error: "codex CLI not found on PATH" };
      }
      const plan = this.plan({ force: opts.force });
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
        return { ok: true, applied: false, action: "noop", plan };
      }
      return addStdio(plan.desired, { action: plan.action, plan });
    },

    restoreEntry(entry) {
      if (!invoker()) return { ok: false, error: "codex CLI not found" };
      if (!entry?.command) return { ok: false, error: "no previous entry to restore" };
      return addStdio(entry, { action: "restore" });
    },

    verifyHost() {
      if (!invoker()) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.CONFIG_VALID,
          error: "codex CLI not found",
        };
      }
      const inspected = this.inspect();
      if (!inspected.registered) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.INSTALLED,
          message: "server not present in codex mcp get",
        };
      }
      const enabled = inspected.enabled !== false;
      return {
        ok: enabled,
        level: enabled ? VERIFICATION_LEVELS.HOST_DISCOVERS : VERIFICATION_LEVELS.CONFIG_VALID,
        requiresRestart: false,
        message: enabled
          ? "Codex mcp get lists Occam enabled"
          : "Codex lists Occam but disabled",
      };
    },

    rollback() {
      if (!invoker()) return { ok: false, error: "codex CLI not found" };
      if (!this.inspect().registered) return { ok: true, removed: false };
      const result = runCodex(["mcp", "remove", serverName], { timeoutMs: 60_000 });
      const combined = `${result.stdout}\n${result.stderr}`;
      const removed = /Removed global MCP server/i.test(combined) || !this.inspect().registered;
      return { ok: removed, removed, result };
    },
  };
}
