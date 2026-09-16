/**
 * P1: harvest first-party cookies after a browser navigation so the host can
 * do one HTTP retry. Never log values. Third-party cookie domains are dropped.
 */

/**
 * @param {string | undefined} cookieDomain
 * @param {string} hostname
 * @returns {boolean}
 */
export function cookieMatchesHost(cookieDomain, hostname) {
  const host = String(hostname || "").toLowerCase();
  const domain = String(cookieDomain || host).replace(/^\./, "").toLowerCase();
  if (!host || !domain) {
    return false;
  }
  return host === domain || host.endsWith(`.${domain}`);
}

/**
 * @param {Array<{ name?: string, value?: string, domain?: string }> | null | undefined} cookies
 * @param {string} pageUrl
 */
export function filterCookiesForUrl(cookies, pageUrl) {
  let hostname;
  try {
    hostname = new URL(pageUrl).hostname;
  } catch {
    return [];
  }

  const seen = new Set();
  const out = [];
  for (const cookie of cookies ?? []) {
    const name = String(cookie?.name ?? "").trim();
    if (!name || seen.has(name)) {
      continue;
    }
    if (!cookieMatchesHost(cookie?.domain || hostname, hostname)) {
      continue;
    }
    seen.add(name);
    out.push(cookie);
  }
  return out;
}

/**
 * @param {Array<{ name?: string, value?: string }>} cookies
 * @returns {string}
 */
export function toCookieHeader(cookies) {
  return (cookies ?? [])
    .filter((cookie) => String(cookie?.name ?? "").trim())
    .map((cookie) => `${String(cookie.name).trim()}=${cookie.value ?? ""}`)
    .join("; ");
}

/**
 * @param {import("playwright").BrowserContext} context
 * @param {string} requestedUrl
 * @param {string} [finalUrl]
 * @returns {Promise<{ header: string, count: number }>}
 */
export async function harvestCookiesForRetry(context, requestedUrl, finalUrl) {
  if (typeof context?.cookies !== "function") {
    return { header: "", count: 0 };
  }

  const cookies = await context.cookies();
  const targets = [finalUrl, requestedUrl].filter(Boolean);
  const merged = [];
  const seen = new Set();
  for (const target of targets) {
    for (const cookie of filterCookiesForUrl(cookies, target)) {
      const name = String(cookie.name).trim();
      if (seen.has(name)) {
        continue;
      }
      seen.add(name);
      merged.push(cookie);
    }
  }

  const header = toCookieHeader(merged);
  return { header, count: merged.length };
}
