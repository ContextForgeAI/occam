/**
 * Windsurf — mcpServers in ~/.codeium/windsurf/mcp_config.json (cross-platform).
 */
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { dirHasEntries, localAppDataDir, resolveUniqueConfigPath } from "../paths.mjs";
import { which } from "../process.mjs";

export const WINDSURF_ADAPTER_ID = "windsurf";
export const WINDSURF_ROOT_KEY = "mcpServers";

export function windsurfConfigPath() {
  return join(homedir(), ".codeium", "windsurf", "mcp_config.json");
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createWindsurfAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: WINDSURF_ADAPTER_ID,
      name: "Windsurf",
      kind: "IDE_EXTENSION",
      // Tier B until validated against a real Windsurf install.
      supportTier: "B",
      rootKey: WINDSURF_ROOT_KEY,
      requiresRestart: true,
      sessionHint: "Reload Windsurf Cascade / MCP settings to activate Occam",
      includeCwd: true,
      resolveConfigTarget: () => resolveUniqueConfigPath([windsurfConfigPath()]),
      detectExtra() {
        const bin = which("windsurf");
        const path = windsurfConfigPath();
        const configExists = existsSync(path);
        // ~/.codeium can be left behind by the Codeium editor plugin; require content.
        const signals = [
          join(homedir(), ".codeium", "windsurf"),
          join(localAppDataDir(), "Programs", "Windsurf"),
        ].filter((p) => dirHasEntries(p));
        if (bin) signals.unshift(bin);
        return {
          detected: configExists || signals.length > 0,
          confidence: configExists ? "high" : signals.length > 0 ? "medium" : "low",
          executable: bin,
          signals,
        };
      },
    },
    ctx,
  );
}
