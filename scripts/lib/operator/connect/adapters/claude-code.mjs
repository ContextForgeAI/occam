/**
 * Claude Code adapter — native CLI (`claude mcp add|list|get|remove`).
 *
 * Live findings (2026-07-25, Windows):
 * - Scope: local | user | project (default local). Desktop auto-connect uses **user**.
 * - Add: `claude mcp add -s user <name> -e KEY=val -- <command> <args...>` (`--` required)
 * - User storage: ~/.claude.json → mcpServers.<name>
 * - Verify L5: `claude mcp get <name>` → Status: √ Connected
 * - PowerShell swallows `--`; always use argv runCapture.
 */
import { existsSync, readFileSync } from "node:fs";
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

export const CLAUDE_CODE_ADAPTER_ID = "claude-code";

/** Desktop one-liner scope — available in all projects. */
export const CLAUDE_CONNECT_SCOPE = "user";

/**
 * @returns {{ command: string, prefixArgs: string[], label: string }|null}
 */
function resolveClaudeInvoker() {
  const bin = which("claude");
  if (!bin) return null;
  return { command: bin, prefixArgs: [], label: bin };
}

export function claudeUserConfigPath() {
  return join(homedir(), ".claude.json");
}

/**
 * @param {string} serverName
 */
export function readClaudeUserServer(serverName) {
  const path = claudeUserConfigPath();
  if (!existsSync(path)) return { path, entry: null, registered: false };
  try {
    const json = JSON.parse(readFileSync(path, "utf8"));
    const entry = json?.mcpServers?.[serverName] ?? null;
    return { path, entry, registered: Boolean(entry) };
  } catch {
    return { path, entry: null, registered: false, parseError: true };
  }
}

/**
 * @param {string} text
 */
export function parseClaudeGetConnected(text) {
  return /Status:\s*.*Connected/i.test(text) || /√\s*Connected/i.test(text);
}

/**
 * @param {{ occamHome: string, serverName?: string }} ctx
 */
export function createClaudeCodeAdapter(ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;

  function invoker() {
    return resolveClaudeInvoker();
  }

  /**
   * @param {string[]} subArgs
   * @param {{ timeoutMs?: number }} [opts]
   */
  function runClaude(subArgs, opts = {}) {
    const inv = invoker();
    if (!inv) {
      return {
        status: 127,
        stdout: "",
        stderr: "claude CLI not found",
        error: new Error("claude CLI not found"),
      };
    }
    return runCapture(inv.command, [...inv.prefixArgs, ...subArgs], {
      timeoutMs: opts.timeoutMs ?? 120_000,
    });
  }

  /**
   * @param {{ command?: string, args?: string[], env?: Record<string, string> }} desired
   * @param {{ action?: string, plan?: object }} meta
   */
  function addStdio(desired, meta = {}) {
    const args = ["mcp", "add", "-s", CLAUDE_CONNECT_SCOPE, serverName];
    for (const [k, v] of Object.entries(desired.env || {})) {
      args.push("-e", `${k}=${v}`);
    }
    args.push("--", desired.command, ...(desired.args || []));
    const result = runClaude(args, { timeoutMs: 180_000 });
    const combined = `${result.stdout}\n${result.stderr}`;
    const alreadyExists = /already exists/i.test(combined);
    const saved = /Added stdio MCP server/i.test(combined) || result.status === 0;
    if (!saved && alreadyExists) {
      // Existing registration is often the desired end state — inspect before failing.
      const after = readClaudeUserServer(serverName);
      if (after.registered) {
        const desired = meta.plan?.desired;
        const matches =
          desired &&
          after.entry &&
          normalizePathish(after.entry.command || "") === normalizePathish(desired.command || "") &&
          argsEqual(after.entry.args || [], desired.args || []) &&
          envEqual(after.entry.env || {}, desired.env || {});
        const managed = looksLikeOccamManagedEntry(after.entry, occamHome);
        if (matches || managed) {
          return {
            ok: true,
            applied: false,
            action: "noop",
            requiresRestart: false,
            plan: meta.plan,
            result,
            inspect: after,
          };
        }
        return {
          ok: false,
          applied: false,
          action: meta.action,
          requiresUserAction: true,
          error:
            "Claude Code already has an Occam-named server with a different configuration — review it or re-run with --force",
          plan: meta.plan,
          result,
          inspect: after,
        };
      }
    }
    if (!saved) {
      return {
        ok: false,
        applied: false,
        action: meta.action,
        error: `claude mcp add failed (exit ${result.status}): ${combined.trim().slice(0, 500)}`,
        plan: meta.plan,
        result,
      };
    }
    const after = readClaudeUserServer(serverName);
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

  return {
    id: CLAUDE_CODE_ADAPTER_ID,
    name: "Claude Code",
    kind: "MCP_HOST",
    connectionMethod: "NATIVE_CLI",
    supportTier: "A",
    maxVerificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    platforms: ["win32", "darwin", "linux"],

    detect() {
      const inv = invoker();
      const path = claudeUserConfigPath();
      const detected = Boolean(inv) || existsSync(path);
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (inv && existsSync(path)) confidence = "high";
      else if (inv) confidence = "medium";
      else if (existsSync(path)) confidence = "medium";
      return {
        id: CLAUDE_CODE_ADAPTER_ID,
        name: "Claude Code",
        kind: "MCP_HOST",
        detected,
        confidence,
        executable: inv?.label ?? null,
        configPath: path,
      };
    },

    inspect() {
      return readClaudeUserServer(serverName);
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
        adapterId: CLAUDE_CODE_ADAPTER_ID,
        serverName,
        connectionMethod: "NATIVE_CLI",
        canAutoConfigure: true,
        requiresRestart: false,
        scope: CLAUDE_CONNECT_SCOPE,
        verificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
        desired,
        current: current.entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in Claude Code user config does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath: current.path,
      };
    },

    apply(opts = {}) {
      if (!invoker()) {
        return { ok: false, applied: false, error: "claude CLI not found on PATH" };
      }
      const inspected = this.inspect();
      if (inspected.parseError) {
        return {
          ok: false,
          applied: false,
          error: `malformed Claude config at ${inspected.path} — refusing to mutate`,
        };
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
      // Claude re-add updates in place (no separate overwrite prompt for user scope).
      return addStdio(plan.desired, { action: plan.action, plan });
    },

    restoreEntry(entry) {
      if (!invoker()) return { ok: false, error: "claude CLI not found" };
      if (!entry?.command) return { ok: false, error: "no previous entry to restore" };
      return addStdio(entry, { action: "restore" });
    },

    verifyHost() {
      if (!invoker()) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.CONFIG_VALID,
          error: "claude CLI not found",
        };
      }
      const got = runClaude(["mcp", "get", serverName], { timeoutMs: 120_000 });
      const text = `${got.stdout}\n${got.stderr}`;
      if (/No MCP server found/i.test(text) || got.status !== 0) {
        return {
          ok: false,
          level: this.inspect().registered
            ? VERIFICATION_LEVELS.CONFIG_VALID
            : VERIFICATION_LEVELS.INSTALLED,
          message: "server not present in claude mcp get",
          text: text.slice(0, 500),
        };
      }
      const connected = parseClaudeGetConnected(text);
      return {
        ok: connected,
        level: connected ? VERIFICATION_LEVELS.HOST_DISCOVERS : VERIFICATION_LEVELS.CONFIG_VALID,
        requiresRestart: false,
        message: connected
          ? "Claude Code mcp get reports Connected"
          : "Claude Code lists server but not Connected",
        text: text.slice(0, 800),
      };
    },

    rollback() {
      if (!invoker()) return { ok: false, error: "claude CLI not found" };
      if (!this.inspect().registered) return { ok: true, removed: false };
      const result = runClaude(["mcp", "remove", "-s", CLAUDE_CONNECT_SCOPE, serverName], {
        timeoutMs: 60_000,
      });
      const combined = `${result.stdout}\n${result.stderr}`;
      const removed = /Removed MCP server/i.test(combined) || !this.inspect().registered;
      return { ok: removed, removed, result };
    },
  };
}
