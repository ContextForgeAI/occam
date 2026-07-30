/**
 * Hermes Agent adapter — native CLI first (`hermes mcp add|list|test|remove`).
 *
 * Live findings (2026-07-25, Windows):
 * - Config: %LOCALAPPDATA%\\hermes\\config.yaml (HERMES_HOME / platform default)
 * - `hermes mcp add` always probes then prompts "Enable all N tools? [Y/n/select]"
 *   → non-interactive: pipe "Y\\n"
 * - Existing name prompts "Overwrite? [y/N]" (default N)
 *   → update: pipe "y\\nY\\n"
 * - EOF on tool prompt cancels without save
 * - `hermes mcp test <name>` → Level 5 (connect + tools)
 * - After add: new Hermes session required for in-chat tools (requiresRestart)
 * - Re-add without overwrite confirm → no duplicate (cancelled)
 */
import { existsSync, readFileSync } from "node:fs";
import { homedir, platform } from "node:os";
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

export const HERMES_ADAPTER_ID = "hermes";

/**
 * @returns {{ command: string, prefixArgs: string[], label: string }|null}
 */
function resolveHermesInvoker() {
  const bin = which("hermes");
  if (!bin) return null;
  return { command: bin, prefixArgs: [], label: bin };
}

/**
 * @returns {string}
 */
export function resolveHermesHome() {
  const fromEnv = process.env.HERMES_HOME?.trim();
  if (fromEnv) return fromEnv;
  if (platform() === "win32") {
    const base = process.env.LOCALAPPDATA?.trim() || join(homedir(), "AppData", "Local");
    return join(base, "hermes");
  }
  return join(homedir(), ".hermes");
}

/**
 * @returns {string}
 */
export function hermesConfigPath() {
  return join(resolveHermesHome(), "config.yaml");
}

/**
 * Minimal YAML extract for mcp_servers.<name> stdio fields (no full YAML parser dependency).
 * Sufficient for Occam-owned entries we write via Hermes CLI.
 * @param {string} yaml
 * @param {string} serverName
 */
export function parseHermesMcpServer(yaml, serverName) {
  if (!yaml || !serverName) return null;
  const lines = yaml.split(/\r?\n/);
  let inMcp = false;
  let inServer = false;
  let serverIndent = -1;
  /** @type {{ command?: string, args: string[], env: Record<string, string>, enabled?: boolean }} */
  const out = { args: [], env: {} };
  let inArgs = false;
  let inEnv = false;
  let argsIndent = -1;
  let envIndent = -1;

  for (const line of lines) {
    if (/^\s*#/.test(line) || !line.trim()) {
      continue;
    }
    const indent = line.match(/^ */)?.[0].length ?? 0;
    const trimmed = line.trim();

    if (/^mcp_servers:\s*$/.test(trimmed)) {
      inMcp = true;
      inServer = false;
      continue;
    }
    if (!inMcp) continue;

    if (inMcp && indent === 0 && /:$/.test(trimmed) && !trimmed.startsWith("mcp_servers")) {
      break;
    }

    if (inMcp && !inServer && indent > 0) {
      const m = trimmed.match(/^([A-Za-z0-9_.-]+):\s*$/);
      if (m) {
        if (m[1] === serverName) {
          inServer = true;
          serverIndent = indent;
          inArgs = false;
          inEnv = false;
          continue;
        }
        if (inServer && indent <= serverIndent) {
          break;
        }
        if (m[1] !== serverName && indent === serverIndent) {
          // sibling server
          if (inServer) break;
        }
      }
    }

    if (!inServer) continue;

    if (indent <= serverIndent && trimmed.endsWith(":") && !trimmed.startsWith("command") && !trimmed.startsWith("args") && !trimmed.startsWith("env")) {
      break;
    }

    if (inArgs && indent > argsIndent) {
      const am = trimmed.match(/^-\s*(.+)$/);
      if (am) {
        out.args.push(stripQuotes(am[1]));
        continue;
      }
      inArgs = false;
    }

    if (inEnv && indent > envIndent) {
      const em = trimmed.match(/^([A-Za-z_][A-Za-z0-9_]*)\s*:\s*(.*)$/);
      if (em) {
        out.env[em[1]] = stripQuotes(em[2]);
        continue;
      }
      inEnv = false;
    }

    const cmd = trimmed.match(/^command:\s*(.+)$/);
    if (cmd) {
      out.command = stripQuotes(cmd[1]);
      inArgs = false;
      inEnv = false;
      continue;
    }
    if (/^args:\s*$/.test(trimmed)) {
      inArgs = true;
      inEnv = false;
      argsIndent = indent;
      continue;
    }
    if (/^args:\s*\[/.test(trimmed)) {
      // inline list — rare; ignore for equality fallback
      continue;
    }
    if (/^env:\s*$/.test(trimmed)) {
      inEnv = true;
      inArgs = false;
      envIndent = indent;
      continue;
    }
    const en = trimmed.match(/^enabled:\s*(.+)$/);
    if (en) {
      out.enabled = !/^false$/i.test(stripQuotes(en[1]));
    }
  }

  if (!out.command) return null;
  return out;
}

/** @param {string} v */
function stripQuotes(v) {
  const s = v.trim();
  if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
    return s.slice(1, -1);
  }
  return s;
}

/**
 * @param {{ occamHome: string, serverName?: string }} ctx
 */
export function createHermesAdapter(ctx) {
  const serverName = ctx.serverName || OCCAM_MCP_SERVER_NAME;
  const occamHome = ctx.occamHome;

  /**
   * @param {{ command: string, prefixArgs: string[] }} inv
   * @param {{ command?: string, args?: string[], env?: Record<string, string> }} desired
   * @param {{
   *   overwrite?: boolean,
   *   action?: string,
   *   plan?: object,
   *   sessionHint?: string|null,
   *   inspect: () => { registered: boolean, entry?: object|null },
   * }} meta
   */
  function addHermesStdio(inv, desired, meta) {
    const args = [
      ...inv.prefixArgs,
      "mcp",
      "add",
      serverName,
      "--command",
      desired.command,
    ];
    // Hermes requires `--args` to be the last option; put `--env` before it.
    const envPairs = Object.entries(desired.env || {}).map(([k, v]) => `${k}=${v}`);
    if (envPairs.length) {
      args.push("--env", ...envPairs);
    }
    if (desired.args?.length) {
      args.push("--args", ...desired.args);
    }

    const input = meta.overwrite ? "y\nY\n" : "Y\n";
    const result = runCapture(inv.command, args, { input, timeoutMs: 180_000 });
    const combined = `${result.stdout}\n${result.stderr}`;
    const saved = /Saved '/i.test(combined) || /tools enabled/i.test(combined);
    if (!saved) {
      return {
        ok: false,
        applied: false,
        action: meta.action,
        error: `hermes mcp add failed (exit ${result.status}): ${combined.trim().slice(0, 500)}`,
        plan: meta.plan,
        result,
      };
    }

    const after = meta.inspect();
    return {
      ok: after.registered,
      applied: true,
      action: meta.action,
      requiresRestart: false,
      sessionHint: meta.sessionHint ?? null,
      plan: meta.plan,
      result,
      inspect: after,
    };
  }

  return {
    id: HERMES_ADAPTER_ID,
    name: "Hermes Agent",
    kind: "MCP_HOST",
    connectionMethod: "NATIVE_CLI",
    supportTier: "A",
    maxVerificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
    platforms: ["win32", "darwin", "linux"],

    detect() {
      const inv = resolveHermesInvoker();
      const configPath = hermesConfigPath();
      const homeDir = resolveHermesHome();
      const homeExists = existsSync(homeDir);
      const configExists = existsSync(configPath);
      // STRONG only: usable `hermes` on PATH. Stale ~/.hermes alone is not connectable.
      const detected = Boolean(inv);
      const residue = !inv && (homeExists || configExists);
      /** @type {'high'|'medium'|'low'} */
      let confidence = "low";
      if (inv && (homeExists || configExists)) confidence = "high";
      else if (inv) confidence = "medium";
      return {
        id: HERMES_ADAPTER_ID,
        name: "Hermes Agent",
        kind: "MCP_HOST",
        detected,
        confidence,
        executable: inv?.label ?? null,
        configPath,
        residue,
        residueSignals: residue
          ? [
              ...(homeExists ? [`dir:${homeDir}`] : []),
              ...(configExists ? [`config:${configPath}`] : []),
            ]
          : [],
      };
    },

    inspect() {
      const path = hermesConfigPath();
      if (!existsSync(path)) {
        return { registered: false, configPath: path, entry: null };
      }
      const yaml = readFileSync(path, "utf8");
      const entry = parseHermesMcpServer(yaml, serverName);
      return {
        registered: Boolean(entry),
        configPath: path,
        entry,
      };
    },

    plan(opts = {}) {
      const spec = buildStableLaunchSpec(occamHome);
      const desired = stdioFromSpec(spec, { preferWrapper: true });
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
        adapterId: HERMES_ADAPTER_ID,
        serverName,
        connectionMethod: "NATIVE_CLI",
        canAutoConfigure: true,
        // Chat session may need refresh; Level 5 via mcp test does not block Ready.
        requiresRestart: false,
        sessionHint: "new Hermes session recommended to use tools in chat",
        verificationLevel: VERIFICATION_LEVELS.HOST_DISCOVERS,
        desired,
        current: current.entry,
        managed,
        action,
        skipReason:
          action === "skip-unmanaged"
            ? `Existing ${serverName} in Hermes does not look Occam-managed; pass --force to overwrite`
            : null,
        configPath: current.configPath,
      };
    },

    /**
     * @param {{ force?: boolean }} [opts]
     */
    apply(opts = {}) {
      const inv = resolveHermesInvoker();
      if (!inv) {
        return { ok: false, applied: false, error: "hermes CLI not found on PATH" };
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

      return addHermesStdio(inv, plan.desired, {
        overwrite: plan.action === "update" || opts.force,
        action: plan.action,
        plan,
        sessionHint: plan.sessionHint,
        inspect: () => this.inspect(),
      });
    },

    /**
     * Restore a previous stdio entry after a failed update verify (best-effort).
     * @param {{ command?: string, args?: string[], env?: Record<string, string> }} entry
     */
    restoreEntry(entry) {
      const inv = resolveHermesInvoker();
      if (!inv) {
        return { ok: false, error: "hermes CLI not found" };
      }
      if (!entry?.command) {
        return { ok: false, error: "no previous entry to restore" };
      }
      return addHermesStdio(inv, entry, {
        overwrite: true,
        action: "restore",
        inspect: () => this.inspect(),
      });
    },

    verifyHost() {
      const inv = resolveHermesInvoker();
      if (!inv) {
        return {
          ok: false,
          level: VERIFICATION_LEVELS.CONFIG_VALID,
          error: "hermes CLI not found",
        };
      }
      const listed = runCapture(inv.command, [...inv.prefixArgs, "mcp", "list"], {
        timeoutMs: 60_000,
      });
      const listOut = `${listed.stdout}\n${listed.stderr}`;
      const inList = new RegExp(`\\b${serverName}\\b`).test(listOut);
      if (!inList) {
        return {
          ok: false,
          level: this.inspect().registered
            ? VERIFICATION_LEVELS.CONFIG_VALID
            : VERIFICATION_LEVELS.INSTALLED,
          error: "server not present in hermes mcp list",
          listOut,
        };
      }

      const tested = runCapture(inv.command, [...inv.prefixArgs, "mcp", "test", serverName], {
        timeoutMs: 180_000,
      });
      const testOut = `${tested.stdout}\n${tested.stderr}`;
      const toolsMatch = testOut.match(/Tools discovered:\s*(\d+)/i);
      const toolCount = toolsMatch ? Number(toolsMatch[1]) : 0;
      const connected = /Connected/i.test(testOut) && toolCount > 0;
      return {
        ok: connected,
        level: connected ? VERIFICATION_LEVELS.HOST_DISCOVERS : VERIFICATION_LEVELS.CONFIG_VALID,
        toolCount,
        // mcp test already proved discovery; session hint is advisory only.
        requiresRestart: false,
        sessionHint: connected ? "new Hermes session recommended to use tools in chat" : null,
        message: connected
          ? "Hermes mcp test connected"
          : "Hermes list shows server but test failed",
        testOut: testOut.slice(0, 1000),
      };
    },

    rollback() {
      const inv = resolveHermesInvoker();
      if (!inv) {
        return { ok: false, error: "hermes CLI not found" };
      }
      if (!this.inspect().registered) {
        return { ok: true, removed: false };
      }
      // Remove confirm default Y
      const result = runCapture(inv.command, [...inv.prefixArgs, "mcp", "remove", serverName], {
        input: "Y\n",
        timeoutMs: 60_000,
      });
      const combined = `${result.stdout}\n${result.stderr}`;
      const removed = /Removed '/i.test(combined) || !this.inspect().registered;
      return { ok: removed, removed, result };
    },
  };
}
