/**
 * OpenClaw adapter — native outbound MCP registry CLI.
 *
 * Live findings (2026-07-25, openclaw@2026.7.1-2):
 * - Config: ~/.openclaw/openclaw.json → mcp.servers
 * - Distinguish: `openclaw mcp serve` (OpenClaw AS server) vs add/probe (AS host)
 * - `mcp add --no-probe` is non-interactive; re-add overwrites (idempotent, no duplicate)
 * - `mcp probe --json` → Level 5 (tools count)
 * - `mcp reload`: disposes cached runtimes; "Active agents use new MCP config on their next runtime build"
 *   → no full gateway restart required for next runtime build; still surface reload note
 * - `mcp unset` removes without prompt
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

export const OPENCLAW_ADAPTER_ID = "openclaw";

/**
 * Prefer PATH binary, else npx openclaw.
 * @returns {{ command: string, prefixArgs: string[], label: string }}
 */
function resolveOpenClawInvoker() {
  const bin = which("openclaw");
  if (bin) {
    return { command: bin, prefixArgs: [], label: bin };
  }
  const npx = which("npx");
  if (!npx) {
    throw new Error("openclaw not found on PATH and npx unavailable");
  }
  return {
    command: npx,
    prefixArgs: ["--yes", "openclaw"],
    label: "npx openclaw",
  };
}

export function openclawConfigPath() {
  return join(homedir(), ".openclaw", "openclaw.json");
}

/**
 * @param {string} serverName
 */
export function readOpenClawServer(serverName) {
  const path = openclawConfigPath();
  if (!existsSync(path)) return { path, entry: null, registered: false };
  try {
    const json = JSON.parse(readFileSync(path, "utf8"));
    const servers = json?.mcp?.servers ?? json?.mcpServers ?? {};
    const entry = servers?.[serverName] ?? null;
    return { path, entry, registered: Boolean(entry) };
  } catch {
    return { path, entry: null, registered: false, parseError: true };
  }
}

/**
 * @param {{ occamHome: string, serverName?: string }} ctx
 */
export function createOpenClawAdapter(ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;

  function invoker() {
    try {
      return resolveOpenClawInvoker();
    } catch {
      return null;
    }
  }

  /**
   * @param {string[]} subArgs
   * @param {{ timeoutMs?: number }} [opts]
   */
  function runOpenClaw(subArgs, opts = {}) {
    const inv = invoker();
    if (!inv) {
      return {
        status: 127,
        stdout: "",
        stderr: "openclaw not available",
        error: new Error("openclaw not available"),
      };
    }
    return runCapture(inv.command, [...inv.prefixArgs, ...subArgs], {
      timeoutMs: opts.timeoutMs ?? 120_000,
    });
  }

  /**
   * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }} desired
   * @param {{
   *   action?: string,
   *   plan?: object,
   *   reloadNote?: string,
   *   probeOnAdd?: boolean,
   *   inspect: () => { registered: boolean },
   * }} meta
   */
  function addOpenClawStdio(desired, meta) {
    /** @type {string[]} */
    const args = ["mcp", "add", serverName, "--command", desired.command];
    if (desired.cwd) {
      args.push("--cwd", desired.cwd);
    }
    for (const a of desired.args || []) {
      args.push("--arg", a);
    }
    for (const [k, v] of Object.entries(desired.env || {})) {
      args.push("--env", `${k}=${v}`);
    }
    if (meta.probeOnAdd !== true) {
      args.push("--no-probe");
    }

    const result = runOpenClaw(args, { timeoutMs: 180_000 });
    const combined = `${result.stdout}\n${result.stderr}`;
    const saved = /Saved MCP server/i.test(combined) || result.status === 0;
    if (!saved) {
      return {
        ok: false,
        applied: false,
        action: meta.action,
        error: `openclaw mcp add failed (exit ${result.status}): ${combined.trim().slice(0, 500)}`,
        plan: meta.plan,
        result,
      };
    }

    runOpenClaw(["mcp", "reload"], { timeoutMs: 30_000 });
    const after = meta.inspect();
    return {
      ok: after.registered,
      applied: true,
      action: meta.action,
      requiresRestart: false,
      reloadNote: meta.reloadNote,
      plan: meta.plan,
      result,
      inspect: after,
    };
  }

  return {
    id: OPENCLAW_ADAPTER_ID,
    name: "OpenClaw",
    kind: "MCP_HOST",
    connectionMethod: "NATIVE_CLI",
    supportTier: "A",
    maxVerificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    platforms: ["win32", "darwin", "linux"],

    detect() {
      const inv = invoker();
      const path = openclawConfigPath();
      const homeDir = join(homedir(), ".openclaw");
      const detected = Boolean(inv) || existsSync(homeDir) || existsSync(path);
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (inv && (existsSync(homeDir) || which("openclaw"))) confidence = "high";
      else if (inv) confidence = "medium";
      else if (existsSync(homeDir)) confidence = "medium";
      return {
        id: OPENCLAW_ADAPTER_ID,
        name: "OpenClaw",
        kind: "MCP_HOST",
        detected,
        confidence,
        executable: inv?.label ?? null,
        configPath: path,
      };
    },

    inspect() {
      return readOpenClawServer(serverName);
    },

    plan(opts = {}) {
      const spec = buildStableLaunchSpec(occamHome);
      const desired = stdioFromSpec(spec, { includeCwd: true });
      const current = this.inspect();
      const entry = current.entry;
      const matches =
        entry &&
        normalizePathish(entry.command || "") === normalizePathish(desired.command) &&
        argsEqual(entry.args || [], desired.args || []) &&
        envEqual(entry.env || {}, desired.env || {}) &&
        normalizePathish(entry.cwd || "") === normalizePathish(desired.cwd || "");
      const managed = looksLikeOccamManagedEntry(entry, occamHome);

      /** @type {string} */
      let action = "add";
      if (matches) action = "noop";
      else if (current.registered) {
        action = managed || opts.force ? "update" : "skip-unmanaged";
      }

      return {
        adapterId: OPENCLAW_ADAPTER_ID,
        serverName,
        connectionMethod: "NATIVE_CLI",
        canAutoConfigure: true,
        requiresRestart: false,
        reloadNote:
          "openclaw mcp reload disposes CLI cached runtimes; gateway/agent processes pick up config on next runtime build",
        verificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
        desired,
        current: entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in OpenClaw does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath: current.path,
      };
    },

    /**
     * @param {{ force?: boolean, probeOnAdd?: boolean }} [opts]
     */
    apply(opts = {}) {
      if (!invoker()) {
        return { ok: false, applied: false, error: "openclaw CLI not available" };
      }
      const inspected = this.inspect();
      if (inspected.parseError) {
        return {
          ok: false,
          applied: false,
          error: `malformed OpenClaw config at ${inspected.path} — refusing to mutate`,
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

      return addOpenClawStdio(plan.desired, {
        action: plan.action,
        plan,
        reloadNote: plan.reloadNote,
        probeOnAdd: opts.probeOnAdd === true,
        inspect: () => this.inspect(),
      });
    },

    /**
     * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }} entry
     */
    restoreEntry(entry) {
      if (!invoker()) {
        return { ok: false, error: "openclaw CLI not available" };
      }
      if (!entry?.command) {
        return { ok: false, error: "no previous entry to restore" };
      }
      return addOpenClawStdio(entry, {
        action: "restore",
        probeOnAdd: false,
        inspect: () => this.inspect(),
      });
    },

    verifyHost() {
      if (!invoker()) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.CONFIG_VALID,
          error: "openclaw CLI not available",
        };
      }
      const status = runOpenClaw(["mcp", "status"], { timeoutMs: 60_000 });
      const statusOut = `${status.stdout}\n${status.stderr}`;
      const inStatus = new RegExp(serverName).test(statusOut);

      const probe = runOpenClaw(["mcp", "probe", serverName, "--json"], { timeoutMs: 180_000 });
      const probeOut = `${probe.stdout}\n${probe.stderr}`;
      let toolCount = 0;
      let parsed = null;
      try {
        const jsonStart = probe.stdout.indexOf("{");
        if (jsonStart >= 0) {
          parsed = JSON.parse(probe.stdout.slice(jsonStart));
          const server = parsed?.servers?.[serverName];
          toolCount = Number(server?.tools ?? 0);
          if (!toolCount && Array.isArray(parsed?.tools)) {
            toolCount = parsed.tools.filter((t) =>
              String(t).startsWith(`${serverName}__`),
            ).length;
          }
        }
      } catch {
        /* fall through */
      }

      const ok = toolCount >= 15;
      return {
        ok,
        level: ok
          ? VERIFICATION_LEVELS.HOST_DISCOVERS
          : inStatus
            ? VERIFICATION_LEVELS.CONFIG_VALID
            : VERIFICATION_LEVELS.INSTALLED,
        toolCount,
        requiresRestart: false,
        message: ok
          ? "OpenClaw probe lists Occam tools"
          : "OpenClaw probe did not confirm tools",
        statusOut: statusOut.slice(0, 500),
        probeOut: probeOut.slice(0, 1000),
        parsed,
      };
    },

    rollback() {
      if (!invoker()) {
        return { ok: false, error: "openclaw CLI not available" };
      }
      if (!this.inspect().registered) {
        return { ok: true, removed: false };
      }
      const result = runOpenClaw(["mcp", "unset", serverName], { timeoutMs: 60_000 });
      const combined = `${result.stdout}\n${result.stderr}`;
      const removed = /Removed MCP server/i.test(combined) || !this.inspect().registered;
      return { ok: removed, removed, result };
    },
  };
}
