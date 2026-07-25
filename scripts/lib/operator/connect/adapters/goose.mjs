/**
 * Goose — ASSISTED. YAML extensions config; no safe JSON config-engine auto-write.
 * Detection only + honest action-required messaging.
 */
import { existsSync } from "node:fs";
import { homedir, platform } from "node:os";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { appDataDir } from "../paths.mjs";
import { which } from "../process.mjs";

export const GOOSE_ADAPTER_ID = "goose";

export function gooseConfigPath() {
  if (platform() === "win32") {
    return join(appDataDir(), "Block", "goose", "config", "config.yaml");
  }
  const xdg = process.env.XDG_CONFIG_HOME?.trim() || join(homedir(), ".config");
  return join(xdg, "goose", "config.yaml");
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createGooseAdapter(ctx) {
  const path = gooseConfigPath();
  return createConfigFileAdapter(
    {
      id: GOOSE_ADAPTER_ID,
      name: "Goose",
      kind: "AI_AGENT",
      supportTier: "B",
      rootKey: "extensions",
      connectionMethod: "ASSISTED",
      requiresRestart: false,
      sessionHint: "Use goose configure → Add Extension (stdio) with Occam launch command",
      resolveConfigTarget: () => ({
        path,
        ambiguous: false,
        candidates: [path],
        reason:
          "Goose uses YAML extensions — assisted setup only. Run `goose configure` and add a stdio extension pointing at the Occam stable launcher.",
      }),
      detectExtra() {
        const bin = which("goose");
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
