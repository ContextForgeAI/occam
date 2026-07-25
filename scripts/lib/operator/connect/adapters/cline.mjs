/**
 * Cline (VS Code extension) — mcpServers in extension globalStorage.
 */
import { existsSync } from "node:fs";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { appDataDir, dirHasEntries, resolveUniqueConfigPath } from "../paths.mjs";

export const CLINE_ADAPTER_ID = "cline";
export const CLINE_ROOT_KEY = "mcpServers";

export function clineConfigPath() {
  return join(
    appDataDir(),
    "Code",
    "User",
    "globalStorage",
    "saoudrizwan.claude-dev",
    "settings",
    "cline_mcp_settings.json",
  );
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createClineAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: CLINE_ADAPTER_ID,
      name: "Cline",
      kind: "IDE_EXTENSION",
      // Tier B until validated against a real Cline install.
      supportTier: "B",
      rootKey: CLINE_ROOT_KEY,
      requiresRestart: true,
      sessionHint: "Reload VS Code / Cline MCP panel to activate Occam",
      includeCwd: true,
      resolveConfigTarget: () => resolveUniqueConfigPath([clineConfigPath()]),
      detectExtra(_ctx, target) {
        const path = clineConfigPath();
        const extDir = join(
          appDataDir(),
          "Code",
          "User",
          "globalStorage",
          "saoudrizwan.claude-dev",
        );
        const configExists = existsSync(path);
        const extInstalled = dirHasEntries(extDir);
        return {
          detected: configExists || extInstalled,
          confidence: configExists ? "high" : extInstalled ? "medium" : "low",
          signals: [...(target.existing || []), ...(extInstalled ? [extDir] : [])],
        };
      },
    },
    ctx,
  );
}
