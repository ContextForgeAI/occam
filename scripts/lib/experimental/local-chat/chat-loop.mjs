/**
 * Experimental local chat tool loop — Ollama /api/chat ↔ Occam MCP.
 * Ollama-specific response quirks stay behind normalizeAssistantMessage.
 *
 * Native documented Ollama `tool_calls` is the only execution path.
 * Textual JSON tool envelopes are detected for recovery/nudge only —
 * never executed as tools.
 */
import { ollamaChat } from "./ollama-api.mjs";
import { callOccamTool } from "./session.mjs";
import {
  MAX_TOOL_ROUNDS,
  SYSTEM_PROMPT,
  TEXTUAL_TOOL_NUDGE,
  CORE_CHAT_TOOLS,
  OPTIONAL_CHAT_TOOLS,
  gateNativeToolCall,
  userMessageJustifiesWebTools,
} from "./tool-surface.mjs";

const ALL_KNOWN_CHAT_TOOLS = new Set([...CORE_CHAT_TOOLS, ...OPTIONAL_CHAT_TOOLS]);

/**
 * @param {unknown} message
 * @returns {{ role: string, content: string, tool_calls?: object[] }}
 */
export function normalizeAssistantMessage(message) {
  const msg = message && typeof message === "object" ? /** @type {Record<string, unknown>} */ (message) : {};
  const content = typeof msg.content === "string" ? msg.content : msg.content == null ? "" : String(msg.content);
  /** @type {object[]|undefined} */
  let toolCalls;
  if (Array.isArray(msg.tool_calls) && msg.tool_calls.length) {
    toolCalls = msg.tool_calls.map((tc, i) => {
      const fn = tc?.function && typeof tc.function === "object" ? tc.function : {};
      let args = fn.arguments;
      if (typeof args === "string") {
        try {
          args = JSON.parse(args);
        } catch {
          args = { _raw: args };
        }
      }
      if (!args || typeof args !== "object" || Array.isArray(args)) {
        args = {};
      }
      return {
        id: tc.id || `call_${i}`,
        type: "function",
        function: {
          name: String(fn.name || ""),
          arguments: args,
        },
      };
    });
  }
  /** @type {{ role: string, content: string, tool_calls?: object[] }} */
  const out = { role: "assistant", content };
  if (toolCalls?.length) out.tool_calls = toolCalls;
  return out;
}

/**
 * Detect textual tool-call envelopes in assistant content.
 * Classification only — NEVER execute.
 *
 * Matches shapes like:
 *   {"name":"occam_transcode","parameters":{"url":"…"}}
 *   {"name": "occam_transcode", "arguments": {…}}
 *
 * @param {string} content
 * @param {Iterable<string>} [allowedNames]
 * @returns {{ detected: boolean, name: string|null, reason: string }}
 */
export function detectTextualToolEnvelope(content, allowedNames = ALL_KNOWN_CHAT_TOOLS) {
  const allow = allowedNames instanceof Set ? allowedNames : new Set(allowedNames);
  const text = typeof content === "string" ? content.trim() : "";
  if (!text) return { detected: false, name: null, reason: "empty" };

  // Whole-message empty JSON / noop
  if (text === "{}" || text === "[]") {
    return { detected: true, name: null, reason: "empty_json" };
  }

  // Find a JSON object that looks like a tool envelope (not executed).
  const match = text.match(/\{[\s\S]*"name"\s*:\s*"([^"]+)"[\s\S]*\}/);
  if (!match) return { detected: false, name: null, reason: "no_envelope" };

  const name = match[1];
  if (allow.has(name) || name.startsWith("occam_")) {
    return { detected: true, name, reason: "textual_tool_envelope" };
  }
  // Unknown name inside JSON — still treat as leakage if it also has parameters/arguments
  if (/"parameters"\s*:/.test(text) || /"arguments"\s*:/.test(text)) {
    return { detected: true, name, reason: "textual_unknown_tool_envelope" };
  }
  return { detected: false, name: null, reason: "json_not_tool" };
}

/**
 * Strip leaked tool JSON from user-visible text when we are not executing it.
 * @param {string} content
 */
export function redactTextualToolLeakage(content) {
  const text = typeof content === "string" ? content : "";
  const leak = detectTextualToolEnvelope(text);
  if (!leak.detected) return text;
  // Remove JSON object blocks; keep any prose before them.
  const cleaned = text
    .replace(/\{[\s\S]*"name"\s*:\s*"[^"]+"[\s\S]*\}/g, "")
    .replace(/^\s*\{\}\s*$/g, "")
    .trim();
  return cleaned;
}

/**
 * @param {object} toolCall
 */
export function toolCallSummary(toolCall) {
  const name = toolCall?.function?.name || "?";
  const args = toolCall?.function?.arguments || {};
  const url = typeof args.url === "string" ? args.url : typeof args.urls === "string" ? args.urls : null;
  if (url) {
    try {
      const u = new URL(url);
      return `${name} → ${u.hostname}`;
    } catch {
      return name;
    }
  }
  if (Array.isArray(args.urls) && args.urls[0]) {
    try {
      return `${name} → ${new URL(String(args.urls[0])).hostname}+`;
    } catch {
      return name;
    }
  }
  return name;
}

/**
 * Run one user turn through Ollama + Occam tools.
 * @param {{
 *   baseUrl: string,
 *   model: string,
 *   messages: object[],
 *   session: import('./session.mjs').LocalChatSession,
 *   verbose?: boolean,
 *   onStatus?: (line: string) => void,
 *   maxRounds?: number,
 * }} opts
 */
export async function runChatTurn(opts) {
  const maxRounds = opts.maxRounds ?? MAX_TOOL_ROUNDS;
  const messages = [...opts.messages];
  if (!messages.some((m) => m.role === "system")) {
    messages.unshift({ role: "system", content: SYSTEM_PROMPT });
  }

  const turnStarted = Date.now();
  /** @type {Array<{ name: string, bytes: number, latencyMs: number, tokensUsed: number|null, ok: boolean }>} */
  const occamCalls = [];
  let rounds = 0;
  let finalContent = "";
  let roundCapHit = false;
  let textualNudgeUsed = false;
  let textualLeakBlocked = false;

  const allowed = new Set(opts.session.toolNames || []);

  /** Latest user text for tool gating (ignore nudges we inject). */
  function latestUserText() {
    for (let i = messages.length - 1; i >= 0; i--) {
      const m = messages[i];
      if (m?.role === "user" && typeof m.content === "string" && m.content !== TEXTUAL_TOOL_NUDGE) {
        return m.content;
      }
    }
    return "";
  }

  while (rounds <= maxRounds) {
    const chatStarted = Date.now();
    const response = await ollamaChat(
      opts.baseUrl,
      {
        model: opts.model,
        messages,
        tools: opts.session.ollamaTools,
      },
      { timeoutMs: 300_000 },
    );
    const assistant = normalizeAssistantMessage(response?.message);
    const hasTools = Array.isArray(assistant.tool_calls) && assistant.tool_calls.length > 0;

    if (!hasTools) {
      const leak = detectTextualToolEnvelope(assistant.content, allowed);
      if (leak.detected && !textualNudgeUsed) {
        textualNudgeUsed = true;
        messages.push({ role: "assistant", content: assistant.content || "" });
        const nudge =
          leak.reason === "empty_json" || !userMessageJustifiesWebTools(latestUserText())
            ? "Reply in plain text only. Do not write JSON. Answer the user directly."
            : TEXTUAL_TOOL_NUDGE;
        messages.push({ role: "user", content: nudge });
        continue;
      }
      if (leak.detected) {
        textualLeakBlocked = true;
        const redacted = redactTextualToolLeakage(assistant.content);
        finalContent =
          redacted ||
          "This model wrote a tool call as plain text instead of using native Ollama tool calling. Try qwen2.5:7b, or rephrase the question.";
        messages.push({ role: "assistant", content: finalContent });
        break;
      }

      finalContent = assistant.content || "";
      messages.push({ role: "assistant", content: finalContent });
      break;
    }

    rounds += 1;
    if (rounds > maxRounds) {
      roundCapHit = true;
      finalContent =
        assistant.content ||
        "Stopped: tool round limit reached. Please narrow the question or try again.";
      messages.push({ role: "assistant", content: finalContent });
      break;
    }

    // Append assistant tool_calls message (Ollama expects arguments as object or string — use object).
    messages.push({
      role: "assistant",
      content: assistant.content || "",
      tool_calls: assistant.tool_calls.map((tc) => ({
        id: tc.id,
        type: "function",
        function: {
          name: tc.function.name,
          arguments: tc.function.arguments,
        },
      })),
    });

    for (const tc of assistant.tool_calls) {
      const name = tc.function.name;
      const gate = gateNativeToolCall(latestUserText(), tc, allowed);
      if (!gate.allow) {
        messages.push({
          role: "tool",
          tool_name: name || "unknown",
          content: JSON.stringify({
            ok: false,
            failure: { code: gate.code || "tool_refused", message: gate.message || "Tool refused" },
          }),
        });
        continue;
      }
      if (opts.onStatus) opts.onStatus(toolCallSummary(tc));
      const result = await callOccamTool(opts.session, name, tc.function.arguments);
      occamCalls.push({
        name,
        bytes: result.bytes,
        latencyMs: result.latencyMs,
        tokensUsed: result.tokensUsed,
        ok: result.ok && !looksLikeOccamFailure(result.text),
      });
      messages.push({
        role: "tool",
        tool_name: name,
        content: truncateToolResult(result.text),
      });
    }

    void chatStarted;
  }

  const metrics = {
    model: opts.model,
    toolCalls: occamCalls.length,
    occamTools: occamCalls.map((c) => c.name),
    occamResultBytes: occamCalls.reduce((n, c) => n + c.bytes, 0),
    occamTokensUsed: sumNullable(occamCalls.map((c) => c.tokensUsed)),
    occamLatencyMs: occamCalls.reduce((n, c) => n + c.latencyMs, 0),
    totalLatencyMs: Date.now() - turnStarted,
    rounds,
    roundCapHit,
    textualNudgeUsed,
    textualLeakBlocked,
    rawBaseline: "unavailable",
  };

  return { messages, finalContent, metrics, occamCalls };
}

/**
 * @param {string} text
 */
function looksLikeOccamFailure(text) {
  try {
    const j = JSON.parse(text);
    return j?.ok === false || Boolean(j?.failure);
  } catch {
    return false;
  }
}

/**
 * Keep tool results bounded for local-model context.
 * @param {string} text
 */
export function truncateToolResult(text, maxChars = 24_000) {
  if (typeof text !== "string") return String(text);
  if (text.length <= maxChars) return text;
  return `${text.slice(0, maxChars)}\n…[truncated ${text.length - maxChars} chars]`;
}

/**
 * @param {Array<number|null>} values
 * @returns {number|null}
 */
function sumNullable(values) {
  let sum = 0;
  let any = false;
  for (const v of values) {
    if (typeof v === "number") {
      sum += v;
      any = true;
    }
  }
  return any ? sum : null;
}

/**
 * @param {object} metrics
 * @param {(s: string) => void} write
 */
export function printVerboseMetrics(metrics, write = console.error) {
  write("");
  write(`Model: ${metrics.model}`);
  write(`Tool calls: ${metrics.toolCalls}`);
  write(`Occam tools: ${metrics.occamTools.length ? metrics.occamTools.join(", ") : "(none)"}`);
  write(`Occam result bytes: ${metrics.occamResultBytes}`);
  write(`Occam tokens used: ${metrics.occamTokensUsed == null ? "unavailable" : metrics.occamTokensUsed}`);
  write(`Occam latency: ${metrics.occamLatencyMs}ms`);
  write(`Total latency: ${metrics.totalLatencyMs}ms`);
  write(`Raw baseline: ${metrics.rawBaseline}`);
  write("");
}
