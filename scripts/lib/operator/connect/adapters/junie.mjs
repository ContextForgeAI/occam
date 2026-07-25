/**
 * JetBrains Junie — ASSISTED. No confirmed safe auto-connect lifecycle yet.
 */
import { existsSync } from "node:fs";
import { homedir, platform } from "node:os";
import { join } from "node:path";
import { createConfigFileAdapter } from "../config-file-adapter.mjs";
import { dirHasEntries } from "../paths.mjs";
import { which } from "../process.mjs";

export const JUNIE_ADAPTER_ID = "junie";

export function junieHintPaths() {
  /** @type {string[]} */
  const out = [];
  if (platform() === "win32") {
    const local = process.env.LOCALAPPDATA?.trim() || join(homedir(), "AppData", "Local");
    out.push(join(local, "JetBrains"));
  } else if (platform() === "darwin") {
    out.push(join(homedir(), "Library", "Application Support", "JetBrains"));
  } else {
    out.push(join(homedir(), ".local", "share", "JetBrains"));
    out.push(join(homedir(), ".config", "JetBrains"));
  }
  return out;
}

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createJunieAdapter(ctx) {
  const hints = junieHintPaths();
  return createConfigFileAdapter(
    {
      id: JUNIE_ADAPTER_ID,
      name: "Junie",
      kind: "IDE_EXTENSION",
      supportTier: "C",
      rootKey: "mcpServers",
      connectionMethod: "ASSISTED",
      requiresRestart: false,
      sessionHint:
        "Configure Occam in JetBrains Junie MCP settings using the stable launcher command",
      resolveConfigTarget: () => ({
        path: null,
        ambiguous: false,
        candidates: hints,
        reason:
          "Junie MCP auto-connect is not confirmed — paste the Occam stable launcher into Junie MCP settings manually (NEEDS LIVE VALIDATION).",
      }),
      detectExtra() {
        const bin = which("junie");
        const present = hints.filter((p) => dirHasEntries(p));
        return {
          detected: Boolean(bin) || present.length > 0,
          confidence: bin ? "medium" : "low",
          executable: bin,
          signals: present,
        };
      },
    },
    ctx,
  );
}
