const HTTP_PROXY_KEY = "OCCAM_HTTP_PROXY";
const HTTPS_PROXY_KEY = "OCCAM_HTTPS_PROXY";
const NO_PROXY_KEY = "OCCAM_NO_PROXY";

const ALLOWED_PROXY_PROTOCOLS = new Set(["http:", "https:", "socks5:"]);

/** @typedef {{ httpProxy: string | null, httpsProxy: string | null, noProxy: string[] }} EgressConfig */

export class EgressProxyError extends Error {
  /** @param {string} code */
  constructor(code, message = code) {
    super(message);
    this.name = "EgressProxyError";
    this.code = code;
  }
}

/** @returns {EgressConfig} */
export function readEgressConfig() {
  const httpProxy = trimEnv(HTTP_PROXY_KEY);
  const httpsProxy = trimEnv(HTTPS_PROXY_KEY) ?? httpProxy;
  return {
    httpProxy,
    httpsProxy,
    noProxy: parseNoProxy(trimEnv(NO_PROXY_KEY)),
  };
}

/** @param {string | null | undefined} value */
export function validateProxyUrl(value) {
  if (!value) {
    return { ok: false, failure: "invalid_proxy_url" };
  }

  try {
    const url = new URL(value);
    if (!ALLOWED_PROXY_PROTOCOLS.has(url.protocol)) {
      return { ok: false, failure: "invalid_proxy_url" };
    }

    if (!url.hostname) {
      return { ok: false, failure: "invalid_proxy_url" };
    }

    return { ok: true, server: value };
  } catch {
    return { ok: false, failure: "invalid_proxy_url" };
  }
}

/**
 * @param {string} hostname
 * @param {string[]} noProxy
 */
export function shouldBypassProxy(hostname, noProxy) {
  const host = hostname.toLowerCase();
  for (const entry of noProxy) {
    const rule = entry.trim().toLowerCase();
    if (!rule) {
      continue;
    }

    if (rule === "*") {
      return true;
    }

    if (host === rule) {
      return true;
    }

    if (rule.startsWith(".") && (host === rule.slice(1) || host.endsWith(rule))) {
      return true;
    }

    if (rule.startsWith("*.")) {
      const suffix = rule.slice(1);
      if (host === suffix.slice(1) || host.endsWith(suffix)) {
        return true;
      }
    }
  }

  return false;
}

/**
 * @param {string} urlString
 * @param {EgressConfig} [config]
 */
export function resolveProxyForUrl(urlString, config = readEgressConfig()) {
  let url;
  try {
    url = new URL(urlString);
  } catch {
    return null;
  }

  if (shouldBypassProxy(url.hostname, config.noProxy)) {
    return null;
  }

  return url.protocol === "https:" ? config.httpsProxy : config.httpProxy;
}

/** Map OCCAM_* proxy env to standard vars consumed by Node fetch (Undici). */
export function syncStandardProxyEnv() {
  const config = readEgressConfig();
  if (config.httpProxy) {
    const validated = validateProxyUrl(config.httpProxy);
    if (!validated.ok) {
      throw new EgressProxyError(validated.failure);
    }

    process.env.HTTP_PROXY = config.httpProxy;
  }

  if (config.httpsProxy) {
    const validated = validateProxyUrl(config.httpsProxy);
    if (!validated.ok) {
      throw new EgressProxyError(validated.failure);
    }

    process.env.HTTPS_PROXY = config.httpsProxy;
  }

  if (config.noProxy.length > 0) {
    process.env.NO_PROXY = config.noProxy.join(",");
  }
}

/** @returns {import('playwright').Proxy | null} */
export function resolvePlaywrightProxy() {
  const config = readEgressConfig();
  const proxyUrl = config.httpsProxy ?? config.httpProxy;
  if (!proxyUrl) {
    return null;
  }

  const validated = validateProxyUrl(proxyUrl);
  if (!validated.ok) {
    return null;
  }

  try {
    const parsed = new URL(proxyUrl);
    const server = `${parsed.protocol}//${parsed.host}`;
    /** @type {import('playwright').Proxy} */
    const proxy = { server };
    if (parsed.username) {
      proxy.username = decodeURIComponent(parsed.username);
      proxy.password = decodeURIComponent(parsed.password);
    }

    if (config.noProxy.length > 0) {
      proxy.bypass = config.noProxy.join(",");
    }

    return proxy;
  } catch {
    return null;
  }
}

/**
 * @param {string | null | undefined} proxyUrl
 */
export function redactProxyUrl(proxyUrl) {
  if (!proxyUrl) {
    return "";
  }

  try {
    const parsed = new URL(proxyUrl);
    if (parsed.username || parsed.password) {
      parsed.username = "***";
      parsed.password = "***";
    }

    return parsed.toString();
  } catch {
    return "(invalid proxy url)";
  }
}

/**
 * @param {string} url
 * @param {RequestInit} [init]
 */
export async function egressFetch(url, init = {}) {
  const proxyUrl = resolveProxyForUrl(url);
  if (!proxyUrl) {
    return fetch(url, init);
  }

  const validated = validateProxyUrl(proxyUrl);
  if (!validated.ok) {
    throw new EgressProxyError(validated.failure);
  }

  syncStandardProxyEnv();

  const { ProxyAgent, fetch: undiciFetch } = await import("undici");
  const agent = new ProxyAgent(validated.server);
  try {
    const response = await undiciFetch(url, { ...init, dispatcher: agent });
    return response;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (/ECONNREFUSED|ENOTFOUND|ETIMEDOUT|proxy|fetch failed|502|Bad Gateway/i.test(message)) {
      throw new EgressProxyError("proxy_unreachable", message);
    }

    throw error;
  } finally {
    await agent.close();
  }
}

/** @param {string | null | undefined} key */
function trimEnv(key) {
  const value = process.env[key]?.trim();
  return value || null;
}

/** @param {string | null | undefined} raw */
function parseNoProxy(raw) {
  if (!raw) {
    return [];
  }

  return raw
    .split(/[,;\s]+/)
    .map((part) => part.trim())
    .filter(Boolean);
}
