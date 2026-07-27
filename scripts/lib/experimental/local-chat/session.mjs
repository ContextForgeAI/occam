/**
 * Experimental local-chat session: MCP host lifecycle + cleanup.
 */
import { buildStableLaunchSpec } from "../../operator/connect/launch-spec.mjs";
import { openOccamMcpSession, mcpToolText } from "../../mcp-stdio-client.mjs";
import {
  allowedToolNames,
  filterListedTools,
  toOllamaTools,
  shortToolLabels,
  isSearchConfigured,
} from "./tool-surface.mjs";

/**
 * @typedef {{
 *   client: import('../../mcp-stdio-client.mjs').McpStdioClient,
 *   tools: object[],
 *   toolNames: string[],
 *   ollamaTools: object[],
 *   searchExposed: boolean,
 *   launcherPid: number|null,
 *   closed: boolean,
 * }} LocalChatSession
 */

/**
 * Start Occam MCP and prepare the filtered Ollama tool surface.
 * @param {string} occamHome
 * @param {{ env?: NodeJS.ProcessEnv }} [opts]
 * @returns {Promise<LocalChatSession>}
 */
export async function startLocalChatSession(occamHome, opts = {}) {
  const env = opts.env ?? process.env;
  const spec = buildStableLaunchSpec(occamHome);
  const client = await openOccamMcpSession({
    occamHome,
    command: spec.command,
    args: spec.args,
    env: { ...env, ...spec.env },
    cwd: spec.cwd,
    clientInfo: { name: "occam-local-chat", version: "0.1.0-experimental" },
    requestTimeoutMs: 120_000,
  });

  const listed = await client.request("tools/list", {});
  const allowed = allowedToolNames(env);
  const tools = filterListedTools(listed?.tools ?? [], allowed);
  const toolNames = tools.map((t) => t.name);
  const missing = allowed.filter((n) => !toolNames.includes(n));
  if (missing.length) {
    await client.close({ graceMs: 500 });
    throw new Error(`Occam tools missing from tools/list: ${missing.join(", ")}`);
  }

  return {
    client,
    tools,
    toolNames,
    ollamaTools: toOllamaTools(tools),
    searchExposed: isSearchConfigured(env) && toolNames.includes("occam_search"),
    launcherPid: client.pid,
    closed: false,
  };
}

/**
 * Call an allowed Occam MCP tool; return compact typed text for the model.
 * @param {LocalChatSession} session
 * @param {string} name
 * @param {unknown} args
 * @returns {Promise<{ ok: boolean, text: string, bytes: number, latencyMs: number, tokensUsed: number|null }>}
 */
export async function callOccamTool(session, name, args) {
  const started = Date.now();
  if (!session.toolNames.includes(name)) {
    const text = JSON.stringify({
      ok: false,
      failure: { code: "tool_not_allowed", message: `Tool not available in local chat: ${name}` },
    });
    return {
      ok: false,
      text,
      bytes: Buffer.byteLength(text, "utf8"),
      latencyMs: Date.now() - started,
      tokensUsed: null,
    };
  }

  let argumentsObj = {};
  if (args && typeof args === "object" && !Array.isArray(args)) {
    argumentsObj = /** @type {Record<string, unknown>} */ (args);
  } else if (typeof args === "string" && args.trim()) {
    try {
      argumentsObj = JSON.parse(args);
    } catch {
      const text = JSON.stringify({
        ok: false,
        failure: { code: "invalid_arguments", message: "Tool arguments must be a JSON object" },
      });
      return {
        ok: false,
        text,
        bytes: Buffer.byteLength(text, "utf8"),
        latencyMs: Date.now() - started,
        tokensUsed: null,
      };
    }
  }

  try {
    const result = await session.client.request(
      "tools/call",
      { name, arguments: argumentsObj },
      180_000,
    );
    const rawText = mcpToolText(result) ?? JSON.stringify(result);
    let tokensUsed = null;
    try {
      const parsed = JSON.parse(rawText);
      const t =
        parsed?.tokens?.used ??
        parsed?.tokensUsed ??
        parsed?.meta?.tokensUsed ??
        parsed?.quality?.tokensUsed;
      if (typeof t === "number") tokensUsed = t;
    } catch {
      /* ignore */
    }
    return {
      ok: true,
      text: rawText,
      bytes: Buffer.byteLength(rawText, "utf8"),
      latencyMs: Date.now() - started,
      tokensUsed,
    };
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    const compact = message.length > 240 ? `${message.slice(0, 240)}…` : message;
    const text = JSON.stringify({
      ok: false,
      failure: { code: "host_invocation_failed", message: compact },
    });
    return {
      ok: false,
      text,
      bytes: Buffer.byteLength(text, "utf8"),
      latencyMs: Date.now() - started,
      tokensUsed: null,
    };
  }
}

/**
 * Tear down MCP stdio + child launcher. Safe to call multiple times.
 * @param {LocalChatSession | null | undefined} session
 */
export async function closeLocalChatSession(session) {
  if (!session || session.closed) return;
  session.closed = true;
  try {
    await session.client.close({ graceMs: 2_000 });
  } catch {
    /* ignore */
  }
}

/**
 * @param {LocalChatSession} session
 */
export function formatToolBanner(session) {
  return shortToolLabels(session.toolNames).join(", ");
}
