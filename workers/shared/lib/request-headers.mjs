import { readFile } from "node:fs/promises";

/** @param {string | null | undefined} argValue */
export async function readRequestHeadersFile(argValue) {
  if (!argValue) {
    return {};
  }

  const filePath = argValue.replace(/^"|"$/g, "");
  try {
    const raw = await readFile(filePath, "utf8");
    const parsed = JSON.parse(raw);
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "parse_failed";
    console.error(`[occam.worker] request_headers_file_invalid code=${code}`);
  }

  return {};
}

/** @param {Record<string, string>} requestHeaders @param {Record<string, string>} defaults */
export function mergeFetchHeaders(requestHeaders, defaults) {
  const merged = { ...defaults, ...requestHeaders };
  const ua = requestHeaders["User-Agent"] ?? requestHeaders["user-agent"];
  if (!ua) {
    merged["User-Agent"] = defaults["User-Agent"];
  }

  return merged;
}

// Credential-bearing headers that must NOT survive a redirect to a different origin — mirrors how
// browsers / undici strip Authorization & Cookie on a cross-origin redirect. session_profile can
// carry these for the requested host; without stripping, a meta-refresh / redirect to a third-party
// host would leak host A's session credentials to host B.
const CROSS_ORIGIN_SENSITIVE_HEADERS = new Set([
  "cookie",
  "authorization",
  "proxy-authorization",
]);

/**
 * Return `headers` unchanged when `toUrl` is same-origin as `fromUrl`, otherwise a copy with the
 * credential-bearing headers removed. Use before re-sending session headers to a redirect target.
 * @param {Record<string, string>} headers
 * @param {string} fromUrl
 * @param {string} toUrl
 */
export function stripCrossOriginSensitiveHeaders(headers, fromUrl, toUrl) {
  let sameOrigin = false;
  try {
    sameOrigin = new URL(fromUrl).origin === new URL(toUrl).origin;
  } catch {
    sameOrigin = false; // unparseable → fail safe, strip credentials
  }
  if (sameOrigin) {
    return headers;
  }
  const out = {};
  for (const [name, value] of Object.entries(headers)) {
    if (!CROSS_ORIGIN_SENSITIVE_HEADERS.has(name.toLowerCase())) {
      out[name] = value;
    }
  }
  return out;
}

const BLOCKED_EXTRA = new Set([
  "cookie",
  // authorization/proxy-authorization must not go into Playwright extraHTTPHeaders: those are static
  // per-context and attach to EVERY request (incl. cross-origin redirects/subresources) with no
  // origin filter, leaking a session_profile's credentials to third-party hosts. Cookie is already
  // blocked here and re-injected domain-scoped via addCookies(); header-Authorization on the browser
  // path is dropped (use Cookie auth or the http backend for Bearer-authenticated targets).
  "authorization",
  "proxy-authorization",
  "host",
  "content-length",
  "content-type",
  "transfer-encoding",
  "connection",
  "expect",
  "upgrade",
  "user-agent",
]);

/** @param {Record<string, string>} requestHeaders */
export function pickExtraHttpHeaders(requestHeaders) {
  const out = {};
  for (const [name, value] of Object.entries(requestHeaders)) {
    if (!BLOCKED_EXTRA.has(name.toLowerCase())) {
      out[name] = value;
    }
  }

  return out;
}

/**
 * Parse Cookie request header into Playwright cookie objects.
 * @param {string} cookieHeader
 * @param {string} pageUrl
 */
export function parseCookieHeader(cookieHeader, pageUrl) {
  const url = new URL(pageUrl);
  const domain = url.hostname;
  const path = url.pathname.endsWith("/") ? url.pathname : `${url.pathname.replace(/\/[^/]*$/, "") || ""}/`;

  return cookieHeader
    .split(";")
    .map((part) => part.trim())
    .filter(Boolean)
    .map((pair) => {
      const eq = pair.indexOf("=");
      if (eq <= 0) {
        return null;
      }

      const name = pair.slice(0, eq).trim();
      const value = pair.slice(eq + 1).trim();
      if (!name) {
        return null;
      }

      return {
        name,
        value,
        domain,
        path: path.startsWith("/") ? path : "/",
        secure: url.protocol === "https:",
        sameSite: "Lax",
      };
    })
    .filter(Boolean);
}
