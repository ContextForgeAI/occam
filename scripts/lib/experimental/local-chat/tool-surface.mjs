/**
 * Smallest useful local-model Occam tool surface.
 *
 * MCP tools/list schemas are intentionally huge (dozens of opt-ins). Shipping
 * those verbatim to Ollama breaks native tool_calls on several Llama models —
 * they fall back to writing JSON in assistant text. Local chat therefore
 * exposes a slim, chat-oriented schema while still invoking the real MCP tools.
 */

export const CORE_CHAT_TOOLS = Object.freeze([
  "occam_transcode",
  "occam_probe",
  "occam_digest",
]);

export const OPTIONAL_CHAT_TOOLS = Object.freeze(["occam_search"]);

export const MAX_TOOL_ROUNDS = 6;

/**
 * Slim Ollama tool definitions — short "when to use" copy + minimal params.
 * Keys must stay in CORE_CHAT_TOOLS / OPTIONAL_CHAT_TOOLS.
 */
export const CHAT_OLLAMA_TOOL_SPECS = Object.freeze({
  occam_transcode: Object.freeze({
    description:
      "Read one web URL and return its current page content as Markdown. Use only when the user asks to read/extract/explain a specific URL.",
    parameters: Object.freeze({
      type: "object",
      properties: Object.freeze({
        url: Object.freeze({ type: "string", description: "https URL to read" }),
      }),
      required: Object.freeze(["url"]),
    }),
  }),
  occam_probe: Object.freeze({
    description:
      "Cheap URL access check (status/class). Prefer occam_transcode to read page content. Use only when diagnostics are needed before reading.",
    parameters: Object.freeze({
      type: "object",
      properties: Object.freeze({
        url: Object.freeze({ type: "string", description: "https URL to inspect" }),
      }),
      required: Object.freeze(["url"]),
    }),
  }),
  occam_digest: Object.freeze({
    description:
      "Read and combine several URLs in one call. Use only for multi-URL / multi-page requests.",
    parameters: Object.freeze({
      type: "object",
      properties: Object.freeze({
        urls: Object.freeze({
          type: "array",
          items: Object.freeze({ type: "string" }),
          description: "https URLs to digest (max 8)",
        }),
      }),
      required: Object.freeze(["urls"]),
    }),
  }),
  occam_search: Object.freeze({
    description:
      "Web search via the operator-configured Occam search provider. Use only when the user asks to search and no specific URL is given.",
    parameters: Object.freeze({
      type: "object",
      properties: Object.freeze({
        query: Object.freeze({ type: "string", description: "search query" }),
      }),
      required: Object.freeze(["query"]),
    }),
  }),
});

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
 * Filter MCP tools/list down to the approved surface; preserve server schemas
 * for allowlisting / diagnostics. Ollama sees slim specs via toOllamaTools.
 * @param {Array<{ name?: string, description?: string, inputSchema?: object }>} listedTools
 * @param {string[]} allowed
 */
export function filterListedTools(listedTools, allowed) {
  const allow = new Set(allowed);
  const tools = Array.isArray(listedTools) ? listedTools : [];
  return tools.filter((t) => t?.name && allow.has(t.name));
}

/**
 * Convert MCP tool descriptors to slim Ollama /api/chat tools.
 * Uses CHAT_OLLAMA_TOOL_SPECS — never ships the full MCP inputSchema to Ollama.
 * @param {Array<{ name: string, description?: string, inputSchema?: object }>} mcpTools
 */
export function toOllamaTools(mcpTools) {
  const order = [...CORE_CHAT_TOOLS, ...OPTIONAL_CHAT_TOOLS];
  const byName = new Map((Array.isArray(mcpTools) ? mcpTools : []).map((t) => [t.name, t]));
  /** @type {object[]} */
  const out = [];
  for (const name of order) {
    if (!byName.has(name)) continue;
    const spec = CHAT_OLLAMA_TOOL_SPECS[name];
    if (!spec) continue;
    out.push({
      type: "function",
      function: {
        name,
        description: spec.description,
        parameters: structuredClone(spec.parameters),
      },
    });
  }
  return out;
}

/**
 * Compact display labels for startup banner.
 * @param {string[]} names
 */
export function shortToolLabels(names) {
  return names.map((n) => n.replace(/^occam_/, ""));
}

export const SYSTEM_PROMPT = `You are a helpful local chat assistant with optional Occam web tools.

Default: answer in plain language. Most turns need no tools.
Do not mention tools, Occam, JSON, or function calls unless the user asks how they work.
Do not say "no tool needed" — just answer.

Never use tools for greetings, arithmetic, or ordinary knowledge.

Use tools only when the user supplies a URL to read/explain (occam_transcode),
multiple URLs to combine (occam_digest), or needs a rare access check (occam_probe).

Never invent current web page content.
Never write tool calls as JSON text — use the native tool-calling interface only.`;

/** Corrective nudge when a model leaks a textual tool envelope instead of native tool_calls. */
export const TEXTUAL_TOOL_NUDGE =
  "Do not write JSON or tool payloads in your reply. If you need a web tool, use the native tool-calling interface. Otherwise answer in plain text only.";

/**
 * True when the latest user text justifies Occam web tools.
 * Arithmetic / greetings / general knowledge → false.
 * @param {string} userText
 */
export function userMessageJustifiesWebTools(userText) {
  const text = typeof userText === "string" ? userText : "";
  if (/https?:\/\//i.test(text)) return true;
  if (/\b(search the web|web search|look up online|find online|google)\b/i.test(text)) return true;
  return false;
}

/**
 * Gate native tool_calls before MCP invocation.
 * Prefer native Ollama tool_calls, but never execute web fetches for no-URL chats.
 *
 * @param {string} userText
 * @param {{ function?: { name?: string, arguments?: object } }} toolCall
 * @param {Iterable<string>} [allowedNames]
 * @returns {{ allow: boolean, code?: string, message?: string }}
 */
export function gateNativeToolCall(userText, toolCall, allowedNames = CORE_CHAT_TOOLS) {
  const allow = allowedNames instanceof Set ? allowedNames : new Set(allowedNames);
  const name = String(toolCall?.function?.name || "");
  if (!name || !allow.has(name)) {
    return {
      allow: false,
      code: "tool_not_allowed",
      message: `Tool not available in local chat: ${name || "(missing)"}`,
    };
  }

  const webFetchTools = new Set(["occam_transcode", "occam_probe", "occam_digest"]);
  if (webFetchTools.has(name) && !userMessageJustifiesWebTools(userText)) {
    return {
      allow: false,
      code: "tool_refused_no_url",
      message:
        "No URL (or web-search request) in the user message. Answer directly without Occam web tools.",
    };
  }

  const args = toolCall?.function?.arguments;
  if (name === "occam_transcode" || name === "occam_probe") {
    if (!args || typeof args.url !== "string" || !/^https?:\/\//i.test(args.url)) {
      return {
        allow: false,
        code: "invalid_arguments",
        message: `${name} requires a https URL in arguments.url`,
      };
    }
  }
  if (name === "occam_digest") {
    const urls = args?.urls;
    const list = Array.isArray(urls) ? urls : typeof urls === "string" ? [urls] : [];
    if (!list.length || !list.every((u) => typeof u === "string" && /^https?:\/\//i.test(u))) {
      return {
        allow: false,
        code: "invalid_arguments",
        message: "occam_digest requires arguments.urls as an array of https URLs",
      };
    }
  }

  return { allow: true };
}
