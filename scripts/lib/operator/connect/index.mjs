/**
 * Public barrel for Occam connect platform (Wave 1–3).
 */
export {
  OCCAM_MCP_SERVER_NAME,
  OCCAM_MANAGED_ENV_KEY,
  OCCAM_MANAGED_MARKER,
  PRODUCT_KINDS,
  CONNECTION_METHODS,
} from "./kinds.mjs";
export { buildStableLaunchSpec, stdioFromSpec, assertLaunchable } from "./launch-spec.mjs";
export {
  VERIFICATION_LEVELS,
  levelLabel,
  evaluateReadyState,
  aggregateConnectionReady,
} from "./verification.mjs";
export { resolveConnectMode, isCiLike } from "./policy.mjs";
export { looksLikeOccamManagedEntry, decidePostVerifyCleanup } from "./ownership.mjs";
export {
  loadMcpConfig,
  inspectManagedEntry,
  planMcpMerge,
  commitMcpRegistration,
  rollbackMcpRegistration,
  mcpEntriesEqual,
  redactMcpEntry,
  STDIO_ENTRY_CODEC,
  encodeStdioEntry,
  restoreMcpConfig,
  looksLikeJsonc,
} from "./config-engine.mjs";
export { OPENCODE_ENTRY_CODEC, VSCODE_ENTRY_CODEC } from "./codecs.mjs";
export {
  appDataDir,
  localAppDataDir,
  dirHasEntries,
  resolveUniqueConfigPath,
  listWindowsClaudeMsixConfigs,
} from "./paths.mjs";
export { createConfigFileAdapter } from "./config-file-adapter.mjs";
export { runConnect } from "./orchestrator.mjs";
export { renderConnectTranscript, renderHumanConnectSummary } from "./render.mjs";
export {
  createHostAdapters,
  listHostAdapters,
  selectAutoConnectAdapters,
  describeSupportedHosts,
  partitionSupportedHosts,
  WAVE1_HOST_IDS,
  WAVE2_HOST_IDS,
  WAVE3_HOST_IDS,
  AUTO_CONNECT_HOST_IDS,
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
  detectAllRuntimes,
} from "./registry.mjs";
export {
  createHermesAdapter,
  parseHermesMcpServer,
  hermesConfigPath,
  resolveHermesHome,
} from "./adapters/hermes.mjs";
export {
  createOpenClawAdapter,
  openclawConfigPath,
  readOpenClawServer,
} from "./adapters/openclaw.mjs";
export {
  createClaudeCodeAdapter,
  parseClaudeGetConnected,
  claudeUserConfigPath,
} from "./adapters/claude-code.mjs";
export {
  createCodexAdapter,
  parseCodexGetJson,
  codexJsonToEntry,
} from "./adapters/codex.mjs";
export {
  createGeminiAdapter,
  parseGeminiListEntry,
  geminiVerificationFromInspection,
} from "./adapters/gemini.mjs";
export {
  createCursorAdapter,
  cursorUserConfigPath,
  cursorWorkspaceConfigPath,
} from "./adapters/cursor.mjs";
export {
  createClaudeDesktopAdapter,
  resolveClaudeDesktopConfigTarget,
  claudeDesktopCandidatePaths,
} from "./adapters/claude-desktop.mjs";
export {
  createVscodeAdapter,
  vscodeUserConfigPath,
  resolveVscodeConfigTarget,
} from "./adapters/vscode.mjs";
export { createClineAdapter, clineConfigPath } from "./adapters/cline.mjs";
export { createRooAdapter, rooConfigPath } from "./adapters/roo.mjs";
export { createWindsurfAdapter, windsurfConfigPath } from "./adapters/windsurf.mjs";
export { createZedAdapter, zedSettingsPath } from "./adapters/zed.mjs";
export { createOpencodeAdapter, opencodeConfigPath } from "./adapters/opencode.mjs";
export { createGooseAdapter, gooseConfigPath } from "./adapters/goose.mjs";
export { createJunieAdapter, junieHintPaths } from "./adapters/junie.mjs";
