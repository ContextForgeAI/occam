/**
 * Thin human CLI over shipped MCP tools. Not a second extractor and not a new MCP tool.
 *
 *   occam read   <url>     → occam_transcode
 *   occam search <query>   → occam_search
 *   occam digest <url...>  → occam_digest
 */
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildStableLaunchSpec } from "./connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../mcp-stdio-client.mjs";

const USAGE = {
  read: `usage: occam read <url> [--json] [--focus Q] [--max-tokens N] [--fit|--no-fit] [--backend http|browser|http_then_browser]
Read one live URL via occam_transcode. Exit 0 when ok:true, 1 when ok:false or host error, 2 on usage.`,
  search: `usage: occam search <query> [--json] [--max-results N]
Search the open web via occam_search (keyless DuckDuckGo by default). Exit 0 when ok:true, 1 when ok:false or host error, 2 on usage.`,
  digest: `usage: occam digest <url> [url...] [--json] [--focus Q] [--max-tokens N] [--fit|--no-fit] [--backend http|browser|http_then_browser]
Research several URLs via occam_digest. Exit 0 when ok:true, 1 when ok:false or host error, 2 on usage.`,
};

/**
 * @param {string} command
 * @param {string[]} argv
 */
export function parseDataArgs(command, argv) {
  const flags = {
    command,
    json: false,
    help: false,
    focus: null,
    maxTokens: null,
    fit: null,
    backend: null,
    maxResults: null,
    positionals: [],
    error: null,
  };
  const args = [...argv];
  while (args.length) {
    const token = args.shift();
    if (token === "--") {
      flags.positionals.push(...args);
      break;
    }
    if (token === "--json") {
      flags.json = true;
      continue;
    }
    if (token === "-h" || token === "--help") {
      flags.help = true;
      continue;
    }
    if (token === "--fit") {
      flags.fit = true;
      continue;
    }
    if (token === "--no-fit") {
      flags.fit = false;
      continue;
    }
    if (token === "--focus" || token.startsWith("--focus=")) {
      flags.focus = token.includes("=") ? token.slice("--focus=".length) : args.shift();
      if (!flags.focus) flags.error = "missing --focus value";
      continue;
    }
    if (token === "--max-tokens" || token.startsWith("--max-tokens=")) {
      const raw = token.includes("=") ? token.slice("--max-tokens=".length) : args.shift();
      const n = Number(raw);
      if (!Number.isInteger(n) || n < 128) flags.error = "--max-tokens must be an integer >= 128";
      else flags.maxTokens = n;
      continue;
    }
    if (token === "--backend" || token.startsWith("--backend=")) {
      flags.backend = token.includes("=") ? token.slice("--backend=".length) : args.shift();
      if (!["http", "browser", "http_then_browser", "http-then-browser"].includes(flags.backend ?? "")) {
        flags.error = "--backend must be http, browser, or http_then_browser";
      }
      continue;
    }
    if (token === "--max-results" || token.startsWith("--max-results=")) {
      const raw = token.includes("=") ? token.slice("--max-results=".length) : args.shift();
      const n = Number(raw);
      if (!Number.isInteger(n) || n < 1 || n > 20) flags.error = "--max-results must be an integer 1–20";
      else flags.maxResults = n;
      continue;
    }
    if (token.startsWith("-")) {
      flags.error = `unknown flag ${token}`;
      continue;
    }
    flags.positionals.push(token);
  }
  return flags;
}

/**
 * @param {ReturnType<typeof parseDataArgs>} flags
 */
export function buildToolCall(flags) {
  if (flags.command === "read") {
    const url = flags.positionals[0];
    if (!url) return { error: "missing url" };
    /** @type {Record<string, unknown>} */
    const arguments_ = { url };
    if (flags.backend) arguments_.backend_policy = flags.backend.replaceAll("-", "_");
    if (flags.focus) arguments_.focus_query = flags.focus;
    if (flags.maxTokens != null) arguments_.max_tokens = flags.maxTokens;
    if (flags.fit != null) arguments_.fit_markdown = flags.fit;
    return { tool: "occam_transcode", arguments: arguments_ };
  }
  if (flags.command === "search") {
    const query = flags.positionals.join(" ").trim();
    if (!query) return { error: "missing query" };
    /** @type {Record<string, unknown>} */
    const arguments_ = { query };
    if (flags.maxResults != null) arguments_.max_results = flags.maxResults;
    return { tool: "occam_search", arguments: arguments_ };
  }
  if (flags.command === "digest") {
    const urls = flags.positionals.filter(Boolean);
    if (urls.length === 0) return { error: "missing url" };
    /** @type {Record<string, unknown>} */
    const arguments_ = { urls };
    if (flags.backend) arguments_.backend_policy = flags.backend.replaceAll("-", "_");
    if (flags.focus) arguments_.focus_query = flags.focus;
    if (flags.maxTokens != null) arguments_.per_url_max_tokens = flags.maxTokens;
    if (flags.fit != null) arguments_.fit_markdown = flags.fit;
    return { tool: "occam_digest", arguments: arguments_ };
  }
  return { error: `unknown command ${flags.command}` };
}

/**
 * @param {string} command
 * @param {unknown} payload
 */
export function formatPlain(command, payload) {
  if (!payload || typeof payload !== "object") return "";
  const row = /** @type {Record<string, unknown>} */ (payload);
  if (row.ok === false) {
    const failure = row.failure && typeof row.failure === "object"
      ? /** @type {Record<string, unknown>} */ (row.failure)
      : {};
    const code = failure.code ?? row.failureCode ?? "unknown";
    const message = failure.message ?? row.message ?? "";
    return `error: ${code}${message ? `: ${message}` : ""}`;
  }
  if (command === "read") {
    return typeof row.markdown === "string" ? row.markdown : "";
  }
  if (command === "search") {
    const results = Array.isArray(row.results) ? row.results : [];
    return results
      .map((item, index) => {
        const hit = item && typeof item === "object" ? /** @type {Record<string, unknown>} */ (item) : {};
        return `${hit.id ?? `S${index + 1}`}\t${hit.title ?? ""}\t${hit.url ?? ""}`;
      })
      .join("\n");
  }
  if (command === "digest") {
    if (typeof row.combined === "string" && row.combined.trim()) return row.combined;
    const items = Array.isArray(row.items) ? row.items : [];
    return items
      .map((item) => {
        const page = item && typeof item === "object" ? /** @type {Record<string, unknown>} */ (item) : {};
        return `## ${page.url ?? ""}\n\n${page.excerpt ?? ""}`;
      })
      .join("\n\n");
  }
  return "";
}

function resolveOccamHome() {
  const fromEnv = process.env.OCCAM_HOME?.trim();
  if (fromEnv) return fromEnv;
  return join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
}

/**
 * @param {string} command
 * @param {string[]} argv
 * @param {{ callTool?: (tool: string, args: Record<string, unknown>) => Promise<unknown> }} [hooks]
 */
export async function runDataCommand(command, argv, hooks = {}) {
  const flags = parseDataArgs(command, argv);
  if (flags.help) {
    console.log(USAGE[command] ?? USAGE.read);
    return 0;
  }
  if (flags.error) {
    console.error(`error: ${flags.error}`);
    console.error(USAGE[command] ?? USAGE.read);
    return 2;
  }
  const call = buildToolCall(flags);
  if (call.error) {
    console.error(`error: ${call.error}`);
    console.error(USAGE[command] ?? USAGE.read);
    return 2;
  }

  let payload;
  try {
    if (hooks.callTool) {
      payload = await hooks.callTool(call.tool, call.arguments);
    } else {
      payload = await invokeMcpTool(call.tool, call.arguments);
    }
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    if (flags.json) console.log(JSON.stringify({ ok: false, error: message }, null, 2));
    else console.error(`error: ${message}`);
    return 1;
  }

  if (flags.json) {
    console.log(JSON.stringify(payload, null, 2));
  } else {
    const text = formatPlain(command, payload);
    if (payload && typeof payload === "object" && /** @type {{ok?: unknown}} */ (payload).ok === false) {
      console.error(text);
    } else {
      console.log(text);
    }
  }

  if (payload && typeof payload === "object" && /** @type {{ok?: unknown}} */ (payload).ok === false) {
    return 1;
  }
  return 0;
}

/**
 * @param {string} tool
 * @param {Record<string, unknown>} arguments_
 */
async function invokeMcpTool(tool, arguments_) {
  const occamHome = resolveOccamHome();
  const spec = buildStableLaunchSpec(occamHome);
  const client = await openOccamMcpSession({
    occamHome,
    command: spec.command,
    args: spec.args,
    env: { ...process.env, ...spec.env, OCCAM_PROFILE: process.env.OCCAM_PROFILE || "reader" },
    cwd: spec.cwd,
    clientInfo: { name: "occam-data-cli", version: "1.0" },
    requestTimeoutMs: 120_000,
  });
  try {
    const result = await client.request("tools/call", { name: tool, arguments: arguments_ });
    const text = mcpToolText(result);
    if (!text) throw new Error(`${tool} returned no text payload`);
    return JSON.parse(text);
  } finally {
    await client.close({ graceMs: 1500 });
  }
}
