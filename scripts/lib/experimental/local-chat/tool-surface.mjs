/**
 * Smallest useful local-model Occam tool surface.
 * Schemas always come from MCP tools/list — never hand-authored here.
 */

export const CORE_CHAT_TOOLS = Object.freeze([
  "occam_transcode",
  "occam_probe",
  "occam_digest",
]);

export const OPTIONAL_CHAT_TOOLS = Object.freeze(["occam_search"]);

export const MAX_TOOL_ROUNDS = 6;

/**
 * Search is exposed only when operator env actually configures a provider.
 * Mirrors SearchService.ResolveProvider required-config rules (without calling Core).
 * @param {NodeJS.ProcessEnv} [env]
 */
export function isSearchConfigured(env = process.env) {
  const name = String(env.OCCAM_SEARCH_PROVIDER || "").trim().toLowerCase();
  if (!name) return false;
  if (name === "searxng") {
    return Boolean(String(env.OCCAM_SEARCH_URL || "").trim());
  }
  if (name === "brave" || name === "tavily") {
    return Boolean(String(env.OCCAM_SEARCH_API_KEY || "").trim());
  }
  // Unknown provider name — do not advertise.
  return false;
}

/**
 * Approved tool names for this chat session.
 * @param {NodeJS.ProcessEnv} [env]
 */
export function allowedToolNames(env = process.env) {
  const names = [...CORE_CHAT_TOOLS];
  if (isSearchConfigured(env)) names.push(...OPTIONAL_CHAT_TOOLS);
  return names;
}

/**
 * Filter MCP tools/list down to the approved surface; preserve server schemas.
 * @param {Array<{ name?: string, description?: string, inputSchema?: object }>} listedTools
 * @param {string[]} allowed
 */
export function filterListedTools(listedTools, allowed) {
  const allow = new Set(allowed);
  const tools = Array.isArray(listedTools) ? listedTools : [];
  return tools.filter((t) => t?.name && allow.has(t.name));
}

/**
 * Convert MCP tool descriptors to Ollama /api/chat tools format.
 * @param {Array<{ name: string, description?: string, inputSchema?: object }>} mcpTools
 */
export function toOllamaTools(mcpTools) {
  return mcpTools.map((t) => ({
    type: "function",
    function: {
      name: t.name,
      description: t.description || t.name,
      parameters: t.inputSchema || { type: "object", properties: {} },
    },
  }));
}

/**
 * Compact display labels for startup banner.
 * @param {string[]} names
 */
export function shortToolLabels(names) {
  return names.map((n) => n.replace(/^occam_/, ""));
}

export const SYSTEM_PROMPT = `You are a helpful local assistant with optional Occam web tools.

Rules:
- Use Occam tools only when current/external web information is required.
- For a supplied URL, prefer occam_transcode.
- occam_probe inspects a page; it does not provide full article content.
- Use occam_digest for genuinely multi-page work.
- Do not use web tools for trivial arithmetic or general reasoning.
- Never claim page content was read if a tool failed or returned ok:false.
- Prefer few tool calls; one good call beats a retry loop.`;
