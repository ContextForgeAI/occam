/**
 * VS Code / GitHub Copilot MCP — user profile mcp.json with root key `servers`.
 * Auto-connect writes user config only (never workspace .vscode/mcp.json).
 */
import { existsSync, readdirSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { VSCODE_ENTRY_CODEC } from "../codecs.mjs";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { appDataDir, dirHasEntries, resolveUniqueConfigPath } from "../paths.mjs";
import { which } from "../process.mjs";

export const VSCODE_ADAPTER_ID = "vscode";
export const VSCODE_ROOT_KEY = "servers";

export function vscodeUserConfigPath() {
  return join(appDataDir(), "Code", "User", "mcp.json");
}

export function vscodeInsidersUserConfigPath() {
  return join(appDataDir(), "Code - Insiders", "User", "mcp.json");
}

/**
 * Non-default VS Code profiles keep their own mcp.json. Writing the default
 * profile file while the user works in another profile registers nothing.
 * @returns {string[]}
 */
export function vscodeProfileConfigPaths() {
  const root = join(appDataDir(), "Code", "User", "profiles");
  if (!existsSync(root)) return [];
  try {
    return readdirSync(root)
      .map((id) => join(root, id, "mcp.json"))
      .filter((p) => existsSync(p));
  } catch {
    return [];
  }
}

/**
 * Prefer stable Code user mcp.json; Insiders only if Code absent and Insiders present.
 */
export function resolveVscodeConfigTarget() {
  const stable = vscodeUserConfigPath();
  const insiders = vscodeInsidersUserConfigPath();
  const profiles = vscodeProfileConfigPaths();
  if (profiles.length > 0) {
    return {
      path: null,
      ambiguous: true,
      candidates: [stable, ...profiles],
      existing: [stable, ...profiles].filter((p) => existsSync(p)),
      reason:
        "VS Code has per-profile MCP configuration. Run `MCP: Open User Configuration` (or `code --add-mcp`) so the entry lands in the profile you actually use.",
    };
  }
  if (existsSync(stable)) {
    return resolveUniqueConfigPath([stable]);
  }
  if (existsSync(insiders) && !existsSync(join(appDataDir(), "Code"))) {
    return resolveUniqueConfigPath([insiders]);
  }
  return resolveUniqueConfigPath([stable]);
}

/**
 * Evidence that VS Code is really installed — an empty leftover `Code/User`
 * directory is not enough to justify writing an MCP registration.
 * @returns {string[]}
 */
export function vscodeAppSignals() {
  /** @type {string[]} */
  const signals = [];
  const bin = which("code") || which("code-insiders");
  if (bin) signals.push(bin);
  for (const dir of [
    join(homedir(), ".vscode", "extensions"),
    join(homedir(), ".vscode-insiders", "extensions"),
    join(appDataDir(), "Code", "User", "globalStorage"),
    join(appDataDir(), "Code - Insiders", "User", "globalStorage"),
  ]) {
    if (dirHasEntries(dir)) signals.push(dir);
  }
  for (const file of [
    join(appDataDir(), "Code", "User", "settings.json"),
    join(appDataDir(), "Code - Insiders", "User", "settings.json"),
  ]) {
    if (existsSync(file)) signals.push(file);
  }
  return signals;
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createVscodeAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: VSCODE_ADAPTER_ID,
      name: "VS Code / Copilot",
      kind: "IDE_EXTENSION",
      // Tier B: file write is correct but not live-proven, and `code --add-mcp`
      // is the safer native path when VS Code is on PATH.
      supportTier: "B",
      rootKey: VSCODE_ROOT_KEY,
      codec: VSCODE_ENTRY_CODEC,
      requiresRestart: true,
      sessionHint: "Reload Window or restart VS Code, then use Copilot Agent mode",
      includeCwd: true,
      resolveConfigTarget: () => resolveVscodeConfigTarget(),
      detectExtra() {
        const bin = which("code") || which("code-insiders");
        const target = resolveVscodeConfigTarget();
        const configExists = Boolean(target.path && existsSync(target.path));
        const signals = vscodeAppSignals();
        return {
          detected: configExists || signals.length > 0,
          confidence: configExists && signals.length ? "high" : signals.length ? "medium" : "low",
          executable: bin,
          signals,
        };
      },
    },
    ctx,
  );
}
