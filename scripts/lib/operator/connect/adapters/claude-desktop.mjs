/**
 * Claude Desktop — CONFIG_FILE profile (mcpServers).
 *
 * Paths:
 * - macOS: ~/Library/Application Support/Claude/claude_desktop_config.json
 * - Windows classic: %APPDATA%\\Claude\\claude_desktop_config.json
 * - Windows MSIX: %LOCALAPPDATA%\\Packages\\Claude_*\\LocalCache\\Roaming\\Claude\\...
 * - Linux: ~/.config/Claude/claude_desktop_config.json
 *
 * Never writes both Windows paths. Ambiguous → action required.
 */
import { existsSync, readdirSync } from "node:fs";
import { platform } from "node:os";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import {
  appDataDir,
  dirHasEntries,
  listWindowsClaudeMsixConfigs,
  localAppDataDir,
  resolveUniqueConfigPath,
} from "../paths.mjs";

export const CLAUDE_DESKTOP_ADAPTER_ID = "claude-desktop";
export const CLAUDE_DESKTOP_ROOT_KEY = "mcpServers";

export function claudeDesktopCandidatePaths() {
  /** @type {string[]} */
  const out = [];
  if (platform() === "win32") {
    out.push(join(appDataDir(), "Claude", "claude_desktop_config.json"));
    out.push(...listWindowsClaudeMsixConfigs());
  } else if (platform() === "darwin") {
    out.push(join(appDataDir(), "Claude", "claude_desktop_config.json"));
  } else {
    out.push(join(appDataDir(), "Claude", "claude_desktop_config.json"));
  }
  return out;
}

/**
 * Evidence that the Claude Desktop app itself is installed (not Claude Code CLI).
 * @returns {string[]}
 */
export function claudeDesktopAppSignals() {
  /** @type {string[]} */
  const signals = [];
  if (platform() === "darwin") {
    if (existsSync("/Applications/Claude.app")) signals.push("/Applications/Claude.app");
  }
  if (platform() === "win32") {
    const packages = join(localAppDataDir(), "Packages");
    if (existsSync(packages)) {
      try {
        for (const name of readdirSync(packages)) {
          if (/^Claude_/i.test(name)) signals.push(join(packages, name));
        }
      } catch {
        /* ignore */
      }
    }
  }
  const roaming = join(appDataDir(), "Claude");
  if (dirHasEntries(roaming)) signals.push(roaming);
  return signals;
}

export function resolveClaudeDesktopConfigTarget() {
  const resolved = resolveUniqueConfigPath(claudeDesktopCandidatePaths());
  if (resolved.ambiguous) {
    return {
      ...resolved,
      reason:
        "Multiple Claude Desktop config locations found (classic vs MSIX). Open Settings → Developer → Edit Config, or set an explicit path.",
    };
  }
  return resolved;
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createClaudeDesktopAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: CLAUDE_DESKTOP_ADAPTER_ID,
      name: "Claude Desktop",
      kind: "MCP_HOST",
      supportTier: "A",
      rootKey: CLAUDE_DESKTOP_ROOT_KEY,
        requiresRestart: true,
        sessionHint: "Fully quit and relaunch Claude Desktop to load Occam",
        // Claude Desktop rewrites the config on launch and drops `cwd`; writing it
        // would make every later connect run see a diff and re-apply. OCCAM_HOME
        // already carries the working directory.
        includeCwd: false,
      resolveConfigTarget: () => resolveClaudeDesktopConfigTarget(),
      detectExtra() {
        // `claude` on PATH is Claude Code, not Claude Desktop — never use it as a signal.
        const target = resolveClaudeDesktopConfigTarget();
        const appInstalled = claudeDesktopAppSignals();
        const detected =
          target.existing.length > 0 || target.ambiguous === true || appInstalled.length > 0;
        return {
          detected,
          confidence:
            target.existing.length === 1
              ? "high"
              : target.ambiguous || appInstalled.length > 0
                ? "medium"
                : "low",
          executable: null,
          signals: [...appInstalled, ...target.existing],
        };
      },
    },
    ctx,
  );
}
