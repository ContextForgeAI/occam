/**
 * Zed — context_servers in settings.json (user/global).
 */
import { existsSync } from "node:fs";
import { homedir, platform } from "node:os";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { appDataDir, resolveUniqueConfigPath } from "../paths.mjs";
import { which } from "../process.mjs";

export const ZED_ADAPTER_ID = "zed";
export const ZED_ROOT_KEY = "context_servers";

export function zedSettingsPath() {
  if (platform() === "win32") {
    return join(appDataDir(), "Zed", "settings.json");
  }
  // macOS + Linux: ~/.config/zed/settings.json
  const xdg = process.env.XDG_CONFIG_HOME?.trim() || join(homedir(), ".config");
  return join(xdg, "zed", "settings.json");
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createZedAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: ZED_ADAPTER_ID,
      name: "Zed",
      kind: "IDE_EXTENSION",
      // Tier B: Zed settings.json is JSONC, so a real user file with comments
      // cannot be rewritten safely; auto-connect only on explicit opt-in.
      supportTier: "B",
      rootKey: ZED_ROOT_KEY,
      requiresRestart: true,
      sessionHint: "Restart Zed (or reload settings) to activate Occam context server",
      // `cwd` is not part of the documented context_servers schema.
      includeCwd: false,
      resolveConfigTarget: () => resolveUniqueConfigPath([zedSettingsPath()]),
      detectExtra() {
        const bin = which("zed");
        const path = zedSettingsPath();
        return {
          detected: Boolean(bin) || existsSync(path),
          confidence: bin && existsSync(path) ? "high" : bin || existsSync(path) ? "medium" : "low",
          executable: bin,
        };
      },
    },
    ctx,
  );
}
