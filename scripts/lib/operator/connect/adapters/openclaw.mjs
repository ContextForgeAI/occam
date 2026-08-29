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
 *
 * Detection honesty (2026-07-30 friend false-positive):
 * - NEVER treat `npx openclaw` as installed OpenClaw — every Node machine has npx.
 * - NEVER treat bare ~/.openclaw residue as a connectable host.
 * - Connectable = usable `openclaw` executable on PATH (STRONG signal only).
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
import {
  evaluateOccamToolList,
  REQUIRED_BASELINE_TOOLS,
} from "../occam-verify.mjs";
import { VERIFICATION_LEVELS } from "../verification.mjs";

export const OPENCLAW_ADAPTER_ID = "openclaw";

/**
 * Usable OpenClaw CLI only — never npx fallback (npx would false-detect every Node install).
 * @returns {{ command: string, prefixArgs: string[], label: string }|null}
 */
export function resolveOpenClawInvoker() {
  const bin = which("openclaw");
  if (!bin) return null;
  return { command: bin, prefixArgs: [], label: bin };
}

export function openclawConfigPath() {
  return join(homedir(), ".openclaw", "openclaw.json");
}

export function openclawHomeDir() {
  return join(homedir(), ".openclaw");
}

/**
 * Stale config/home without a usable CLI — diagnostics only, never auto-connect.
 * @returns {{ residue: boolean, signals: string[] }}
 */
export function describeOpenClawResidue() {
  /** @type {string[]} */
  const signals = [];
  const homeDir = openclawHomeDir();
  const path = openclawConfigPath();
  if (existsSync(homeDir)) signals.push(`dir:${homeDir}`);
  if (existsSync(path)) signals.push(`config:${path}`);
  return { residue: signals.length > 0 && !which("openclaw"), signals };
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
    return resolveOpenClawInvoker();
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
      const homeDir = openclawHomeDir();
      const residue = describeOpenClawResidue();
      // STRONG only: usable openclaw on PATH. Residue alone is never connectable.
      const detected = Boolean(inv);
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (inv && (existsSync(homeDir) || existsSync(path))) confidence = "high";
      else if (inv) confidence = "medium";
      return {
        id: OPENCLAW_ADAPTER_ID,
        name: "OpenClaw",
        kind: "MCP_HOST",
        detected,
        confidence,
        executable: inv?.label ?? null,
        configPath: path,
        residue: residue.residue,
        residueSignals: residue.signals,
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
        return {
          ok: false,
          applied: false,
          error:
            "OpenClaw CLI not found on PATH (stale ~/.openclaw alone is not enough)",
        };
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
          level: VERIFICATION_LEVELS.INSTALLED,
          error:
            "OpenClaw CLI not found on PATH — cannot verify (config residue is not a live host)",
        };
      }
      const status = runOpenClaw(["mcp", "status"], { timeoutMs: 60_000 });
      const statusOut = `${status.stdout}\n${status.stderr}`;
      const inStatus = new RegExp(serverName).test(statusOut);

      const probe = runOpenClaw(["mcp", "probe", serverName, "--json"], { timeoutMs: 180_000 });
      const probeOut = `${probe.stdout}\n${probe.stderr}`;
      let toolCount = 0;
      let parsed = null;
      let ok = false;
      try {
        const jsonStart = probe.stdout.indexOf("{");
        if (jsonStart >= 0) {
          parsed = JSON.parse(probe.stdout.slice(jsonStart));
          const server = parsed?.servers?.[serverName];
          const prefix = `${serverName}__`;
          const namedFrom = (entries) =>
            entries
              .map((entry) => {
                if (typeof entry === "string") {
                  return entry.startsWith(prefix)
                    ? entry.slice(prefix.length)
                    : entry;
                }
                if (entry && typeof entry === "object" && typeof entry.name === "string") {
                  const name = entry.name;
                  return name.startsWith(prefix) ? name.slice(prefix.length) : name;
                }
                return null;
              })
              .filter((name) => typeof name === "string" && name.length > 0)
              .map((name) => ({ name }));

          if (Array.isArray(server?.tools)) {
            const evaluated = evaluateOccamToolList(namedFrom(server.tools));
            toolCount = evaluated.toolCount;
            ok = evaluated.ok;
          } else if (typeof server?.tools === "number") {
            toolCount = server.tools;
            ok = toolCount >= REQUIRED_BASELINE_TOOLS.length;
          } else if (Array.isArray(parsed?.tools)) {
            const evaluated = evaluateOccamToolList(namedFrom(parsed.tools));
            toolCount = evaluated.toolCount;
            ok = evaluated.ok;
          }
        }
      } catch {
        /* fall through */
      }

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
          : "OpenClaw probe did not confirm required Occam tools",
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
