/**
 * Narrow Ollama local HTTP API client — documented endpoints only.
 * Default base: http://127.0.0.1:11434
 * Never touches Ollama cloud / Web Search / App private APIs.
 */

/** @typedef {{ name: string, model?: string, modified_at?: string, size?: number, digest?: string }} OllamaTagModel */
/** @typedef {{ name: string, capabilities: string[], tools: boolean, raw: object }} OllamaModelInfo */

export const DEFAULT_OLLAMA_BASE = "http://127.0.0.1:11434";

/**
 * @param {string} [baseUrl]
 */
export function normalizeOllamaBase(baseUrl) {
  const raw = String(baseUrl || process.env.OLLAMA_HOST || DEFAULT_OLLAMA_BASE).trim();
  return raw.replace(/\/+$/, "") || DEFAULT_OLLAMA_BASE;
}

/**
 * @param {string} baseUrl
 * @param {string} path
 * @param {{ method?: string, body?: unknown, timeoutMs?: number }} [opts]
 */
export async function ollamaFetch(baseUrl, path, opts = {}) {
  const base = normalizeOllamaBase(baseUrl);
  const url = `${base}${path.startsWith("/") ? path : `/${path}`}`;
  const timeoutMs = opts.timeoutMs ?? 15_000;
  const ac = new AbortController();
  const timer = setTimeout(() => ac.abort(), timeoutMs);
  try {
    /** @type {RequestInit} */
    const init = {
      method: opts.method ?? (opts.body !== undefined ? "POST" : "GET"),
      signal: ac.signal,
      headers: { Accept: "application/json" },
    };
    if (opts.body !== undefined) {
      init.headers = { ...init.headers, "Content-Type": "application/json" };
      init.body = JSON.stringify(opts.body);
    }
    const res = await fetch(url, init);
    const text = await res.text();
    let json = null;
    try {
      json = text ? JSON.parse(text) : null;
    } catch {
      json = null;
    }
    if (!res.ok) {
      const detail = json?.error || text.slice(0, 200) || res.statusText;
      throw new Error(`Ollama ${res.status}: ${detail}`);
    }
    return json;
  } catch (err) {
    if (err?.name === "AbortError") {
      throw new Error(`Ollama unreachable at ${base} (timeout ${timeoutMs}ms)`);
    }
    const msg = err instanceof Error ? err.message : String(err);
    if (/fetch failed|ECONNREFUSED|ENOTFOUND|network/i.test(msg)) {
      throw new Error(`Ollama unreachable at ${base}`);
    }
    throw err instanceof Error ? err : new Error(msg);
  } finally {
    clearTimeout(timer);
  }
}

/**
 * @param {string} [baseUrl]
 * @returns {Promise<{ ok: true, version: string, baseUrl: string } | { ok: false, error: string, baseUrl: string }>}
 */
export async function probeOllama(baseUrl) {
  const base = normalizeOllamaBase(baseUrl);
  try {
    const ver = await ollamaFetch(base, "/api/version", { timeoutMs: 5_000 });
    const version = String(ver?.version ?? "unknown");
    return { ok: true, version, baseUrl: base };
  } catch (err) {
    return {
      ok: false,
      error: err instanceof Error ? err.message : String(err),
      baseUrl: base,
    };
  }
}

/**
 * @param {string} [baseUrl]
 * @returns {Promise<OllamaTagModel[]>}
 */
export async function listOllamaModels(baseUrl) {
  const tags = await ollamaFetch(baseUrl, "/api/tags", { timeoutMs: 10_000 });
  return Array.isArray(tags?.models) ? tags.models : [];
}

/**
 * @param {string} [baseUrl]
 * @param {string} model
 */
export async function showOllamaModel(baseUrl, model) {
  return ollamaFetch(baseUrl, "/api/show", {
    method: "POST",
    body: { model },
    timeoutMs: 30_000,
  });
}

/**
 * @param {unknown} show
 * @returns {boolean}
 */
export function modelSupportsTools(show) {
  const caps = show?.capabilities;
  if (!Array.isArray(caps)) return false;
  return caps.map((c) => String(c).toLowerCase()).includes("tools");
}

/**
 * @param {string} [baseUrl]
 * @returns {Promise<OllamaModelInfo[]>}
 */
export async function listModelsWithCapabilities(baseUrl) {
  const models = await listOllamaModels(baseUrl);
  /** @type {OllamaModelInfo[]} */
  const out = [];
  for (const m of models) {
    const name = String(m.name || "").trim();
    if (!name) continue;
    let show;
    try {
      show = await showOllamaModel(baseUrl, name);
    } catch {
      out.push({ name, capabilities: [], tools: false, raw: {} });
      continue;
    }
    const capabilities = Array.isArray(show?.capabilities)
      ? show.capabilities.map((c) => String(c))
      : [];
    out.push({
      name,
      capabilities,
      tools: modelSupportsTools(show),
      raw: show,
    });
  }
  return out;
}

/**
 * Deterministic pick among tool-capable models.
 * Prefer live-proven friend models (qwen2.5 → llama3.1); avoid degraded llama3.2
 * as default when a better option exists. Lexical fallback last.
 * @param {OllamaModelInfo[]} models
 */
export function pickDefaultToolModel(models) {
  const toolModels = models.filter((m) => m.tools);
  if (toolModels.length === 0) return null;
  // Supported-first preference (live Mac evidence). llama3.2 is intentionally last.
  const preferred = ["qwen2.5:7b", "qwen2.5", "llama3.1:8b", "llama3.1", "llama3.2:3b", "llama3.2"];
  for (const pref of preferred) {
    const hit = toolModels.find(
      (m) => m.name === pref || m.name.startsWith(`${pref}:`) || m.name.startsWith(`${pref}-`),
    );
    if (hit) return hit;
  }
  return [...toolModels].sort((a, b) => a.name.localeCompare(b.name))[0];
}

/**
 * Non-streaming /api/chat.
 * @param {string} [baseUrl]
 * @param {{
 *   model: string,
 *   messages: object[],
 *   tools?: object[],
 *   options?: object,
 * }} body
 * @param {{ timeoutMs?: number }} [opts]
 */
export async function ollamaChat(baseUrl, body, opts = {}) {
  return ollamaFetch(baseUrl, "/api/chat", {
    method: "POST",
    body: { ...body, stream: false },
    timeoutMs: opts.timeoutMs ?? 300_000,
  });
}
