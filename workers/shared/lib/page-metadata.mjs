/**
 * Page metadata extraction (publishedAt, author, lang, canonical) from a jsdom document.
 * Cheap, always-on, additive. Sources, in priority order:
 *  - publishedAt: meta article:published_time / itemprop datePublished / name=date / JSON-LD / <time datetime>
 *  - author:      meta name=author / article:author / JSON-LD author / rel=author
 *  - lang:        <html lang>
 *  - canonical:   <link rel=canonical> (absolute)
 * Returns an object with only the fields that resolved (others omitted).
 */
export function collectPageMetadata(doc, baseUrl) {
  if (!doc) {
    return undefined;
  }

  const ld = readJsonLd(doc);
  const meta = {
    publishedAt: firstNonEmpty(
      attr(doc, 'meta[property="article:published_time"]', "content"),
      attr(doc, 'meta[itemprop="datePublished"]', "content"),
      attr(doc, 'meta[name="date"]', "content"),
      attr(doc, 'meta[name="dc.date"]', "content"),
      ld.datePublished,
      attr(doc, "time[datetime]", "datetime"),
    ),
    author: firstNonEmpty(
      attr(doc, 'meta[name="author"]', "content"),
      attr(doc, 'meta[property="article:author"]', "content"),
      ld.author,
      text(doc, '[rel="author"]'),
    ),
    lang: normalize(doc.documentElement?.getAttribute?.("lang")),
    canonical: toAbsolute(attr(doc, 'link[rel="canonical"]', "href"), baseUrl),
  };

  // Drop null/empty keys so the wire stays clean.
  const out = {};
  for (const [k, v] of Object.entries(meta)) {
    if (v) {
      out[k] = v;
    }
  }
  return Object.keys(out).length > 0 ? out : undefined;
}

function attr(doc, selector, name) {
  try {
    return normalize(doc.querySelector(selector)?.getAttribute(name));
  } catch {
    return null;
  }
}

function text(doc, selector) {
  try {
    return normalize(doc.querySelector(selector)?.textContent);
  } catch {
    return null;
  }
}

function readJsonLd(doc) {
  try {
    for (const el of doc.querySelectorAll('script[type="application/ld+json"]')) {
      let parsed;
      try {
        parsed = JSON.parse(el.textContent ?? "");
      } catch {
        continue;
      }
      const nodes = Array.isArray(parsed) ? parsed : [parsed, ...(Array.isArray(parsed?.["@graph"]) ? parsed["@graph"] : [])];
      for (const node of nodes) {
        if (!node || typeof node !== "object") {
          continue;
        }
        const datePublished = normalize(node.datePublished);
        const author = normalize(typeof node.author === "string" ? node.author : node.author?.name);
        if (datePublished || author) {
          return { datePublished, author };
        }
      }
    }
  } catch {
    // best-effort
  }
  return { datePublished: null, author: null };
}

function firstNonEmpty(...values) {
  for (const v of values) {
    const n = normalize(v);
    if (n) {
      return n;
    }
  }
  return null;
}

function normalize(value) {
  const t = (value ?? "").toString().replace(/\s+/g, " ").trim();
  return t.length > 0 ? t : null;
}

function toAbsolute(value, baseUrl) {
  const v = normalize(value);
  if (!v) {
    return null;
  }
  try {
    return new URL(v, baseUrl).href;
  } catch {
    return v;
  }
}
