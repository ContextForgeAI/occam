/**
 * Adapter + runtime registry for Occam connect platform.
 */
import { createHermesAdapter, HERMES_ADAPTER_ID } from "./adapters/hermes.mjs";
import { createOpenClawAdapter, OPENCLAW_ADAPTER_ID } from "./adapters/openclaw.mjs";
import { createClaudeCodeAdapter, CLAUDE_CODE_ADAPTER_ID } from "./adapters/claude-code.mjs";
import { createCodexAdapter, CODEX_ADAPTER_ID } from "./adapters/codex.mjs";
import { createGeminiAdapter, GEMINI_ADAPTER_ID } from "./adapters/gemini.mjs";
import { createCursorAdapter, CURSOR_ADAPTER_ID } from "./adapters/cursor.mjs";
import {
  createClaudeDesktopAdapter,
  CLAUDE_DESKTOP_ADAPTER_ID,
} from "./adapters/claude-desktop.mjs";
import { createVscodeAdapter, VSCODE_ADAPTER_ID } from "./adapters/vscode.mjs";
import { createClineAdapter, CLINE_ADAPTER_ID } from "./adapters/cline.mjs";
import { createRooAdapter, ROO_ADAPTER_ID } from "./adapters/roo.mjs";
import { createWindsurfAdapter, WINDSURF_ADAPTER_ID } from "./adapters/windsurf.mjs";
import { createZedAdapter, ZED_ADAPTER_ID } from "./adapters/zed.mjs";
import { createOpencodeAdapter, OPENCODE_ADAPTER_ID } from "./adapters/opencode.mjs";
import { createGooseAdapter, GOOSE_ADAPTER_ID } from "./adapters/goose.mjs";
import { createJunieAdapter, JUNIE_ADAPTER_ID } from "./adapters/junie.mjs";
import { detectAllRuntimes } from "./runtimes.mjs";

/** Wave 1 host adapter factory ids. */
export const WAVE1_HOST_IDS = Object.freeze([HERMES_ADAPTER_ID, OPENCLAW_ADAPTER_ID]);

/** Wave 2 host adapter factory ids. */
export const WAVE2_HOST_IDS = Object.freeze([
  CLAUDE_CODE_ADAPTER_ID,
  CODEX_ADAPTER_ID,
  GEMINI_ADAPTER_ID,
  CURSOR_ADAPTER_ID,
]);

/** Wave 3 config-file / assisted host ids. */
export const WAVE3_HOST_IDS = Object.freeze([
  CLAUDE_DESKTOP_ADAPTER_ID,
  VSCODE_ADAPTER_ID,
  CLINE_ADAPTER_ID,
  ROO_ADAPTER_ID,
  WINDSURF_ADAPTER_ID,
  ZED_ADAPTER_ID,
  OPENCODE_ADAPTER_ID,
  GOOSE_ADAPTER_ID,
  JUNIE_ADAPTER_ID,
]);

/** Default auto-connect registry order (Wave 1–3). Assisted hosts remain detect-only. */
export const AUTO_CONNECT_HOST_IDS = Object.freeze([
  ...WAVE1_HOST_IDS,
  ...WAVE2_HOST_IDS,
  ...WAVE3_HOST_IDS,
]);

/**
 * @param {{ occamHome: string, serverName?: string, workspaceRoot?: string }} ctx
 */
export function createHostAdapters(ctx) {
  return {
    [HERMES_ADAPTER_ID]: createHermesAdapter(ctx),
    [OPENCLAW_ADAPTER_ID]: createOpenClawAdapter(ctx),
    [CLAUDE_CODE_ADAPTER_ID]: createClaudeCodeAdapter(ctx),
    [CODEX_ADAPTER_ID]: createCodexAdapter(ctx),
    [GEMINI_ADAPTER_ID]: createGeminiAdapter(ctx),
    [CURSOR_ADAPTER_ID]: createCursorAdapter(ctx),
    [CLAUDE_DESKTOP_ADAPTER_ID]: createClaudeDesktopAdapter(ctx),
    [VSCODE_ADAPTER_ID]: createVscodeAdapter(ctx),
    [CLINE_ADAPTER_ID]: createClineAdapter(ctx),
    [ROO_ADAPTER_ID]: createRooAdapter(ctx),
    [WINDSURF_ADAPTER_ID]: createWindsurfAdapter(ctx),
    [ZED_ADAPTER_ID]: createZedAdapter(ctx),
    [OPENCODE_ADAPTER_ID]: createOpencodeAdapter(ctx),
    [GOOSE_ADAPTER_ID]: createGooseAdapter(ctx),
    [JUNIE_ADAPTER_ID]: createJunieAdapter(ctx),
  };
}

/**
 * @param {{ occamHome: string, serverName?: string, only?: string[], workspaceRoot?: string }} opts
 */
export function listHostAdapters(opts) {
  const all = createHostAdapters(opts);
  const ids = opts.only?.length ? opts.only : AUTO_CONNECT_HOST_IDS;
  return ids.map((id) => all[id]).filter(Boolean);
}

/**
 * Auto-connect candidates: detected with medium+ confidence, not assisted.
 *
 * Tier A hosts are live-validated and connect automatically. Tier B hosts are
 * implemented but not proven end-to-end, so they only connect when the user
 * names them explicitly (`--host <id>`).
 * @param {ReturnType<typeof listHostAdapters>} adapters
 * @param {{ explicit?: boolean }} [opts]
 */
export function selectAutoConnectAdapters(adapters, opts = {}) {
  const allowedTiers = opts.explicit === true ? ["A", "B"] : ["A"];
  return adapters.filter((a) => {
    const d = a.detect();
    if (d.ambiguous === true) return false;
    const methodOk =
      a.connectionMethod === "NATIVE_CLI" || a.connectionMethod === "CONFIG_FILE";
    return (
      d.detected &&
      (d.confidence === "high" || d.confidence === "medium") &&
      allowedTiers.includes(a.supportTier) &&
      methodOk
    );
  });
}

/**
 * Split host rows into the ones connect can configure and the ones that always
 * need the user. Callers render this instead of keeping their own host list,
 * which used to drift from the registry.
 * @param {Array<{ name?: string, connectionMethod?: string }>} hostRows
 */
export function partitionSupportedHosts(hostRows) {
  /** @type {string[]} */
  const automatic = [];
  /** @type {string[]} */
  const assisted = [];
  for (const host of hostRows) {
    if (!host?.name) continue;
    (host.connectionMethod === "ASSISTED" ? assisted : automatic).push(host.name);
  }
  return { automatic, assisted };
}

/**
 * One-line form of the same split, for status messages.
 * @param {Array<{ name?: string, connectionMethod?: string }>} hostRows
 */
export function describeSupportedHosts(hostRows) {
  const { automatic, assisted } = partitionSupportedHosts(hostRows);
  const parts = [];
  if (automatic.length) parts.push(automatic.join(", "));
  if (assisted.length) parts.push(`(${assisted.join("/")} assisted)`);
  return parts.join(" ");
}

export {
  detectAllRuntimes,
  HERMES_ADAPTER_ID,
  OPENCLAW_ADAPTER_ID,
  CLAUDE_CODE_ADAPTER_ID,
  CODEX_ADAPTER_ID,
  GEMINI_ADAPTER_ID,
  CURSOR_ADAPTER_ID,
  CLAUDE_DESKTOP_ADAPTER_ID,
  VSCODE_ADAPTER_ID,
  CLINE_ADAPTER_ID,
  ROO_ADAPTER_ID,
  WINDSURF_ADAPTER_ID,
  ZED_ADAPTER_ID,
  OPENCODE_ADAPTER_ID,
  GOOSE_ADAPTER_ID,
  JUNIE_ADAPTER_ID,
};
