import { existsSync } from "node:fs";
import { chromium } from "playwright";
import { extractMarkdownFromHtml } from "./extract-html.mjs";
import { tryDismissConsent, hideConsentOverlays, waitForConsentOverlayHidden } from "./consent.mjs";
import { getRecipe } from "./recipes/registry.mjs";
import { injectRecipeCookies } from "./cookie-inject.mjs";
import { runVirtualScroll, normalizeVirtualScrollConfig } from "./virtual-scroll.mjs";
import { flattenOpenShadowRoots } from "./shadow-dom-flatten.mjs";
import { runBrowserPlan } from "./interaction-steps.mjs";
import { applySessionCookies, resolveBrowserContextOptions } from "./session-headers.mjs";
import { resolvePlaywrightProxy } from "../../shared/lib/egress-proxy.mjs";
import { genericMarkdownPrune } from "../../shared/lib/generic-markdown-prune.mjs";
import { applySeedPostMarkdown, getContentSelectorsForUrl, getDomStripSelectorsForUrl, isStrictPlaybookOverlay } from "../../shared/lib/playbook-seed.mjs";
import { resolveBrowserLaunchOptions, STEALTH_INIT_SCRIPT, classifyBrowserLaunchError, usesSystemBrowser } from "./browser-launch-options.mjs";
import { provisionChromium, autoInstallEnabled } from "./browser-provision.mjs";
import { isUrlAllowed, shouldSkipPrivateIpCheck, resolveAndValidateHost, SsrfBlockedError } from "../../shared/lib/private-ip.mjs";
import { isLoginRoute } from "../../shared/lib/access-evidence.mjs";

/**
 * Validates a URL after navigation in browser context
 * @param {string} url - URL to validate
 * @param {number} started - Performance start time
 * @returns {{ ok: false, backend: string, failure: string, latency_ms: number, url: { requested: string, final: string } } | null}
 */
function validateFinalUrlInBrowser(url, started) {
  if (!isUrlAllowed(url)) {
    return {
      ok: false,
      backend: "browser_playwright",
      failure: "private_url_blocked",
      latency_ms: Math.round(performance.now() - started),
      url: { requested: "", final: url },
    };
  }
  return null;
}

const BLOCKED_TYPES = new Set(["image", "font", "media"]);
const TRACKER_HOST_RE = /(google-analytics|googletagmanager|doubleclick|facebook\.net|segment\.io|hotjar)/i;
/** Avoid JSDOM OOM on very large static HTML (e.g. RFC 9110 ~1.2MB). HTTP path should succeed first. */
const BROWSER_HTML_MAX_CHARS = 900_000;

/** Pure cap check shared by the initial and post-settle SPA snapshots. */
export function isBrowserHtmlTooLarge(html) {
  return typeof html === "string" && html.length > BROWSER_HTML_MAX_CHARS;
}

function htmlTooLargeFailure(html, requestedUrl, finalUrl, variant, renderMs, started) {
  if (!isBrowserHtmlTooLarge(html)) return null;
  return {
    ok: false,
    backend: "browser_playwright",
    failure: "extraction_failed",
    message: `html_too_large:${html.length}`,
    extract_variant: variant,
    url: { requested: requestedUrl, final: finalUrl },
    html_length: html.length,
    render_ms: renderMs,
    latency_ms: Math.round(performance.now() - started),
  };
}

/** @typedef {"baseline" | "reextract" | "css-hide" | "strip-consent" | "strip-chrome"} ExtractVariant */

export const EXTRACT_VARIANTS = ["baseline", "reextract", "css-hide", "strip-consent", "strip-chrome"];

export function parseExtractVariant(value) {
  const v = (value ?? "css-hide").toLowerCase();
  return EXTRACT_VARIANTS.includes(v) ? v : "css-hide";
}

/**
 * Interstitial anti-bot / challenge phrases seen in the visible title or body of a wall page
 * (Cloudflare "Just a moment", generic captcha gates). Kept lowercase for case-insensitive match.
 */
const CHALLENGE_PHRASES = [
  "just a moment",
  "checking your browser",
  "verify you are human",
  "attention required",
  "enable javascript and cookies to continue",
  "please enable cookies",
  "please wait while we verify",
  "one more step",
  "ддос", // ru "ddos" gate wording
];

/**
 * Q-019 fail-fast decision — pure function over a lightweight DOM probe so it is unit-testable
 * without a browser. Returns true only when the page is an anti-bot *wall* (a challenge marker is
 * present AND there is essentially no readable content), so a real content page that merely embeds
 * a captcha widget in a form is NOT short-circuited.
 * @param {{ title?: string, textLen?: number, sampleLower?: string, hasChallengeNode?: boolean }} probe
 */
export function isChallengeWall(probe) {
  if (!probe) return false;
  const title = (probe.title ?? "").toLowerCase();
  const sample = (probe.sampleLower ?? "").toLowerCase();
  const textLen = probe.textLen ?? 0;
  const phraseHit = CHALLENGE_PHRASES.some((p) => title.includes(p) || sample.includes(p));
  const marker = Boolean(probe.hasChallengeNode) || phraseHit;
  // Wall = marker present with near-zero readable prose. A genuine page that got through lean-A has
  // real body text and never trips this even if it hosts a login/turnstile widget.
  return marker && textLen < 200;
}

/**
 * @param {{ blockStylesheets?: boolean }} [options]
 */
export async function createBrowserSession(options = {}) {
  const blockStylesheets = options.blockStylesheets === true;
  const { userAgent, extraHTTPHeaders, headers } = await resolveBrowserContextOptions(options.headersFile);
  const proxy = resolvePlaywrightProxy();
  const launchOptions = resolveBrowserLaunchOptions();
  if (proxy) {
    launchOptions.proxy = proxy;
  }

  // Branch 2: if no browser binary is installed and the page needs one, provision it (user-level) and
  // retry — reporting it — instead of failing. System-lib (root) / autoinstall-off cases propagate to
  // the caller's typed browser_required (branch 3).
  let browserProvisioned = null;
  let browser;
  try {
    browser = await chromium.launch(launchOptions);
  } catch (err) {
    const classified = classifyBrowserLaunchError(err);
    // This condition IS the auto-provision gate. The C# host predicts it (to decide whether to downgrade a
    // browser request to HTTP) by spawning lib/provision-gate.mjs, which re-uses these same two predicates —
    // so keep the decision expressed through them rather than inlining new env checks here (B6).
    if (classified?.fix?.kind === "manual_install" && autoInstallEnabled() && !usesSystemBrowser()) {
      browserProvisioned = await provisionChromium();
      browser = await chromium.launch(launchOptions); // retry with the just-provisioned browser
    } else {
      throw err;
    }
  }
  const storageStatePath = options.storageStateFile?.replace(/^"|"$/g, "");
  const contextOptions = {
    userAgent,
    extraHTTPHeaders,
    viewport: { width: 1280, height: 720 },
    bypassCSP: true,
  };
  if (storageStatePath && existsSync(storageStatePath)) {
    contextOptions.storageState = storageStatePath;
  }

  const context = await browser.newContext(contextOptions);

  // lean-A: mask navigator.webdriver before any page script executes.
  await context.addInitScript(STEALTH_INIT_SCRIPT);

  await context.route("**/*", async (route) => {
    const req = route.request();
    const type = req.resourceType();
    if (BLOCKED_TYPES.has(type)) {
      return route.abort();
    }
    if (blockStylesheets && type === "stylesheet") {
      return route.abort();
    }
    if (blockStylesheets && type === "script") {
      try {
        const host = new URL(req.url()).hostname;
        if (TRACKER_HOST_RE.test(host)) {
          return route.abort();
        }
      } catch {
        // ignore malformed tracker URL
      }
    }
    // SSRF: Chromium resolves and follows redirects itself, so the pre-navigation host check and
    // the literal framenavigated check can't stop a redirect/JS navigation to an internal host via
    // a DNS-resolving name. Validate the host of EVERY navigation request (initial, 3xx, meta
    // refresh, JS location, iframe) with a real DNS resolve here and abort private targets before
    // the request leaves — the network-layer guard the http worker gets from resolveAndValidateHost.
    if (!shouldSkipPrivateIpCheck() && req.isNavigationRequest()) {
      try {
        await resolveAndValidateHost(new URL(req.url()).hostname);
      } catch {
        return route.abort("blockedbyclient");
      }
    }
    return route.continue();
  });

  return {
    browser,
    context,
    sessionHeaders: headers,
    browserProvisioned, // branch-2 telemetry (null unless we auto-installed on this launch)
    async close() {
      await context.close();
      await browser.close();
    },
  };
}

async function stripRecipeDom(page, recipe, url) {
  const selectors = [
    ...(recipe?.domStripSelectors ?? []),
    ...getDomStripSelectorsForUrl(url),
  ];
  const unique = [...new Set(selectors)];
  if (unique.length === 0) {
    return;
  }

  await page.evaluate((sels) => {
    for (const selector of sels) {
      document.querySelectorAll(selector).forEach((el) => el.remove());
    }
  }, unique);
}

const MIN_ARTICLE_TEXT_CHARS = 80;
const MIN_CONTAINER_TEXT_CHARS = 200;
const DEFAULT_ARTICLE_SELECTOR_TIMEOUT_MS = 4_000;
const DEFAULT_ARTICLE_WAIT_BUDGET_MS = 8_000;

/** @param {string} selector */
function parentSelectorFromArticleSelector(selector) {
  const trimmed = selector.trim();
  const space = trimmed.lastIndexOf(" ");
  return space > 0 ? trimmed.slice(0, space) : "";
}

/** @param {string} selector @param {ReturnType<typeof getRecipe>} recipe */
function containerSelectorsForArticleProbe(selector, recipe) {
  const fromRecipe = recipe?.contentSelectors ?? [];
  const parent = parentSelectorFromArticleSelector(selector);
  const derived = parent ? [parent] : [];
  return [...new Set([...fromRecipe, ...derived].filter(Boolean))];
}

/**
 * Visible short leaf node — check recipe/parent containers before trying more selectors.
 * @param {import("playwright").Page} page
 * @param {ReturnType<typeof getRecipe>} recipe
 * @param {string} articleSelector
 */
async function tryContainerArticleFallback(page, recipe, articleSelector) {
  for (const containerSelector of containerSelectorsForArticleProbe(articleSelector, recipe)) {
    try {
      const locator = page.locator(containerSelector).first();
      if (!(await locator.isVisible())) {
        continue;
      }
      const text = (await locator.innerText())?.trim() ?? "";
      if (text.length >= MIN_CONTAINER_TEXT_CHARS) {
        return true;
      }
    } catch {
      // try next container
    }
  }
  return false;
}

const DEFAULT_ARTICLE_SELECTORS = [
  "article p",
  "main p",
  '[role="main"] p',
  ".article-body p",
  ".post-content p",
  ".docs-content p",
  "#main-content p",
  "[data-main-column] p",
  ".markdown p",
  ".docs-article p",
  "body p",
  "p",
];

/**
 * Bare HTML (no article/main shell): skip the 4s×N selector loop when content is already visible.
 * @param {import("playwright").Page} page
 */
async function tryBareHtmlArticleFastPath(page) {
  const hasSemanticShell = await page
    .locator("article, main, [role='main']")
    .first()
    .isVisible()
    .catch(() => false);
  if (hasSemanticShell) {
    return false;
  }

  for (const selector of ["body p", "p"]) {
    try {
      const locator = page.locator(selector).first();
      if (!(await locator.isVisible())) {
        continue;
      }
      const text = (await locator.textContent())?.trim() ?? "";
      if (text.length >= MIN_ARTICLE_TEXT_CHARS) {
        return true;
      }
      if (await tryContainerArticleFallback(page, null, selector)) {
        return true;
      }
    } catch {
      // try next
    }
  }
  return false;
}

async function waitForRecipeSelectors(page, recipe = null) {
  const selectors = recipe?.waitSelectors ?? [];
  const timeout = recipe?.selectorTimeoutMs ?? 12_000;
  for (const selector of selectors) {
    try {
      const locator = page.locator(selector).first();
      await locator.waitFor({ state: "visible", timeout });
      const text = await locator.textContent();
      if (text && text.trim().length >= 40) {
        return true;
      }
    } catch {
      // try next
    }
  }
  return false;
}

async function waitForArticleContent(page, recipe = null) {
  const usingDefaultSelectors = !recipe?.articleSelectors?.length;
  const selectors = usingDefaultSelectors ? DEFAULT_ARTICLE_SELECTORS : recipe.articleSelectors;
  const selectorTimeoutMs = recipe?.articleSelectorTimeoutMs ?? DEFAULT_ARTICLE_SELECTOR_TIMEOUT_MS;
  const maxBudgetMs = recipe?.articleWaitBudgetMs ?? DEFAULT_ARTICLE_WAIT_BUDGET_MS;

  if (usingDefaultSelectors && (await tryBareHtmlArticleFastPath(page))) {
    return;
  }

  const budgetStart = performance.now();

  for (const selector of selectors) {
    const elapsed = performance.now() - budgetStart;
    if (elapsed >= maxBudgetMs) {
      break;
    }
    const remainingMs = Math.max(0, Math.min(selectorTimeoutMs, maxBudgetMs - elapsed));
    if (remainingMs <= 0) {
      break;
    }

    try {
      const locator = page.locator(selector).first();
      if (!(await locator.isVisible())) {
        await locator.waitFor({ state: "visible", timeout: remainingMs });
      }
      const text = (await locator.textContent())?.trim() ?? "";
      if (text.length >= MIN_ARTICLE_TEXT_CHARS) {
        return;
      }
      if (await tryContainerArticleFallback(page, recipe, selector)) {
        return;
      }
      // Visible but short — continue immediately (no extra wait).
    } catch {
      // Not visible within timeout — try next selector.
    }
  }
}

function extractOptionsForVariant(variant, recipe = null, url = "", wantBlocks = false, wantTables = false) {
  const base = (() => {
    switch (variant) {
      case "strip-chrome":
        return { stripChrome: true, useClone: true };
      case "strip-consent":
        return { stripConsentOnly: true, useClone: true };
      case "baseline":
        return { stripChrome: false, useClone: false };
      default:
        return { stripChrome: false, useClone: true };
    }
  })();
  const seedSelectors = getContentSelectorsForUrl(url);
  const contentSelectors = isStrictPlaybookOverlay() && seedSelectors.length > 0
    ? seedSelectors
    : recipe?.contentSelectors?.length
      ? recipe.contentSelectors
      : seedSelectors;
  return {
    ...base,
    contentSelectors,
    strictSelectors: isStrictPlaybookOverlay() && seedSelectors.length > 0,
    wantBlocks,
    wantTables,
  };
}

/** @param {ReturnType<typeof getRecipe>} recipe @param {ReturnType<typeof import("./interaction-steps.mjs").readBrowserPlanFile>} browserPlan */
function resolveVirtualScrollOptions(recipe, browserPlan) {
  const fromRecipe = recipe?.virtualScroll ? normalizeVirtualScrollConfig(recipe.virtualScroll) : {};
  const fromPlan = browserPlan?.virtual_scroll
    ? normalizeVirtualScrollConfig(browserPlan.virtual_scroll)
    : {};
  return {
    ...fromRecipe,
    ...fromPlan,
    mode: fromPlan.mode ?? fromRecipe.mode ?? "auto",
  };
}

function resolveGotoTimeoutMs(recipe, consentAggressive) {
  const base = recipe?.gotoTimeoutMs ?? (consentAggressive ? 60_000 : 45_000);
  if (process.env.OCCAM_TIER_B !== "1") {
    return base;
  }

  const envCap = process.env.OCCAM_BROWSER_GOTO_TIMEOUT_MS;
  const cap = envCap ? Number(envCap) : 20_000;
  return Math.min(base, Number.isFinite(cap) ? cap : 20_000);
}

export async function renderAndExtract(context, url, options = {}) {
  const {
    consentAggressive: optionConsentAggressive = false,
    extractVariant = "css-hide",
    browserPlan = null,
  } = options;
  const variant = parseExtractVariant(extractVariant);
  const activeFeatureList = (options.features ?? process.env.OCCAM_FEATURES ?? "")
    .split(",")
    .map((f) => f.trim().toLowerCase());
  const wantBlocks = activeFeatureList.includes("json_blocks");
  const wantTables = activeFeatureList.includes("json_tables");
  const useReextract = variant !== "baseline";
  const useCssHide = variant === "css-hide" || variant === "strip-chrome" || variant === "strip-consent";

  const started = performance.now();
  const page = await context.newPage();
  let scrollInfo = {
    mode: "disabled",
    rounds: 0,
    finalHeight: 0,
    chunks_merged: 0,
    unique_items: 0,
    disabled: true,
  };
  let renderMs = 0;
  let consentClicked = null;
  let consentReextract = false;
  let interactionInfo = { steps_run: 0 };
  const recipe = await getRecipe(url);
  const consentAggressive = recipe?.consentAggressive === true || optionConsentAggressive;
  let cookiesInjected = false;
  let sessionCookiesAdded = 0;

  try {
    const renderStart = performance.now();
    const injectResult = await injectRecipeCookies(context, recipe);
    cookiesInjected = injectResult.injected;
    const sessionCookieResult = await applySessionCookies(context, url, options.sessionHeaders ?? {});
    sessionCookiesAdded = sessionCookieResult.cookiesAdded;
    if (sessionCookiesAdded > 0) {
      cookiesInjected = true;
    }

    const waitUntil = recipe?.waitUntil ?? (consentAggressive ? "networkidle" : "domcontentloaded");
    const gotoTimeout = resolveGotoTimeoutMs(recipe, consentAggressive);
    page.setDefaultTimeout(gotoTimeout);

    // SSRF / DNS-rebinding protection: resolve the host across BOTH families and reject any
    // private address before navigation. (Chromium does its own resolution, so we can't pin the
    // socket here as on the http path — the both-families check closes the prior IPv4-only gap.)
    // Skipped when private URLs are explicitly allowed (e.g. local testing).
    if (!shouldSkipPrivateIpCheck()) {
      try {
        await resolveAndValidateHost(new URL(url).hostname);
      } catch (error) {
        return {
          ok: false,
          backend: "browser_playwright",
          failure: error instanceof SsrfBlockedError ? error.failure : "dns_resolution_failed",
          extract_variant: variant,
          url: { requested: url, final: url },
          render_ms: 0,
          extract_ms: 0,
          latency_ms: Math.round(performance.now() - started),
        };
      }
    }

    // P0-1: Intercept all frame navigations for final URL validation
    let navigationBlocked = false;
    const navigationHandler = (frame) => {
      if (frame === page.mainFrame()) {
        const validation = validateFinalUrlInBrowser(frame.url(), started);
        if (validation) {
          navigationBlocked = true;
        }
      }
    };
    page.on('framenavigated', navigationHandler);

    const mainResponse = await page.goto(url, { waitUntil, timeout: gotoTimeout });
    
    // Remove the listener after initial navigation
    page.off('framenavigated', navigationHandler);
    
    // Check if initial navigation was blocked
    if (navigationBlocked) {
      return {
        ok: false,
        backend: "browser_playwright",
        failure: "private_url_blocked",
        extract_variant: variant,
        url: { requested: url, final: page.url() },
        render_ms: 0,
        extract_ms: 0,
        latency_ms: Math.round(performance.now() - started),
      };
    }

    const navStatus = mainResponse?.status?.() ?? 0;
    if (navStatus >= 400) {
      return {
        ok: false,
        backend: "browser_playwright",
        failure: `http_${navStatus}`,
        status_code: navStatus,
        extract_variant: variant,
        url: { requested: url, final: page.url() },
        render_ms: Math.round(performance.now() - renderStart),
        extract_ms: 0,
        consent_recipe: recipe?.id ?? null,
        cookies_injected: cookiesInjected,
        latency_ms: Math.round(performance.now() - started),
      };
    }
    await page.waitForSelector("article, main, [role='main'], body", { timeout: 12_000 }).catch(() => {});
    await page.waitForTimeout(recipe?.postLoadWaitMs ?? (consentAggressive ? 2000 : 1000));

    // Q-019 fail-fast: if lean-A did NOT get us past an anti-bot wall, bail now with a typed
    // failure instead of burning the whole per-extract budget (consent → interaction → virtual
    // scroll → networkidle re-extract) on an interstitial that will never yield content. A raw
    // 120s mcp_timeout is both slower and less honest than a fast captcha_or_challenge.
    const challengeProbe = await page.evaluate(() => {
      const bodyText = (document.body?.innerText || "").trim();
      const hasChallengeNode = Boolean(document.querySelector(
        'script[src*="challenges.cloudflare.com"], .cf-turnstile, #cf-turnstile, ' +
        '#challenge-form, #cf-challenge-running, iframe[src*="turnstile"], ' +
        'iframe[src*="hcaptcha"], iframe[title*="hCaptcha"], iframe[src*="recaptcha/api2"]'));
      return {
        title: document.title || "",
        textLen: bodyText.length,
        sampleLower: bodyText.slice(0, 400).toLowerCase(),
        hasChallengeNode,
      };
    }).catch(() => null);
    if (isChallengeWall(challengeProbe)) {
      return {
        ok: false,
        backend: "browser_playwright",
        failure: "captcha_or_challenge",
        extract_variant: variant,
        url: { requested: url, final: page.url() },
        consent_recipe: recipe?.id ?? null,
        cookies_injected: cookiesInjected,
        render_ms: Math.round(performance.now() - renderStart),
        extract_ms: 0,
        latency_ms: Math.round(performance.now() - started),
      };
    }

    consentClicked = await tryDismissConsent(page, { aggressive: consentAggressive, recipe });
    if (!consentClicked) {
      consentClicked = await tryDismissConsent(page, { aggressive: true, recipe });
    }

    if (useReextract && consentClicked) {
      await waitForConsentOverlayHidden(page, { timeoutMs: consentAggressive ? 5000 : 4000 });
      consentReextract = true;
      await page.waitForTimeout(consentAggressive ? 1000 : 600);
    } else if (useCssHide && !consentClicked) {
      await hideConsentOverlays(page);
      await page.waitForTimeout(consentAggressive ? 1000 : 800);
    } else {
      await page.waitForTimeout(consentAggressive ? 1000 : 800);
    }

    interactionInfo = await runBrowserPlan(page, browserPlan);

    await waitForRecipeSelectors(page, recipe);
    await waitForArticleContent(page, recipe);
    scrollInfo = await runVirtualScroll(page, resolveVirtualScrollOptions(recipe, browserPlan));

    if (useCssHide) {
      await hideConsentOverlays(page);
    }

    await stripRecipeDom(page, recipe, url);

    renderMs = Math.round(performance.now() - renderStart);

    const shadowFlat = await flattenOpenShadowRoots(page);
    let html = await page.content();
    let finalUrl = page.url();

    const initialSizeFailure = htmlTooLargeFailure(html, url, finalUrl, variant, renderMs, started);
    if (initialSizeFailure) return initialSizeFailure;

    let extractStart = performance.now();
    let extracted = extractMarkdownFromHtml(html, finalUrl, extractOptionsForVariant(variant, recipe, finalUrl, wantBlocks, wantTables));
    let extractMs = Math.round(performance.now() - extractStart);

    const shortResponse = !extracted || (extracted.text_length ?? 0) < 800;
    if (shortResponse) {
      await page.waitForLoadState("networkidle", { timeout: 8_000 }).catch(() => {});
      await waitForArticleContent(page, recipe);
      await page.waitForTimeout(1200);
      await flattenOpenShadowRoots(page);
      html = await page.content();
      finalUrl = page.url();
      // The extra settle above can let a dynamic SPA grow the DOM past the cap the first
      // snapshot passed — re-apply the same guard so re-extract never feeds an oversized
      // document into the markdown extractor.
      const settledSizeFailure = htmlTooLargeFailure(html, url, finalUrl, variant, renderMs, started);
      if (settledSizeFailure) return settledSizeFailure;
      extractStart = performance.now();
      extracted = extractMarkdownFromHtml(html, finalUrl, extractOptionsForVariant(variant, recipe, finalUrl, wantBlocks, wantTables));
      extractMs += Math.round(performance.now() - extractStart);
    }

    if (extracted?.access) {
      extracted.access.redirected_to_login = url.toLowerCase() !== finalUrl.toLowerCase()
        && isLoginRoute(finalUrl);
    }

    if (extracted && recipe?.contentPrefix) {
      const prefix = String(recipe.contentPrefix).trim();
      if (prefix.length > 0 && !extracted.markdown.toLowerCase().includes(prefix.toLowerCase())) {
        extracted.markdown = `${prefix}\n\n${extracted.markdown}`;
        extracted.text_length = extracted.markdown.length;
      }
    }

    // P0-1: Final URL validation after all redirects/navigations
    const finalValidation = validateFinalUrlInBrowser(finalUrl, started);
    if (finalValidation) {
      return {
        ...finalValidation,
        extract_variant: variant,
      };
    }

    if (extracted?.markdown) {
      extracted.markdown = genericMarkdownPrune(extracted.markdown);
      extracted.markdown = applySeedPostMarkdown(extracted.markdown, finalUrl);
      extracted.text_length = extracted.markdown.length;
    }

    let screenshotBase64 = null;
    const featuresStr = options.features ?? process.env.OCCAM_FEATURES ?? "";
    const activeFeatures = featuresStr
      .split(",")
      .map((f) => f.trim().toLowerCase())
      .filter(Boolean);
    if (activeFeatures.includes("screenshot")) {
      try {
        const screenshotBuffer = await page.screenshot({ type: "jpeg", quality: 80 });
        screenshotBase64 = screenshotBuffer.toString("base64");
      } catch (err) {
        console.error("Screenshot capture failed:", err);
      }
    }

    return {
      ok: true,
      backend: "browser_playwright",
      extract_variant: variant,
      consent_recipe: recipe?.id ?? null,
      cookies_injected: cookiesInjected,
      url: { requested: url, final: finalUrl },
      title: extracted.title,
      markdown: extracted.markdown,
      media_refs: extracted.media_refs ?? [],
      blocks: extracted.blocks,
      tables: extracted.tables,
      meta: extracted.meta,
      access: extracted.access,
      text_length: extracted.text_length,
      html_length: html.length,
      shadow_dom_flatten: shadowFlat,
      render_ms: renderMs,
      extract_ms: extractMs,
      consent_clicked: consentClicked,
      consent_reextract: consentReextract,
      interaction_steps_run: interactionInfo?.steps_run ?? 0,
      virtual_scroll_rounds: scrollInfo.rounds,
      virtual_scroll_mode: scrollInfo.mode,
      virtual_scroll_chunks_merged: scrollInfo.chunks_merged ?? 0,
      virtual_scroll_unique_items: scrollInfo.unique_items ?? 0,
      latency_ms: Math.round(performance.now() - started),
      screenshot: screenshotBase64 || undefined,
    };
  } catch (error) {
    // Map unexpected exceptions to a clean taxonomy code — never leak the raw JS error name as a
    // failure code (this used to emit e.g. "typeerror" for a Playwright/page TypeError). The raw
    // name+message is preserved in `message` for diagnostics. Q-005.
    const errName = error?.name ?? "Error";
    // A navigation aborted by the SSRF route guard (a redirect to a private host) surfaces here as
    // net::ERR_BLOCKED_BY_CLIENT — report it as the honest private_url_blocked, not extraction_failed.
    const blockedBySsrf = (error?.message ?? "").includes("ERR_BLOCKED_BY_CLIENT");
    return {
      ok: false,
      backend: "browser_playwright",
      failure: blockedBySsrf ? "private_url_blocked" : (errName === "TimeoutError" ? "timeout" : "extraction_failed"),
      message: `${errName}: ${error?.message ?? ""}`.trim(),
      extract_variant: variant,
      url: { requested: url, final: page.url?.() ?? url },
      render_ms: renderMs,
      extract_ms: 0,
      consent_clicked: consentClicked,
      consent_reextract: consentReextract,
      consent_recipe: recipe?.id ?? null,
      cookies_injected: cookiesInjected,
      latency_ms: Math.round(performance.now() - started),
    };
  } finally {
    await page.close();
  }
}
