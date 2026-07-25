/**
 * Roo Code — mcpServers in extension globalStorage (mcp_settings.json).
 */
import { existsSync } from "node:fs";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { appDataDir, dirHasEntries, resolveUniqueConfigPath } from "../paths.mjs";

export const ROO_ADAPTER_ID = "roo";
export const ROO_ROOT_KEY = "mcpServers";

export function rooConfigPath() {
  return join(
    appDataDir(),
    "Code",
    "User",
    "globalStorage",
    "rooveterinaryinc.roo-cline",
    "settings",
    "mcp_settings.json",
  );
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createRooAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: ROO_ADAPTER_ID,
      name: "Roo Code",
      kind: "IDE_EXTENSION",
      // Tier B until validated against a real Roo Code install.
      supportTier: "B",
      rootKey: ROO_ROOT_KEY,
      requiresRestart: true,
      sessionHint: "Reload VS Code / Roo MCP settings to activate Occam",
      includeCwd: true,
      resolveConfigTarget: () => resolveUniqueConfigPath([rooConfigPath()]),
      detectExtra() {
        const path = rooConfigPath();
        const extDir = join(
          appDataDir(),
          "Code",
          "User",
          "globalStorage",
          "rooveterinaryinc.roo-cline",
        );
        const configExists = existsSync(path);
        const extInstalled = dirHasEntries(extDir);
        return {
          detected: configExists || extInstalled,
          confidence: configExists ? "high" : extInstalled ? "medium" : "low",
          signals: extInstalled ? [extDir] : [],
        };
      },
    },
    ctx,
  );
}
