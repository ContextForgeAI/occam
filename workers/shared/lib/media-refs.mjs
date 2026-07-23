/**
 * Collect downloadable media references from a DOM snapshot (no binary fetch).
 * @param {Document} document
 * @param {string} baseUrl
 * @param {{ root?: Element | null, maxRefs?: number }} [options]
 * @returns {Array<{ url: string, kind: string, alt: string | null, context_heading: string | null, selector_hint: string | null }>}
 */
export function collectMediaRefs(document, baseUrl, options = {}) {
  const maxRefs = options.maxRefs ?? 32;
  const root = options.root ?? pickContentRoot(document);
  if (!root) {
    return [];
  }

  const headingIndex = buildHeadingIndex(root);
  const seen = new Set();
  const refs = [];

  const pushRef = (candidate) => {
    if (!candidate?.url || refs.length >= maxRefs) {
      return;
    }
    const absolute = toAbsoluteUrl(candidate.url, baseUrl);
    if (!absolute || !isHttpUrl(absolute) || seen.has(absolute)) {
      return;
    }
    seen.add(absolute);
    refs.push({
      url: absolute,
      kind: candidate.kind,
      alt: candidate.alt ?? null,
      context_heading: candidate.contextHeading ?? findContextHeading(candidate.element, headingIndex),
      selector_hint: candidate.selectorHint ?? describeElement(candidate.element),
    });
  };

  for (const img of root.querySelectorAll("img, picture img")) {
    const url = resolveImageUrl(img, baseUrl);
    if (!url) {
      continue;
    }
    pushRef({
      url,
      kind: "image",
      alt: normalizeText(img.getAttribute("alt") ?? img.getAttribute("title")),
      element: img,
      selectorHint: "img",
    });
  }

  for (const video of root.querySelectorAll("video")) {
    const url = resolveMediaSrc(video, baseUrl) ?? resolveChildSource(video, "video", baseUrl);
    if (!url) {
      continue;
    }
    pushRef({
      url,
      kind: "video",
      alt: normalizeText(video.getAttribute("title") ?? video.getAttribute("aria-label")),
      element: video,
      selectorHint: "video",
    });
  }

  for (const audio of root.querySelectorAll("audio")) {
    const url = resolveMediaSrc(audio, baseUrl) ?? resolveChildSource(audio, "audio", baseUrl);
    if (!url) {
      continue;
    }
    pushRef({
      url,
      kind: "audio",
      alt: normalizeText(audio.getAttribute("title") ?? audio.getAttribute("aria-label")),
      element: audio,
      selectorHint: "audio",
    });
  }

  for (const anchor of root.querySelectorAll("a[href]")) {
    const href = anchor.getAttribute("href")?.trim();
    if (!href || href.startsWith("#")) {
      continue;
    }
    const absolute = toAbsoluteUrl(href, baseUrl);
    if (!absolute || !isHttpUrl(absolute) || seen.has(absolute)) {
      continue;
    }
    const kind = classifyDownloadKind(absolute, anchor);
    if (!kind) {
      continue;
    }
    pushRef({
      url: absolute,
      kind,
      alt: normalizeText(anchor.textContent ?? anchor.getAttribute("title")),
      element: anchor,
      selectorHint: kind === "pdf" ? "a[href$=.pdf]" : "a[download]",
    });
  }

  return refs;
}

function pickContentRoot(document) {
  const selectors = [
    "article",
    '[role="main"]',
    "main",
    ".docs-content",
    ".markdown-body",
    "#main-content",
    "#content",
  ];
  for (const selector of selectors) {
    const el = document.querySelector(selector);
    if ((el?.textContent?.trim().length ?? 0) > 120) {
      return el;
    }
  }
  return document.body ?? null;
}

function buildHeadingIndex(root) {
  /** @type {Array<{ level: number, text: string, element: Element }>} */
  const headings = [];
  for (const el of root.querySelectorAll("h1, h2, h3, h4, h5, h6")) {
    const text = normalizeText(el.textContent);
    if (!text) {
      continue;
    }
    const level = Number(el.tagName.slice(1));
    headings.push({ level, text, element: el });
  }
  return headings;
}

function findContextHeading(element, headingIndex) {
  if (!element || headingIndex.length === 0) {
    return null;
  }

  const docPosition = element.compareDocumentPosition?.bind(element);
  let best = null;
  const preceding = 2; // Node.DOCUMENT_POSITION_PRECEDING
  for (const heading of headingIndex) {
    if (docPosition && (docPosition(heading.element) & preceding)) {
      if (!best || heading.level <= best.level) {
        best = heading;
      }
    }
  }

  if (best) {
    return `${"#".repeat(best.level)} ${best.text}`;
  }

  const parentHeading = element.closest("section, article, div")?.querySelector("h1, h2, h3, h4, h5, h6");
  const parentText = normalizeText(parentHeading?.textContent);
  if (!parentText || !parentHeading) {
    return null;
  }
  const level = Number(parentHeading.tagName.slice(1));
  return `${"#".repeat(level)} ${parentText}`;
}

function resolveImageUrl(img, baseUrl) {
  return (
    resolveMediaSrc(img, baseUrl)
    ?? pickSrcsetUrl(img.getAttribute("srcset"), baseUrl)
    ?? null
  );
}

function resolveMediaSrc(el, baseUrl) {
  const candidates = [
    el.getAttribute("src"),
    el.getAttribute("data-src"),
    el.getAttribute("data-lazy-src"),
    el.getAttribute("data-original"),
    el.getAttribute("data-url"),
  ];
  for (const value of candidates) {
    const trimmed = value?.trim();
    if (!trimmed || trimmed.startsWith("data:")) {
      continue;
    }
    const absolute = toAbsoluteUrl(trimmed, baseUrl);
    if (absolute && isHttpUrl(absolute)) {
      return absolute;
    }
  }
  return null;
}

function resolveChildSource(container, kind, baseUrl) {
  for (const source of container.querySelectorAll("source")) {
    const type = source.getAttribute("type") ?? "";
    if (kind === "video" && type && !type.startsWith("video/")) {
      continue;
    }
    if (kind === "audio" && type && !type.startsWith("audio/")) {
      continue;
    }
    const url = resolveMediaSrc(source, baseUrl) ?? pickSrcsetUrl(source.getAttribute("srcset"), baseUrl);
    if (url) {
      return url;
    }
  }
  return null;
}

function pickSrcsetUrl(srcset, baseUrl) {
  if (!srcset) {
    return null;
  }
  const first = srcset.split(",")[0]?.trim().split(/\s+/)[0];
  if (!first || first.startsWith("data:")) {
    return null;
  }
  return toAbsoluteUrl(first, baseUrl);
}

function classifyDownloadKind(absoluteUrl, anchor) {
  const path = safePathname(absoluteUrl).toLowerCase();
  if (path.endsWith(".pdf")) {
    return "pdf";
  }
  if (anchor.hasAttribute("download")) {
    return "file";
  }
  const ext = path.match(/\.([a-z0-9]{2,5})(?:$|\?)/i)?.[1] ?? "";
  if (["zip", "tar", "gz", "tgz", "7z", "rar", "dmg", "exe", "msi", "deb", "rpm"].includes(ext)) {
    return "file";
  }
  if (["mp4", "webm", "mov", "m4v", "ogv"].includes(ext)) {
    return "video";
  }
  if (["mp3", "wav", "ogg", "m4a", "aac", "flac"].includes(ext)) {
    return "audio";
  }
  if (["png", "jpg", "jpeg", "gif", "webp", "svg", "avif", "bmp", "ico"].includes(ext)) {
    return "image";
  }
  return null;
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

function safePathname(url) {
  try {
    return new URL(url).pathname;
  } catch {
    return "";
  }
}

function normalizeText(value) {
  const text = (value ?? "").replace(/\s+/g, " ").trim();
  return text.length > 0 ? text : null;
}

function describeElement(element) {
  if (!element?.tagName) {
    return null;
  }
  const tag = element.tagName.toLowerCase();
  if (tag === "img") {
    return "img";
  }
  if (tag === "a") {
    return element.hasAttribute("download") ? "a[download]" : "a[href]";
  }
  return tag;
}
