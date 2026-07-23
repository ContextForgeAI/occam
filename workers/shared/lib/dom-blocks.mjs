/**
 * DOM-derived structured blocks for RAG citations (R3, fork B).
 *
 * Walks the article content DOM (the same `mediaRoot` both extractors already build for
 * media_refs) and emits ordered blocks `{ type, text, links[], source_selector }`. Unlike the
 * markdown-derived chunker, `source_selector` is a real CSS path:
 *  - when the root is connected to the live document, the selector is document-absolute and
 *    round-trips through `document.querySelector` (verified) — a true page selector;
 *  - when the root is a detached Readability wrapper, the selector is anchored to that wrapper
 *    (the live page cannot be addressed because Readability re-serialised the fragment).
 */

const DEFAULT_MAX_BLOCKS = 400;
const MAX_TEXT_LENGTH = 8000;
const MAX_LINKS_PER_BLOCK = 32;

const SKIP_TAGS = new Set([
  "script",
  "style",
  "noscript",
  "template",
  "svg",
  "nav",
  "footer",
  "header",
  "aside",
  "form",
  "button",
  "input",
  "select",
  "textarea",
  "iframe",
]);

/** @returns {"heading"|"paragraph"|"list_item"|"code"|"quote"|"table"|"figure"|null} */
function blockType(tag) {
  if (tag === "h1" || tag === "h2" || tag === "h3" || tag === "h4" || tag === "h5" || tag === "h6") {
    return "heading";
  }
  switch (tag) {
    case "p":
      return "paragraph";
    case "li":
    case "dt":
    case "dd":
      return "list_item";
    case "pre":
      return "code";
    case "blockquote":
      return "quote";
    case "table":
      return "table";
    case "figure":
      return "figure";
    default:
      return null;
  }
}

/**
 * @param {Element | null} root content root (connected element or detached wrapper)
 * @param {{ doc?: Document, baseUrl?: string, maxBlocks?: number }} [options]
 * @returns {Array<{ type: string, text: string, links: Array<{ text: string, href: string }>, source_selector: string }>}
 */
export function collectBlocks(root, options = {}) {
  if (!root) {
    return [];
  }
  const doc = options.doc ?? root.ownerDocument ?? null;
  if (!doc) {
    return [];
  }
  const baseUrl = options.baseUrl ?? doc.baseURI ?? "";
  const maxBlocks = options.maxBlocks ?? DEFAULT_MAX_BLOCKS;
  const connected = isConnected(root);
  const ctx = { doc, root, baseUrl, connected };

  const blocks = [];
  walk(root, blocks, maxBlocks, ctx);
  return blocks;
}

function walk(node, out, maxBlocks, ctx) {
  for (let child = node.firstElementChild; child; child = child.nextElementSibling) {
    if (out.length >= maxBlocks) {
      return;
    }
    const tag = child.localName;
    if (SKIP_TAGS.has(tag)) {
      continue;
    }

    const type = blockType(tag);
    if (type) {
      pushBlock(child, type, out, ctx);
      // Recognised blocks are terminal: do not descend (avoids emitting a <p> inside a <li>
      // twice). Lists are containers, handled below.
      continue;
    }

    if (tag === "ul" || tag === "ol" || tag === "dl") {
      walk(child, out, maxBlocks, ctx);
      continue;
    }

    // Generic container (div, section, article, main, ...): descend.
    walk(child, out, maxBlocks, ctx);
  }
}

function pushBlock(el, type, out, ctx) {
  const text = normalizeText(el.textContent);
  if (!text) {
    return;
  }
  const block = {
    type,
    text: text.length > MAX_TEXT_LENGTH ? text.slice(0, MAX_TEXT_LENGTH) : text,
    links: collectLinks(el, ctx.baseUrl),
    source_selector: selectorFor(el, ctx),
  };
  // Heading level (h1..h6 → 1..6) so a downstream codec can rebuild the heading hierarchy instead of
  // flattening every heading to one level. Additive: only emitted for headings; absent otherwise.
  if (type === "heading") {
    const level = Number(el.localName.slice(1));
    if (Number.isInteger(level) && level >= 1 && level <= 6) {
      block.level = level;
    }
  }
  out.push(block);
}

function collectLinks(el, baseUrl) {
  const links = [];
  const seen = new Set();
  for (const anchor of el.querySelectorAll("a[href]")) {
    if (links.length >= MAX_LINKS_PER_BLOCK) {
      break;
    }
    const raw = anchor.getAttribute("href")?.trim();
    if (!raw || raw.startsWith("#") || raw.startsWith("javascript:")) {
      continue;
    }
    const href = toAbsoluteUrl(raw, baseUrl);
    if (!href || !isHttpUrl(href) || seen.has(href)) {
      continue;
    }
    seen.add(href);
    links.push({ text: normalizeText(anchor.textContent) ?? "", href });
  }
  return links;
}

/**
 * Builds a CSS selector for `el`. When the root is connected, returns a document-absolute path
 * verified to round-trip; otherwise a wrapper-relative best-effort path.
 */
function selectorFor(el, ctx) {
  if (ctx.connected) {
    const absolute = buildPath(el, null, ctx);
    if (absolute && verify(ctx.doc, absolute, el)) {
      return absolute;
    }
  }
  // Detached wrapper (or verification failed): anchor inside the content root.
  return buildPath(el, ctx.root, ctx);
}

/**
 * Walks ancestors of `el` up to (but not past) `stopAt`. When `stopAt` is null, walks to the
 * document root, short-circuiting on a unique id.
 */
function buildPath(el, stopAt, ctx) {
  const segments = [];
  let node = el;
  while (node && node.nodeType === 1) {
    if (stopAt === null && node.id && isUniqueId(ctx.doc, node.id)) {
      segments.unshift(`#${cssEscape(node.id, ctx)}`);
      break;
    }

    const tag = node.localName;
    let nth = 1;
    for (let sib = node.previousElementSibling; sib; sib = sib.previousElementSibling) {
      if (sib.localName === tag) {
        nth += 1;
      }
    }
    segments.unshift(`${tag}:nth-of-type(${nth})`);

    if (node === stopAt) {
      break;
    }
    node = node.parentElement;
  }
  return segments.join(" > ");
}

/**
 * Public selector helper reused by sibling codecs (e.g. dom-tables) so they emit the same
 * document-absolute / wrapper-relative `source_selector` semantics as blocks.
 * @param {Element | null} el
 * @param {{ doc?: Document, root?: Element | null }} [options]
 * @returns {string}
 */
export function buildElementSelector(el, options = {}) {
  if (!el) {
    return "";
  }
  const doc = options.doc ?? el.ownerDocument ?? null;
  if (!doc) {
    return "";
  }
  const ctx = { doc, root: options.root ?? null, baseUrl: "", connected: isConnected(el) };
  return selectorFor(el, ctx);
}

function verify(doc, selector, el) {
  try {
    return doc.querySelector(selector) === el;
  } catch {
    return false;
  }
}

function isUniqueId(doc, id) {
  try {
    return doc.querySelectorAll(`#${cssEscapeRaw(doc, id)}`).length === 1;
  } catch {
    return false;
  }
}

function isConnected(el) {
  if (typeof el.isConnected === "boolean") {
    return el.isConnected;
  }
  // Fallback: walk to the top and check it is the document element.
  let node = el;
  while (node.parentElement) {
    node = node.parentElement;
  }
  return node === el.ownerDocument?.documentElement;
}

function cssEscape(value, ctx) {
  return cssEscapeRaw(ctx.doc, value);
}

function cssEscapeRaw(doc, value) {
  const native = doc?.defaultView?.CSS?.escape;
  if (typeof native === "function") {
    return native(value);
  }
  // Minimal fallback: escape anything that is not a safe identifier char.
  return String(value).replace(/[^a-zA-Z0-9_-]/g, (ch) => `\\${ch}`);
}

function toAbsoluteUrl(value, baseUrl) {
  try {
    return new URL(value, baseUrl).href;
  } catch {
    return null;
  }
}

function isHttpUrl(value) {
  return value.startsWith("http://") || value.startsWith("https://");
}

function normalizeText(value) {
  const text = (value ?? "").replace(/\s+/g, " ").trim();
  return text.length > 0 ? text : null;
}
