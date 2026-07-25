/**
 * OpenCode — root key `mcp` with local entry codec (command array + environment).
 */
import { existsSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";
import { OPENCODE_ENTRY_CODEC } from "../codecs.mjs";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { dirHasEntries, resolveUniqueConfigPath } from "../paths.mjs";
import { which } from "../process.mjs";

export const OPENCODE_ADAPTER_ID = "opencode";
export const OPENCODE_ROOT_KEY = "mcp";

export function opencodeConfigPath() {
  // Prefer XDG / config home; fall back to project-less user config.
  const xdg = process.env.XDG_CONFIG_HOME?.trim() || join(homedir(), ".config");
  return join(xdg, "opencode", "opencode.json");
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createOpencodeAdapter(ctx) {
  return createConfigFileAdapter(
    {
      id: OPENCODE_ADAPTER_ID,
      name: "OpenCode",
      kind: "AI_AGENT",
      // Schema matches the published OpenCode config reference, but no live
      // install was available to prove discovery — Tier B until it is.
      supportTier: "B",
      rootKey: OPENCODE_ROOT_KEY,
      codec: OPENCODE_ENTRY_CODEC,
      requiresRestart: true,
      sessionHint: "Restart OpenCode / reload config to activate Occam",
      includeCwd: true,
      resolveConfigTarget: () => resolveUniqueConfigPath([opencodeConfigPath()]),
      detectExtra() {
        const bin = which("opencode");
        const path = opencodeConfigPath();
        const configExists = existsSync(path);
        const signals = [
          join(homedir(), ".config", "opencode"),
          join(homedir(), ".local", "share", "opencode"),
        ].filter((p) => dirHasEntries(p));
        if (bin) signals.unshift(bin);
        return {
          detected: configExists || signals.length > 0,
          confidence: configExists && bin ? "high" : configExists || signals.length ? "medium" : "low",
          executable: bin,
          signals,
        };
      },
    },
    ctx,
  );
}
