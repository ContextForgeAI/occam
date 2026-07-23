import { Readability } from "@mozilla/readability";
import { JSDOM, VirtualConsole } from "jsdom";
import TurndownService from "turndown";
import { stripChrome, stripConsentOnly, stripPromoBanners } from "./html-preprocess.mjs";
import { appendIframeMarkdownBlocks, collectIframeMarkdownBlocks } from "../../shared/lib/process-iframes.mjs";
import { collectMediaRefs } from "../../shared/lib/media-refs.mjs";
import { collectBlocks } from "../../shared/lib/dom-blocks.mjs";
import { collectTables } from "../../shared/lib/dom-tables.mjs";
import { addTableRule } from "../../shared/lib/turndown-table-rule.mjs";
import { collectPageMetadata } from "../../shared/lib/page-metadata.mjs";
import { collectAccessEvidence } from "../../shared/lib/access-evidence.mjs";
function pickMainContent(document, selectors = []) {
  for (const selector of selectors) {
    const el = document.querySelector(selector);
    const len = el?.textContent?.trim().length ?? 0;
    if (len > 200) {
      return { title: document.title, content: el.innerHTML };
    }
    if (len > 120 && el?.querySelectorAll("h2, h3, a").length >= 4) {
      return { title: document.title, content: el.innerHTML };
    }
  }
  return null;
}

export function extractMarkdownFromHtml(html, url, options = {}) {
  const {
    stripChrome: stripChromeOption = false,
    stripConsentOnly: stripConsentOnlyOption = false,
    useClone = true,
    contentSelectors = [],
    strictSelectors = false,
    processIframes = true,
    wantBlocks = false,
    wantTables = false,
  } = options;
  const virtualConsole = new VirtualConsole();
  virtualConsole.on("jsdomError", () => {});
  const dom = new JSDOM(html, { url, virtualConsole });
  const doc = dom.window.document;
  const access = collectAccessEvidence(doc, { requestedUrl: url, finalUrl: url });

  // CSS-hide in the live page does not affect JSDOM — always strip CMP nodes from the snapshot.
  stripConsentOnly(doc);
  stripPromoBanners(doc);
  if (stripChromeOption) {
    stripChrome(doc);
  }
  const iframeBlocks = processIframes ? collectIframeMarkdownBlocks(doc, url) : [];

  const parseDoc = useClone ? doc.cloneNode(true) : doc;
  const selectorList = strictSelectors && contentSelectors.length > 0
    ? contentSelectors
    : [
        ...contentSelectors,
        "article",
        '[role="main"]',
        "main",
        ".docs-content",
        '[class*="docs-content"]',
        ".docs-post-content",
        ".td-content",
        ".markdown-body",
        '[data-testid="primary-column"]',
        '[data-testid="page-content"]',
        ".main-docs-content",
        "#main-content",
        "#content",
        "div.section",
        "div.sect1",
        "#docs",
        "table.doccontent",
        ".article-body",
        ".post-content",
      ];
  // Collect blocks/tables from the pristine (post-strip) snapshot BEFORE Readability mutates
  // parseDoc destructively (Q-025) — same order fix as the http worker; otherwise collection ran
  // on a gutted DOM and returned 0 blocks for some page shapes.
  const blockRoot = wantBlocks
    ? (pickMainContentElement(parseDoc, selectorList)
        ?? parseDoc.querySelector("article, [role='main'], main")
        ?? parseDoc.body)
    : null;
  const blocks = blockRoot ? collectBlocks(blockRoot, { doc: parseDoc, baseUrl: url }) : undefined;
  const tableRoot = wantTables
    ? (pickMainContentElement(parseDoc, selectorList)
        ?? parseDoc.querySelector("article, [role='main'], main")
        ?? parseDoc.body)
    : null;
  const tables = tableRoot ? collectTables(tableRoot, { doc: parseDoc }) : undefined;

  let article =
    contentSelectors.length > 0 || strictSelectors ? pickMainContent(parseDoc, selectorList) : null;
  if (!article?.content && !strictSelectors) {
    article = new Readability(parseDoc).parse();
  }
  if (!article?.content && !strictSelectors) {
    article = pickMainContent(parseDoc, selectorList);
  }
  if (!article?.content) {
    return null;
  }

  const contentRoot =
    contentSelectors.length > 0 || strictSelectors
      ? pickMainContentElement(parseDoc, selectorList)
      : parseDoc.querySelector("article, main, [role='main'], #content") ?? parseDoc.body;
  let mediaRoot = contentRoot;
  if ((!mediaRoot || mediaRoot === parseDoc.body) && article.content) {
    const wrapper = parseDoc.createElement("div");
    wrapper.innerHTML = article.content;
    mediaRoot = wrapper;
  }
  const media_refs = collectMediaRefs(parseDoc, url, { root: mediaRoot });
  const meta = collectPageMetadata(parseDoc, url);

  const turndown = new TurndownService({ headingStyle: "atx", codeBlockStyle: "fenced" });
  addTableRule(turndown);
  const body = turndown.turndown(article.content);
  const title = (article.title ?? "").trim();
  let markdown = title.length > 0 ? `# ${title}\n\n${body}` : body;
  markdown = appendIframeMarkdownBlocks(markdown, iframeBlocks);
  return {
    title,
    markdown,
    text_length: markdown.length,
    media_refs,
    blocks,
    tables,
    meta,
    access,
  };
}

function pickMainContentElement(document, selectors = []) {
  for (const selector of selectors) {
    const el = document.querySelector(selector);
    const len = el?.textContent?.trim().length ?? 0;
    if (len > 120) {
      return el;
    }
  }
  return null;
}
