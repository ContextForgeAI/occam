/**
 * P10-C5: inline iframe embeds as readable markdown blocks.
 */

/**
 * @param {Document} document
 * @param {string} baseUrl
 * @returns {string[]}
 */
export function collectIframeMarkdownBlocks(document, baseUrl) {
  const blocks = [];

  for (const iframe of document.querySelectorAll("iframe")) {
    const src = iframe.getAttribute("src")?.trim();
    const title =
      iframe.getAttribute("title")?.trim()
      || iframe.getAttribute("aria-label")?.trim()
      || "";
    const inner = iframe.innerHTML?.trim() ?? "";

    if (inner.length > 60 && !inner.toLowerCase().includes("<iframe")) {
      blocks.push(inner);
      continue;
    }

    if (!src) {
      continue;
    }

    let absolute = src;
    try {
      absolute = new URL(src, baseUrl).href;
    } catch {
      // keep raw src
    }

    const label = title.length > 0 ? title : absolute;
    blocks.push(`> **Embedded widget:** [${label}](${absolute})`);
  }

  return blocks;
}

/**
 * @param {Document} document
 * @param {string} baseUrl
 * @returns {number}
 */
export function processIframesInDocument(document, baseUrl) {
  return collectIframeMarkdownBlocks(document, baseUrl).length;
}

/**
 * @param {string} markdown
 * @param {string[]} blocks
 * @returns {string}
 */
export function appendIframeMarkdownBlocks(markdown, blocks) {
  if (!blocks.length) {
    return markdown;
  }

  return `${markdown.trimEnd()}\n\n${blocks.join("\n\n")}`.trim();
}
