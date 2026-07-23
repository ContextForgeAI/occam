/**
 * RSS 2.0 / Atom / RSS 1.0 (RDF) / JSON Feed parsing for the opt-in `json_feed` codec.
 *
 * Operates on an already-parsed XML document (jsdom XML mode) or a parsed JSON Feed object.
 * Returns `{ title, items[] }` or `null` when the input is not a recognizable feed.
 *
 * Each item: `{ title, link, publishedAt, summary, summaryHtml, summaryText, summaryMarkdown }`.
 * - `summaryHtml` — source HTML when present (CDATA / content_html / nested markup)
 * - `summaryText` — plain text, no tags, entities decoded
 * - `summaryMarkdown` — clean markdown (no raw HTML tags)
 * - `summary` — compat alias of `summaryText`
 *
 * Performance: one shared summary-normalizer per collect call; no per-item JSDOM/Turndown.
 */

const DEFAULT_MAX_ITEMS = 200;
const MAX_SUMMARY_LENGTH = 4000;

/**
 * Heuristic feed detection from a content-type and/or a body prefix, used before paying for a parse.
 * @param {string | null | undefined} contentType
 * @param {string | null | undefined} bodyPrefix first ~512 chars of the response body
 * @returns {boolean}
 */
export function looksLikeFeed(contentType, bodyPrefix) {
  const ct = (contentType ?? "").toLowerCase();
  if (
    ct.includes("rss")
    || ct.includes("atom")
    || ct.includes("feed+json")
    || ct.includes("application/xml")
    || ct.includes("text/xml")
  ) {
    return true;
  }
  const head = (bodyPrefix ?? "").slice(0, 512);
  const headLower = head.toLowerCase();
  if (headLower.includes("<rss") || headLower.includes("<feed") || headLower.includes("<rdf:rdf")) {
    return true;
  }
  // JSON Feed: version URL or content_html / items array near the top.
  if (headLower.includes("jsonfeed.org") || /"version"\s*:\s*"https?:\/\/jsonfeed\.org/i.test(head)) {
    return true;
  }
  return false;
}

/**
 * @param {string | null | undefined} contentType
 * @param {string | null | undefined} bodyPrefix
 * @returns {boolean}
 */
export function looksLikeJsonFeed(contentType, bodyPrefix) {
  const ct = (contentType ?? "").toLowerCase();
  if (ct.includes("feed+json")) {
    return true;
  }
  const head = (bodyPrefix ?? "").slice(0, 512);
  return /"version"\s*:\s*"https?:\/\/jsonfeed\.org/i.test(head)
    || (head.includes("{") && head.toLowerCase().includes("jsonfeed.org"));
}

/**
 * @param {Document | null} doc XML-parsed feed document
 * @param {{ baseUrl?: string, maxItems?: number }} [options]
 * @returns {{ title: string, items: FeedItem[] } | null}
 */
export function collectFeedItems(doc, options = {}) {
  if (!doc) {
    return null;
  }
  const maxItems = options.maxItems ?? DEFAULT_MAX_ITEMS;
  const baseUrl = options.baseUrl ?? "";

  const itemNodes = [...doc.getElementsByTagName("item")];
  const entryNodes = itemNodes.length === 0 ? [...doc.getElementsByTagName("entry")] : [];
  const nodes = itemNodes.length > 0 ? itemNodes : entryNodes;
  const isAtom = itemNodes.length === 0 && entryNodes.length > 0;

  const hasFeedRoot = doc.getElementsByTagName("channel").length > 0
    || doc.getElementsByTagName("feed").length > 0
    || doc.getElementsByTagName("rdf:RDF").length > 0;
  if (nodes.length === 0 && !hasFeedRoot) {
    return null;
  }

  const title = normalize(channelTitle(doc)) ?? "";
  const items = [];
  for (const node of nodes) {
    if (items.length >= maxItems) {
      break;
    }
    const rawSummary = childSummaryRaw(node, isAtom);
    items.push({
      title: normalize(childText(node, "title")) ?? "",
      link: itemLink(node, isAtom, baseUrl),
      publishedAt: normalize(
        childText(node, "pubDate")
        ?? childText(node, "published")
        ?? childText(node, "updated")
        ?? childText(node, "dc:date")) ?? "",
      ...formatSummaryFields(rawSummary),
    });
  }

  if (items.length === 0 && title.length === 0) {
    return null;
  }
  return { title, items };
}

/**
 * Parse a JSON Feed 1 / 1.1 object (already JSON.parse'd).
 * @param {object | null | undefined} feed
 * @param {{ baseUrl?: string, maxItems?: number }} [options]
 * @returns {{ title: string, items: FeedItem[] } | null}
 */
export function collectJsonFeed(feed, options = {}) {
  if (!feed || typeof feed !== "object" || !Array.isArray(feed.items)) {
    return null;
  }
  const version = String(feed.version ?? "");
  if (version && !version.includes("jsonfeed.org") && feed.items.length === 0) {
    return null;
  }

  const maxItems = options.maxItems ?? DEFAULT_MAX_ITEMS;
  const baseUrl = options.baseUrl ?? "";
  const title = normalize(feed.title) ?? "";
  const items = [];

  for (const raw of feed.items) {
    if (items.length >= maxItems) {
      break;
    }
    if (!raw || typeof raw !== "object") {
      continue;
    }
    const html = typeof raw.content_html === "string" ? raw.content_html
      : (typeof raw.summary === "string" && looksLikeHtml(raw.summary) ? raw.summary : "");
    const textHint = typeof raw.content_text === "string" ? raw.content_text
      : (typeof raw.summary === "string" && !looksLikeHtml(raw.summary) ? raw.summary : "");
    const fields = formatSummaryFields(html || textHint, { plainTextHint: textHint });
    const link = absolutize(String(raw.url ?? raw.external_url ?? ""), baseUrl);
    items.push({
      title: normalize(raw.title) ?? "",
      link: link || "",
      publishedAt: normalize(raw.date_published ?? raw.date_modified) ?? "",
      ...fields,
    });
  }

  if (items.length === 0 && title.length === 0) {
    return null;
  }
  return { title, items };
}

/**
 * Normalize a raw summary blob into the three public fields (+ compat `summary`).
 * Pure / sync — no DOM, no Turndown (feed summaries are typically simple HTML).
 * @param {string | null | undefined} raw
 * @param {{ plainTextHint?: string }} [opts]
 */
export function formatSummaryFields(raw, opts = {}) {
  const clamped = clampSummary(raw ?? "");
  if (!clamped) {
    const hint = clampSummary(opts.plainTextHint ?? "");
    if (!hint) {
      return { summary: "", summaryHtml: "", summaryText: "", summaryMarkdown: "" };
    }
    const text = decodeEntities(hint);
    return { summary: text, summaryHtml: "", summaryText: text, summaryMarkdown: text };
  }

  if (!looksLikeHtml(clamped)) {
    const text = decodeEntities(clamped);
    return { summary: text, summaryHtml: "", summaryText: text, summaryMarkdown: text };
  }

  const summaryHtml = clamped;
  const summaryText = htmlToPlainText(clamped);
  const summaryMarkdown = htmlToMarkdown(clamped);
  return {
    summary: summaryText,
    summaryHtml,
    summaryText,
    summaryMarkdown,
  };
}

function channelTitle(doc) {
  const channel = doc.getElementsByTagName("channel")[0]
    ?? doc.getElementsByTagName("feed")[0]
    ?? doc.documentElement;
  if (!channel) {
    return null;
  }
  for (const child of channel.children ?? []) {
    if (child.localName === "title") {
      return child.textContent;
    }
  }
  return null;
}

function childText(node, name) {
  for (const child of node.children ?? []) {
    if (child.localName === name || child.nodeName === name) {
      const text = child.textContent;
      if (text && text.trim().length > 0) {
        return text;
      }
    }
  }
  return null;
}

/**
 * Prefer serialized HTML when the element has markup children; otherwise textContent
 * (CDATA / escaped-HTML-as-text cases where tags remain in the string).
 */
function childSummaryRaw(node, isAtom) {
  const names = isAtom
    ? ["content", "summary", "description"]
    : ["description", "content:encoded", "summary", "content"];
  for (const name of names) {
    const local = name.includes(":") ? name.split(":")[1] : name;
    for (const child of node.children ?? []) {
      const match = child.localName === local
        || child.localName === name
        || child.nodeName === name
        || child.nodeName === local;
      if (!match) {
        continue;
      }
      if (child.children && child.children.length > 0) {
        const html = typeof child.innerHTML === "string" ? child.innerHTML : "";
        if (html.trim()) {
          return html;
        }
      }
      const text = child.textContent;
      if (text && text.trim()) {
        return text;
      }
    }
  }
  return null;
}

function itemLink(node, isAtom, baseUrl) {
  if (isAtom) {
    let fallback = "";
    for (const child of node.children ?? []) {
      if (child.localName !== "link") {
        continue;
      }
      const href = child.getAttribute?.("href");
      if (!href) {
        continue;
      }
      const rel = child.getAttribute?.("rel");
      if (!rel || rel === "alternate") {
        return absolutize(href, baseUrl);
      }
      if (!fallback) {
        fallback = absolutize(href, baseUrl);
      }
    }
    return fallback;
  }
  const link = normalize(childText(node, "link"));
  return link ? absolutize(link, baseUrl) : "";
}

function absolutize(value, baseUrl) {
  if (!value) {
    return "";
  }
  try {
    return new URL(value, baseUrl || undefined).href;
  } catch {
    return value;
  }
}

function clampSummary(value) {
  const text = (value ?? "").trim();
  if (!text) {
    return "";
  }
  return text.length > MAX_SUMMARY_LENGTH ? text.slice(0, MAX_SUMMARY_LENGTH) : text;
}

function normalize(value) {
  const text = (value ?? "").replace(/\s+/g, " ").trim();
  return text.length > 0 ? text : null;
}

function looksLikeHtml(value) {
  return /<[a-zA-Z][\s\S]*>/.test(value);
}

function htmlToPlainText(html) {
  let s = html;
  s = s.replace(/<(script|style)[\s\S]*?<\/\1>/gi, "");
  s = s.replace(/<br\s*\/?>/gi, "\n");
  s = s.replace(/<\/p>/gi, "\n\n");
  s = s.replace(/<\/div>/gi, "\n");
  s = s.replace(/<\/li>/gi, "\n");
  s = s.replace(/<[^>]+>/g, "");
  return collapseWs(decodeEntities(s));
}

/**
 * Lightweight HTML→Markdown for typical feed summaries (p/a/br/em/strong/lists).
 * Avoids Turndown/JSDOM per item — feed summaries are almost never deep document trees.
 */
function htmlToMarkdown(html) {
  let s = html;
  s = s.replace(/<(script|style)[\s\S]*?<\/\1>/gi, "");
  s = s.replace(/<br\s*\/?>/gi, "\n");
  s = s.replace(/<\/p>/gi, "\n\n");
  s = s.replace(/<p(?:\s[^>]*)?>/gi, "");
  s = s.replace(/<\/div>/gi, "\n");
  s = s.replace(/<div(?:\s[^>]*)?>/gi, "");
  s = s.replace(/<(strong|b)(?:\s[^>]*)?>/gi, "**");
  s = s.replace(/<\/(strong|b)>/gi, "**");
  s = s.replace(/<(em|i)(?:\s[^>]*)?>/gi, "_");
  s = s.replace(/<\/(em|i)>/gi, "_");
  s = s.replace(/<li(?:\s[^>]*)?>/gi, "- ");
  s = s.replace(/<\/li>/gi, "\n");
  s = s.replace(/<\/?[uo]l(?:\s[^>]*)?>/gi, "\n");
  s = s.replace(
    /<a\s+[^>]*href\s*=\s*["']([^"']+)["'][^>]*>([\s\S]*?)<\/a>/gi,
    (_, href, text) => `[${collapseWs(htmlToPlainText(text))}](${href})`,
  );
  s = s.replace(/<[^>]+>/g, "");
  s = decodeEntities(s);
  // Drop leftover angle-bracket noise; collapse blank runs.
  s = s.replace(/[ \t]+\n/g, "\n");
  s = s.replace(/\n{3,}/g, "\n\n");
  return s.trim();
}

function collapseWs(text) {
  return text.replace(/[ \t\f\v]+/g, " ").replace(/\n{3,}/g, "\n\n").replace(/ *\n */g, "\n").trim();
}

function decodeEntities(text) {
  return text
    .replace(/&nbsp;/gi, " ")
    .replace(/&amp;/gi, "&")
    .replace(/&lt;/gi, "<")
    .replace(/&gt;/gi, ">")
    .replace(/&quot;/gi, "\"")
    .replace(/&#39;/gi, "'")
    .replace(/&apos;/gi, "'")
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) => safeChar(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, dec) => safeChar(Number(dec)));
}

function safeChar(code) {
  if (!Number.isFinite(code) || code < 0 || code > 0x10ffff) {
    return "";
  }
  try {
    return String.fromCodePoint(code);
  } catch {
    return "";
  }
}
