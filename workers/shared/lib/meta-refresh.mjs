/**
 * Follow HTML meta refresh redirects (client-side) up to maxHops.
 * @param {string} html
 * @param {string} baseUrl
 * @returns {string|null} target URL or null
 */
export function parseMetaRefreshTarget(html, baseUrl) {
  if (!html || !baseUrl) {
    return null;
  }

  const match = html.match(
    /<meta[^>]+http-equiv=["']refresh["'][^>]*content=["']([^"']+)["'][^>]*>/i,
  );
  if (!match) {
    return null;
  }

  const content = match[1];
  const urlPart = content.match(/url\s*=\s*([^;'">\s]+)/i);
  if (!urlPart) {
    return null;
  }

  try {
    return new URL(urlPart[1].trim(), baseUrl).href;
  } catch {
    return null;
  }
}
