import { Readability } from "@mozilla/readability";
import { readFile } from "node:fs/promises";
import { JSDOM, VirtualConsole } from "jsdom";
import TurndownService from "turndown";
import { appendIframeMarkdownBlocks, collectIframeMarkdownBlocks } from "../../shared/lib/process-iframes.mjs";
import { mergeFetchHeaders, readRequestHeadersFile, stripCrossOriginSensitiveHeaders } from "../../shared/lib/request-headers.mjs";
import { getDefaultFetchHeaders } from "../../shared/lib/default-fetch-headers.mjs";
import { parseMetaRefreshTarget } from "../../shared/lib/meta-refresh.mjs";
import { genericMarkdownPrune } from "../../shared/lib/generic-markdown-prune.mjs";
import {
  applySeedPostMarkdown,
  getContentSelectorsForUrl,
  getDomStripSelectorsForUrl,
  isStrictPlaybookOverlay,
  stripDomSelectors,
  wasOverlayApplied,
} from "../../shared/lib/playbook-seed.mjs";
import { plainTextToMarkdown, shouldPassThroughPlainText } from "../../shared/lib/plain-text-pass-through.mjs";
import { EgressProxyError, egressFetch } from "../../shared/lib/egress-proxy.mjs";
import { collectMediaRefs } from "../../shared/lib/media-refs.mjs";
import {
  readResponseBodyForExtract,
  resolveMaxResponseBytes,
  ResponseTooLargeError,
} from "../../shared/lib/response-body-cap.mjs";
import { isPrivateIp, isPrivateUrlBlocked, shouldSkipPrivateIpCheck, resolveAndValidateHost, createPinnedDispatcher, pinnedDispatcherForUrl, SsrfBlockedError } from "../../shared/lib/private-ip.mjs";
import { runPlugins } from "../../shared/lib/plugins-runner.mjs";
import { collectBlocks } from "../../shared/lib/dom-blocks.mjs";
import { collectTables } from "../../shared/lib/dom-tables.mjs";
import { addTableRule } from "../../shared/lib/turndown-table-rule.mjs";
import { collectFeedItems, collectJsonFeed, looksLikeFeed, looksLikeJsonFeed } from "../../shared/lib/feed-items.mjs";
import { shouldTryPdf, extractPdfMarkdown } from "../../shared/lib/pdf-extract.mjs";
import { collectPageMetadata } from "../../shared/lib/page-metadata.mjs";
import { collectAccessEvidence } from "../../shared/lib/access-evidence.mjs";

/** True when the given OCCAM feature is active for this run. */
function featureActive(features, name) {
  const raw = features ?? process.env.OCCAM_FEATURES ?? "";
  return raw
    .split(",")
    .map((f) => f.trim().toLowerCase())
    .includes(name);
}

/**
 * Validates a URL against private IP/host policy.
 * Returns a failure object if the URL is blocked, null otherwise.
 * Respects OCCAM_ALLOW_PRIVATE_URLS environment variable.
 * @param {string} url
 * @returns {{ ok: false, backend: string, failure: string, latency_ms: number } | null}
 */
function validateFinalUrl(url, started) {
  // If private URLs are allowed via env var, skip all checks
  if (!isPrivateUrlBlocked()) {
    return null;
  }
  
  try {
    const parsed = new URL(url);
    const hostname = parsed.hostname;
    
    // Check for localhost, .local, .internal
    if (hostname === "localhost" || 
        hostname.endsWith(".local") || 
        hostname.endsWith(".internal")) {
      return {
        ok: false,
        backend: "node_readability_turndown",
        failure: "private_url_blocked",
        latency_ms: Math.round(performance.now() - started),
      };
    }
    
    // Check if hostname is an IP address
    if (/^\d+\.\d+\.\d+\.\d+$/.test(hostname) || /^\[?[0-9a-fA-F:]+\]?$/.test(hostname)) {
      if (isPrivateIp(hostname.replace(/[\[\]]/g, ''))) {
        return {
          ok: false,
          backend: "node_readability_turndown",
          failure: "private_url_blocked",
          latency_ms: Math.round(performance.now() - started),
        };
      }
    }
  } catch {
    // Invalid URL - treat as blocked
    return {
      ok: false,
      backend: "node_readability_turndown",
      failure: "private_url_blocked",
      latency_ms: Math.round(performance.now() - started),
    };
  }
  return null;
}

/**
 * @param {{
 *   url: string,
 *   htmlFile?: string | null,
 *   finalUrl?: string | null,
 *   headersFile?: string | null,
 * }} options
 */
export async function runHttpExtract(options) {
  const result = await runHttpExtractCore(options);
  // A3: honest provenance — stamp overlay_applied on success when the argv overlay matched this host.
  // HTTP overlays always run one-shot (the daemon-skip guard), so the argv global is the source here.
  if (result && typeof result === "object" && result.ok) {
    result.overlay_applied = wasOverlayApplied(result?.url?.final ?? options.url);
  }
  return result;
}

async function runHttpExtractCore(options) {
  const { url } = options;
  const started = performance.now();
  // Split point for the "with-internet vs without" breakdown: timestamp set the moment the
  // response body is fully in memory. Everything before = network I/O (DNS + connect + TLS +
  // TTFB + download); everything after = CPU (JSDOM + Readability + Turndown + prune).
  let fetchDoneAt = null;
  const timing = () => {
    const now = performance.now();
    return {
      latency_ms: Math.round(now - started),
      network_ms: fetchDoneAt == null ? 0 : Math.round(fetchDoneAt - started),
      parse_ms: fetchDoneAt == null ? 0 : Math.round(now - fetchDoneAt),
    };
  };
  const maxResponseBytes = resolveMaxResponseBytes();
  /** @type {import('undici').Agent | undefined} */
  let pinnedDispatcher;

  try {
    let html;
    let finalUrl;
    let responseContentType = "";
    /** @type {{ truncated: boolean, bytesRead: number, maxBytes: number }} */
    let sizeMeta = { truncated: false, bytesRead: 0, maxBytes: maxResponseBytes };

    if (options.htmlFile) {
      html = await readFile(options.htmlFile, "utf8");
      finalUrl = options.finalUrl ?? url;
      sizeMeta.bytesRead = Buffer.byteLength(html, "utf8");
      fetchDoneAt = performance.now(); // cached HTML: no network leg
    } else {
      // SSRF / DNS-rebinding protection: resolve the host across BOTH families and pin the connection to the
      // resolved IPs so a re-resolve can't rebind to an internal target. Pinning ALWAYS happens;
      // OCCAM_ALLOW_PRIVATE_URLS=1 only relaxes the private-IP rejection (local testing) — it does not turn
      // pinning off, so the pinned path is the same one every default user (and the CI gate) runs.
      try {
        const allowPrivate = shouldSkipPrivateIpCheck();
        const pinnedHost = new URL(url).hostname;
        const records = await resolveAndValidateHost(pinnedHost, { allowPrivate });
        pinnedDispatcher = await createPinnedDispatcher(pinnedHost, records, { allowPrivate });
      } catch (error) {
        return {
          ok: false,
          backend: "node_readability_turndown",
          failure: error instanceof SsrfBlockedError ? error.failure : "dns_resolution_failed",
          latency_ms: Math.round(performance.now() - started),
        };
      }

      const requestHeaders = await readRequestHeadersFile(options.headersFile ?? undefined);
      const defaults = getDefaultFetchHeaders();
      const defaultHeaders = {
        "User-Agent": defaults.userAgent,
        Accept: defaults.accept,
      };
      const response = await egressFetch(url, {
        headers: mergeFetchHeaders(requestHeaders, defaultHeaders),
        signal: AbortSignal.timeout(30_000),
        ...(pinnedDispatcher ? { dispatcher: pinnedDispatcher } : {}),
      });

      if (!response.ok) {
        // Release the body we are deliberately not reading. Without this the request stays in-flight:
        // it leaks a connection in the long-lived daemon, and it is what made the pool teardown below
        // hang forever on a large error page (see the destroy() note in `finally`).
        await response.body?.cancel().catch(() => {});
        return {
          ok: false,
          backend: "node_readability_turndown",
          failure: `http_${response.status}`,
          status_code: response.status,
          latency_ms: Math.round(performance.now() - started),
        };
      }

      responseContentType = response.headers.get("content-type") ?? "";

      // PDF path: read as binary and extract text (Readability cannot parse PDF). One-way (the
      // body stream is consumed), so only commit when confident; always returns a result.
      if (shouldTryPdf(responseContentType, response.url)) {
        return await extractPdfResponse(response, url, started, responseContentType, options.features);
      }

      const body = await readResponseBodyForExtract(response, maxResponseBytes);
      fetchDoneAt = performance.now(); // body fully read: network leg ends here
      html = body.html;
      sizeMeta = body;
      finalUrl = response.url;

      // P0-1: Final URL validation after redirects (SSRF protection)
      const urlValidation = validateFinalUrl(finalUrl, started);
      if (urlValidation) {
        return urlValidation;
      }

      if (shouldPassThroughPlainText(responseContentType, html)) {
        const markdown = plainTextToMarkdown(html, finalUrl);
        if (markdown.length > 0) {
          if (sizeMeta.truncated) {
            return buildTruncatedResult(markdown, finalUrl, url, sizeMeta, started, responseContentType, html.length);
          }

          return await runPlugins({
            ok: true,
            backend: "plain_text",
            url: { requested: url, final: finalUrl },
            title: "",
            markdown,
            text_length: markdown.length,
            html_length: html.length,
            content_type: responseContentType,
            ...timing(),
          }, options.features);
        }
      }

      for (let hop = 0; hop < 3; hop += 1) {
        const refreshTarget = parseMetaRefreshTarget(html, finalUrl);
        if (!refreshTarget || refreshTarget === finalUrl) {
          break;
        }

        // SSRF: the meta-refresh target comes from the untrusted page HTML — an app-level
        // redirect that bypasses undici's (guarded) HTTP-3xx path. Resolve+validate and pin the
        // target host before fetching, exactly like the initial fetch, so a <meta refresh> to an
        // internal host / cloud-metadata endpoint can't slip past via a DNS-resolving name
        // (validateFinalUrl below is a literal-only check and won't catch a hostname).
        let refreshDispatcher;
        try {
          refreshDispatcher = await pinnedDispatcherForUrl(refreshTarget);
        } catch (error) {
          return {
            ok: false,
            backend: "node_readability_turndown",
            failure: error instanceof SsrfBlockedError ? error.failure : "dns_resolution_failed",
            latency_ms: Math.round(performance.now() - started),
          };
        }

        // Drop session Cookie/Authorization when the meta-refresh crosses to a different origin —
        // otherwise host A's credentials leak to a third-party redirect target (host B).
        const refreshHeaders = stripCrossOriginSensitiveHeaders(requestHeaders, finalUrl, refreshTarget);
        const refreshResponse = await egressFetch(refreshTarget, {
          headers: mergeFetchHeaders(refreshHeaders, defaultHeaders),
          signal: AbortSignal.timeout(30_000),
          ...(refreshDispatcher ? { dispatcher: refreshDispatcher } : {}),
        });
        if (!refreshResponse.ok) {
          break;
        }

        const refreshBody = await readResponseBodyForExtract(refreshResponse, maxResponseBytes);
        fetchDoneAt = performance.now(); // refresh hop adds to the network leg
        html = refreshBody.html;
        sizeMeta = refreshBody;
        finalUrl = refreshResponse.url;

        // P0-1: Final URL validation after meta refresh redirects
        const urlValidation = validateFinalUrl(finalUrl, started);
        if (urlValidation) {
          return urlValidation;
        }
      }
    }

    // Opt-in feed codec: feeds are XML or JSON Feed, not articles. When json_feed is requested and
    // the body looks like a feed we parse it directly and short-circuit (Readability would fail on
    // XML). Additive: without the flag, or for non-feed bodies, extraction proceeds as before.
    const wantFeed = featureActive(options.features, "json_feed");
    if (wantFeed && looksLikeFeed(responseContentType, html)) {
      const feed = parseFeedBody(html, finalUrl, responseContentType);
      if (feed) {
        const markdown = renderFeedMarkdown(feed);
        return await runPlugins({
          ok: true,
          backend: "node_readability_turndown",
          url: { requested: url, final: finalUrl },
          title: feed.title,
          markdown,
          media_refs: [],
          feed,
          text_length: markdown.length,
          html_length: html.length,
          ...timing(),
        }, options.features);
      }
    }

    const wantBlocks = featureActive(options.features, "json_blocks");
    const wantTables = featureActive(options.features, "json_tables");
    const extracted = extractHtmlToMarkdown(html, finalUrl, url, wantBlocks, wantTables);
    if (!extracted.ok) {
      if (sizeMeta.truncated) {
        return {
          ok: false,
          backend: "node_readability_turndown",
          failure: "response_too_large",
          bytes_read: sizeMeta.bytesRead,
          max_bytes: sizeMeta.maxBytes,
          latency_ms: Math.round(performance.now() - started),
        };
      }

      return {
        ...extracted.payload,
        ...timing(),
      };
    }

    if (sizeMeta.truncated) {
      return buildTruncatedResult(
        extracted.payload.markdown,
        finalUrl,
        url,
        sizeMeta,
        started,
        responseContentType,
        html.length,
        extracted.payload);
    }

    return await runPlugins({
      ok: true,
      backend: "node_readability_turndown",
      url: { requested: url, final: finalUrl },
      title: extracted.payload.title,
      markdown: extracted.payload.markdown,
      media_refs: extracted.payload.media_refs,
      blocks: extracted.payload.blocks,
      tables: extracted.payload.tables,
      meta: extracted.payload.meta,
      access: extracted.payload.access,
      text_length: extracted.payload.markdown.length,
      html_length: html.length,
      ...timing(),
    }, options.features);
  } catch (error) {
    if (error instanceof ResponseTooLargeError) {
      return {
        ok: false,
        backend: "node_readability_turndown",
        failure: "response_too_large",
        bytes_read: error.bytesRead,
        max_bytes: error.maxBytes,
        latency_ms: Math.round(performance.now() - started),
      };
    }

    const failure = error instanceof EgressProxyError
      ? error.code
      : (error?.cause?.code ?? error?.name ?? "error");
    return {
      ok: false,
      backend: "node_readability_turndown",
      failure,
      latency_ms: Math.round(performance.now() - started),
    };
  } finally {
    // Release the pinned connection pool (matters in the long-lived http daemon).
    //
    // destroy(), NOT close(): undici's close() waits for in-flight requests to COMPLETE, and the early
    // returns above (notably `!response.ok`) deliberately never read the response body. An unread body
    // means the request never completes, so close() never resolves — and because the only other thing on
    // the loop is AbortSignal.timeout's UNREF'd timer, node then exits 13 ("Unfinished Top-Level Await")
    // having printed no JSON at all: the host saw `workers_unavailable` + "run doctor" for a plain 404.
    // (Reproduced on Node 20 with MDN's 404: close() hung; body-cancelled or body-read → closed in ms.
    // A tiny body like nginx's 404 is already fully buffered, so it completed and masked this.)
    // Everything we return is materialized in memory by now, so aborting the pool is safe.
    if (pinnedDispatcher) {
      await pinnedDispatcher.destroy().catch(() => {});
    }
  }
}

/**
 * @param {string} markdown
 * @param {string} finalUrl
 * @param {string} requestedUrl
 * @param {{ truncated: boolean, bytesRead: number, maxBytes: number }} sizeMeta
 * @param {number} started
 * @param {string} responseContentType
 * @param {number} htmlLength
 * @param {{ title?: string, media_refs?: unknown[] }} [extra]
 */
function buildTruncatedResult(markdown, finalUrl, requestedUrl, sizeMeta, started, responseContentType, htmlLength, extra = {}) {
  return {
    ok: false,
    backend: "node_readability_turndown",
    failure: "response_truncated",
    truncated: true,
    url: { requested: requestedUrl, final: finalUrl },
    title: extra.title ?? "",
    markdown,
    media_refs: extra.media_refs,
    access: extra.access,
    text_length: markdown.length,
    html_length: htmlLength,
    content_type: responseContentType || undefined,
    bytes_read: sizeMeta.bytesRead,
    max_bytes: sizeMeta.maxBytes,
    latency_ms: Math.round(performance.now() - started),
  };
}

/**
 * @param {string} html
 * @param {string} finalUrl
 * @param {string} requestedUrl
 */
const DEFAULT_MAX_PDF_BYTES = 16 * 1024 * 1024; // 16 MiB — PDFs routinely exceed the 1 MiB HTML cap.

function resolveMaxPdfBytes() {
  const raw = Number(process.env.OCCAM_MAX_PDF_BYTES);
  if (Number.isFinite(raw) && raw > 0) {
    return Math.min(Math.max(raw, 64 * 1024), 128 * 1024 * 1024);
  }
  return DEFAULT_MAX_PDF_BYTES;
}

/** Reads a response body as bytes, bounded by maxBytes. @returns {{ bytes: Uint8Array, truncated: boolean, total: number }} */
async function readBinaryCapped(response, maxBytes) {
  const reader = response.body.getReader();
  const chunks = [];
  let total = 0;
  let truncated = false;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }
    total += value.byteLength;
    if (total > maxBytes) {
      truncated = true;
      try { await reader.cancel(); } catch { /* ignore */ }
      break;
    }
    chunks.push(value);
  }
  const bytes = new Uint8Array(chunks.reduce((n, c) => n + c.byteLength, 0));
  let offset = 0;
  for (const c of chunks) {
    bytes.set(c, offset);
    offset += c.byteLength;
  }
  return { bytes, truncated, total };
}

async function extractPdfResponse(response, requestedUrl, started, contentType, features) {
  const maxPdfBytes = resolveMaxPdfBytes();
  const elapsed = () => Math.round(performance.now() - started);

  const lenHeader = Number(response.headers.get("content-length") ?? "0");
  if (Number.isFinite(lenHeader) && lenHeader > maxPdfBytes) {
    return {
      ok: false,
      backend: "pdf",
      failure: "response_too_large",
      bytes_read: lenHeader,
      max_bytes: maxPdfBytes,
      latency_ms: elapsed(),
    };
  }

  const { bytes, truncated } = await readBinaryCapped(response, maxPdfBytes);
  if (truncated) {
    return {
      ok: false,
      backend: "pdf",
      failure: "response_too_large",
      bytes_read: bytes.length,
      max_bytes: maxPdfBytes,
      latency_ms: elapsed(),
    };
  }

  let parsed;
  try {
    parsed = await extractPdfMarkdown(bytes, response.url);
  } catch {
    return {
      ok: false,
      backend: "pdf",
      failure: "extraction_failed",
      note: "pdf_parse_error",
      content_type: contentType || undefined,
      latency_ms: elapsed(),
    };
  }

  if (parsed === null || parsed.markdown.length === 0) {
    // No %PDF magic, or a scanned/image-only PDF with no extractable text — honest failure
    // (the agent must not infer content; OCR is out of scope).
    return {
      ok: false,
      backend: "pdf",
      failure: "extraction_failed",
      note: parsed === null ? "not_a_pdf" : "pdf_no_text_layer",
      content_type: contentType || undefined,
      latency_ms: elapsed(),
    };
  }

  return await runPlugins({
    ok: true,
    backend: "pdf",
    url: { requested: requestedUrl, final: response.url },
    title: parsed.title,
    markdown: parsed.markdown,
    pdf_pages: parsed.pages,
    text_length: parsed.markdown.length,
    content_type: contentType || undefined,
    latency_ms: elapsed(),
  }, features);
}

function parseFeedBody(body, finalUrl, contentType) {
  if (looksLikeJsonFeed(contentType, body)) {
    try {
      const parsed = JSON.parse(body);
      return collectJsonFeed(parsed, { baseUrl: finalUrl });
    } catch {
      // Fall through to XML — some servers mislabel.
    }
  }
  return parseFeedFromHtml(body, finalUrl);
}

function parseFeedFromHtml(html, finalUrl) {
  try {
    const virtualConsole = new VirtualConsole();
    virtualConsole.on("jsdomError", () => {});
    const dom = new JSDOM(html, { url: finalUrl, contentType: "text/xml", virtualConsole });
    return collectFeedItems(dom.window.document, { baseUrl: finalUrl });
  } catch {
    return null;
  }
}

function renderFeedMarkdown(feed) {
  const lines = [];
  if (feed.title) {
    lines.push(`# ${feed.title}`, "");
  }
  for (const item of feed.items) {
    const heading = item.title || item.link || "(untitled)";
    lines.push(`## ${heading}`);
    if (item.link) {
      lines.push(item.link);
    }
    if (item.publishedAt) {
      lines.push(`_${item.publishedAt}_`);
    }
    const body = item.summaryMarkdown || item.summaryText || item.summary;
    if (body) {
      lines.push("", body);
    }
    lines.push("");
  }
  return lines.join("\n").trim();
}

function extractHtmlToMarkdown(html, finalUrl, requestedUrl, wantBlocks = false, wantTables = false) {
  const virtualConsole = new VirtualConsole();
  virtualConsole.on("jsdomError", () => {});
  const dom = new JSDOM(html, { url: finalUrl, virtualConsole });
  const doc = dom.window.document;
  const access = collectAccessEvidence(doc, { requestedUrl, finalUrl });
  const iframeBlocks = collectIframeMarkdownBlocks(doc, finalUrl);

  stripPromoBanners(doc);
  stripDomSelectors(doc, getDomStripSelectorsForUrl(finalUrl));

  // Collect structured blocks/tables from the pristine (post-strip) DOM BEFORE Readability runs.
  // Readability.parse() mutates the passed document destructively, so running collection after it
  // gutted the live content element for some page shapes (0 blocks on a real article) — Q-025.
  const blockRoot = wantBlocks
    ? (pickMainContent(doc, finalUrl)?.root
        ?? doc.querySelector("article, [role='main'], main")
        ?? doc.body)
    : null;
  const blocks = blockRoot ? collectBlocks(blockRoot, { doc, baseUrl: finalUrl }) : undefined;
  const tableRoot = wantTables
    ? (pickMainContent(doc, finalUrl)?.root
        ?? doc.querySelector("article, [role='main'], main")
        ?? doc.body)
    : null;
  const tables = tableRoot ? collectTables(tableRoot, { doc }) : undefined;

  const hasSeedSelectors = getContentSelectorsForUrl(finalUrl).length > 0;
  let article = hasSeedSelectors || isStrictPlaybookOverlay() ? pickMainContent(doc, finalUrl) : null;
  if (!article?.content && !isStrictPlaybookOverlay()) {
    article = new Readability(doc).parse();
  }
  if (!article?.content && !isStrictPlaybookOverlay()) {
    article = pickMainContent(doc, finalUrl);
  }

  if (!article?.content) {
    const bodyText = (doc.body?.textContent ?? "").replace(/\s+/g, " ").trim();
    const htmlLower = html.toLowerCase();
    const spaStub =
      bodyText.length < 500
      && (htmlLower.includes('id="app"')
        || htmlLower.includes("id='app'")
        || htmlLower.includes('id="root"')
        || htmlLower.includes('id="__next"')
        || htmlLower.includes("__next_data__")
        || htmlLower.includes("__nuxt__")
        || htmlLower.includes("data-reactroot"));
    return {
      ok: false,
      payload: {
        ok: false,
        backend: "node_readability_turndown",
        failure: "extraction_failed",
        spa_stub: spaStub,
      },
    };
  }

  const turndown = new TurndownService({ headingStyle: "atx", codeBlockStyle: "fenced" });
  addTableRule(turndown);
  const body = turndown.turndown(article.content);
  const title = (article.title ?? "").trim();
  let markdown = title.length > 0 ? `# ${title}\n\n${body}` : body;
  markdown = appendIframeMarkdownBlocks(markdown, iframeBlocks);
  markdown = genericMarkdownPrune(markdown);
  markdown = applySeedPostMarkdown(markdown, finalUrl);
  let mediaRoot = article.root ?? null;
  if (!mediaRoot && article.content) {
    const wrapper = doc.createElement("div");
    wrapper.innerHTML = article.content;
    mediaRoot = wrapper;
  }
  const media_refs = collectMediaRefs(doc, finalUrl, { root: mediaRoot });
  const meta = collectPageMetadata(doc, finalUrl);

  return {
    ok: true,
    payload: {
      title,
      markdown,
      media_refs,
      blocks,
      tables,
      meta,
      access,
    },
  };
}

function stripPromoBanners(document) {
  for (const selector of [".banner", ".banner-content", '[class*="promo-banner"]', '[class*="site-banner"]']) {
    document.querySelectorAll(selector).forEach((el) => el.remove());
  }
}

function pickMainContent(document, pageUrl) {
  const seedSelectors = getContentSelectorsForUrl(pageUrl);
  const selectors = isStrictPlaybookOverlay() && seedSelectors.length > 0
    ? seedSelectors
    : [
        ...seedSelectors,
        "article",
        '[role="main"]',
        "main",
        ".docs-content",
        ".docs-post-content",
        ".markdown-body",
        "#main-content",
        "#content",
        "div.section",
        "div.sect1",
        "#docs",
        ".article-body",
        ".post-content",
      ];
  for (const selector of selectors) {
    const el = document.querySelector(selector);
    const len = el?.textContent?.trim().length ?? 0;
    if (len > 200) {
      return { title: document.title, content: el.innerHTML, root: el };
    }
    if (len > 120 && el?.querySelectorAll("h2, h3, a").length >= 4) {
      return { title: document.title, content: el.innerHTML, root: el };
    }
  }
  return null;
}
