/**
 * Gemini CLI adapter — native CLI (`gemini mcp add|list|remove|enable|disable`).
 *
 * Live findings (2026-07-25, Windows):
 * - Prefer PATH `gemini`, else `npx --yes @google/gemini-cli`
 * - Scope: user | project (default project). Desktop auto-connect uses **user**.
 * - Add: `gemini mcp add -s user <name> <command> <args...> -e KEY=val`
 * - Untrusted folder: user servers listed Disabled — CONFIG_VALID + trust hint, not Ready.
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

export const GEMINI_ADAPTER_ID = "gemini";

/** Desktop one-liner scope. */
export const GEMINI_CONNECT_SCOPE = "user";

/**
 * @returns {{ command: string, prefixArgs: string[], label: string }|null}
 */
function resolveGeminiInvoker() {
  const bin = which("gemini");
  if (bin) return { command: bin, prefixArgs: [], label: bin };
  const npx = which("npx");
  if (!npx) return null;
  return {
    command: npx,
    prefixArgs: ["--yes", "@google/gemini-cli"],
    label: "npx @google/gemini-cli",
  };
}

export function geminiSettingsHintPath() {
  return join(homedir(), ".gemini");
}

/**
 * @param {string} listOut
 * @param {string} serverName
 */
export function parseGeminiListEntry(listOut, serverName) {
  const lines = listOut.split(/\r?\n/);
  const re = new RegExp(`\\b${serverName}\\b`);
  for (const line of lines) {
    if (!re.test(line)) continue;
    const disabled = /Disabled/i.test(line);
    const enabled = /Enabled/i.test(line) && !disabled;
    // Example: ○ name: node path (stdio) - Disabled
    const cmdMatch = line.match(/:\s+(\S+)\s+(.+?)\s+\(stdio\)/i);
    /** @type {{ command?: string, args?: string[], disabled: boolean, enabled: boolean, line: string }} */
    const out = { disabled, enabled: enabled || (!disabled && /stdio/i.test(line)), line };
    if (cmdMatch) {
      out.command = cmdMatch[1];
      const rest = cmdMatch[2].trim();
      out.args = rest ? rest.split(/\s+/) : [];
    }
    return out;
  }
  return null;
}

/**
 * Map Gemini inspection into the generic host verification contract.
 * Disabled means the registration is valid but blocked by host policy.
 * @param {{ registered?: boolean, disabled?: boolean, listOut?: string }} inspected
 */
export function geminiVerificationFromInspection(inspected) {
  if (!inspected.registered) {
    return {
      ok: false,
      configured: false,
      level: VERIFICATION_LEVELS.INSTALLED,
      message: "server not present in gemini mcp list",
    };
  }
  if (inspected.disabled) {
    return {
      ok: false,
      configured: true,
      requiresUserAction: true,
      hostBlocked: true,
      level: VERIFICATION_LEVELS.CONFIG_VALID,
      requiresRestart: false,
      message:
        "Gemini lists Occam but Disabled (folder may be untrusted) — trust the folder or enable the server",
      listOut: inspected.listOut,
    };
  }
  return {
    ok: true,
    configured: true,
    requiresUserAction: false,
    hostBlocked: false,
    level: VERIFICATION_LEVELS.HOST_DISCOVERS,
    requiresRestart: false,
    message: "Gemini mcp list reports Occam configured",
  };
}

/**
 * @param {{ occamHome: string, serverName?: string }} ctx
 */
export function createGeminiAdapter(ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;

  function invoker() {
    return resolveGeminiInvoker();
  }

  /**
   * @param {string[]} subArgs
   * @param {{ timeoutMs?: number }} [opts]
   */
  function runGemini(subArgs, opts = {}) {
    const inv = invoker();
    if (!inv) {
      return {
        status: 127,
        stdout: "",
        stderr: "gemini CLI not found",
        error: new Error("gemini CLI not found"),
      };
    }
    return runCapture(inv.command, [...inv.prefixArgs, ...subArgs], {
      timeoutMs: opts.timeoutMs ?? 180_000,
    });
  }

  /**
   * @param {{ command?: string, args?: string[], env?: Record<string, string> }} desired
   * @param {{ action?: string, plan?: object }} meta
   */
  function addStdio(desired, meta = {}) {
    const args = [
      "mcp",
      "add",
      "-s",
      GEMINI_CONNECT_SCOPE,
      serverName,
      desired.command,
      ...(desired.args || []),
    ];
    for (const [k, v] of Object.entries(desired.env || {})) {
      args.push("-e", `${k}=${v}`);
    }
    const result = runGemini(args, { timeoutMs: 180_000 });
    const combined = `${result.stdout}\n${result.stderr}`;
    const saved = /added to user settings/i.test(combined) || result.status === 0;
    if (!saved) {
      return {
        ok: false,
        applied: false,
        action: meta.action,
        error: `gemini mcp add failed (exit ${result.status}): ${combined.trim().slice(0, 500)}`,
        plan: meta.plan,
        result,
      };
    }
    // Best-effort enable (may still be suppressed in untrusted folders).
    runGemini(["mcp", "enable", serverName], { timeoutMs: 60_000 });
    const after = inspectGemini();
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

  function inspectGemini() {
    const path = geminiSettingsHintPath();
    if (!invoker()) {
      return { path, entry: null, registered: false };
    }
    const listed = runGemini(["mcp", "list"], { timeoutMs: 120_000 });
    const text = `${listed.stdout}\n${listed.stderr}`;
    const parsed = parseGeminiListEntry(text, serverName);
    if (!parsed) {
      return { path, entry: null, registered: false, listOut: text.slice(0, 500) };
    }
    const entry = {
      command: parsed.command,
      args: parsed.args || [],
      env: {},
    };
    return {
      path,
      entry,
      registered: true,
      disabled: parsed.disabled === true,
      enabled: parsed.enabled === true && !parsed.disabled,
      listOut: text.slice(0, 800),
      line: parsed.line,
    };
  }

  return {
    id: GEMINI_ADAPTER_ID,
    name: "Gemini CLI",
    kind: "MCP_HOST",
    connectionMethod: "NATIVE_CLI",
    supportTier: "A",
    maxVerificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    platforms: ["win32", "darwin", "linux"],

    detect() {
      const bin = which("gemini");
      const home = geminiSettingsHintPath();
      // Do not treat bare `npx` as detection — that would auto-connect every machine.
      const detected = Boolean(bin) || existsSync(home);
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (bin && existsSync(home)) confidence = "high";
      else if (bin) confidence = "medium";
      else if (existsSync(home)) confidence = "medium";
      return {
        id: GEMINI_ADAPTER_ID,
        name: "Gemini CLI",
        kind: "MCP_HOST",
        detected,
        confidence,
        executable: bin || (detected ? resolveGeminiInvoker()?.label ?? null : null),
        configPath: home,
      };
    },

    inspect() {
      return inspectGemini();
    },

    plan(opts = {}) {
      const spec = buildStableLaunchSpec(occamHome);
      const desired = stdioFromSpec(spec, { preferWrapper: false });
      const current = this.inspect();
      // List parser may not recover env — treat launcher path match as managed match when env unknown.
      const matches =
        current.entry &&
        normalizePathish(current.entry.command || "") === normalizePathish(desired.command) &&
        argsEqual(current.entry.args || [], desired.args || []) &&
        (Object.keys(current.entry.env || {}).length === 0 ||
          envEqual(current.entry.env || {}, desired.env || {}));
      const managed =
        looksLikeOccamManagedEntry(
          {
            command: current.entry?.command,
            args: current.entry?.args,
            env: current.entry?.env?.OCCAM_CONNECT_MANAGED
              ? current.entry.env
              : matches
                ? desired.env
                : current.entry?.env,
          },
          occamHome,
        ) || Boolean(matches);

      /** @type {string} */
      let action = "add";
      if (matches) action = "noop";
      else if (current.registered) {
        action = managed || opts.force ? "update" : "skip-unmanaged";
      }

      return {
        adapterId: GEMINI_ADAPTER_ID,
        serverName,
        connectionMethod: "NATIVE_CLI",
        canAutoConfigure: true,
        requiresRestart: false,
        scope: GEMINI_CONNECT_SCOPE,
        verificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
        desired,
        current: current.entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in Gemini does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath: current.path,
      };
    },

    apply(opts = {}) {
      if (!invoker()) {
        return { ok: false, applied: false, error: "gemini CLI not found" };
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
      if (!invoker()) return { ok: false, error: "gemini CLI not found" };
      if (!entry?.command) return { ok: false, error: "no previous entry to restore" };
      return addStdio(entry, { action: "restore" });
    },

    verifyHost() {
      if (!invoker()) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.CONFIG_VALID,
          error: "gemini CLI not found",
        };
      }
      const inspected = this.inspect();
      return geminiVerificationFromInspection(inspected);
    },

    rollback() {
      if (!invoker()) return { ok: false, error: "gemini CLI not found" };
      if (!this.inspect().registered) return { ok: true, removed: false };
      const result = runGemini(["mcp", "remove", "-s", GEMINI_CONNECT_SCOPE, serverName], {
        timeoutMs: 60_000,
      });
      const combined = `${result.stdout}\n${result.stderr}`;
      const removed = /removed from user settings/i.test(combined) || !this.inspect().registered;
      return { ok: removed, removed, result };
    },
  };
}
