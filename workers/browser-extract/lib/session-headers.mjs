import { parseCookieHeader, pickExtraHttpHeaders, readRequestHeadersFile } from "../../shared/lib/request-headers.mjs";
import { getDefaultUserAgent } from "../../shared/lib/default-fetch-headers.mjs";

const DEFAULT_USER_AGENT = getDefaultUserAgent();

/**
 * Resolve browser context options from optional headers file.
 * @param {string | null | undefined} headersFile
 */
export async function resolveBrowserContextOptions(headersFile) {
  const headers = await readRequestHeadersFile(headersFile);
  const userAgent = headers["User-Agent"] ?? headers["user-agent"] ?? DEFAULT_USER_AGENT;
  const extra = pickExtraHttpHeaders(headers);
  return {
    userAgent,
    extraHTTPHeaders: Object.keys(extra).length > 0 ? extra : undefined,
    headers,
  };
}

/**
 * Inject Cookie header into Playwright context before navigation.
 * @param {import("playwright").BrowserContext} context
 * @param {string} url
 * @param {Record<string, string>} headers
 */
export async function applySessionCookies(context, url, headers) {
  const cookieHeader = headers.Cookie ?? headers.cookie;
  if (!cookieHeader) {
    return { cookiesAdded: 0 };
  }

  const cookies = parseCookieHeader(String(cookieHeader), url);
  if (cookies.length === 0) {
    return { cookiesAdded: 0 };
  }

  await context.addCookies(cookies);
  return { cookiesAdded: cookies.length };
}
