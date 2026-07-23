import { chromium } from "playwright";
import { flattenOpenShadowRoots } from "./lib/shadow-dom-flatten.mjs";
import { buildDomSkeleton } from "./lib/dom-skeleton.mjs";
import { tryDismissConsent } from "./lib/consent.mjs";
import { resolveBrowserLaunchOptions, STEALTH_INIT_SCRIPT } from "./lib/browser-launch-options.mjs";
import { resolveAndValidateHost, shouldSkipPrivateIpCheck } from "../shared/lib/private-ip.mjs";
import { applySessionCookies, resolveBrowserContextOptions } from "./lib/session-headers.mjs";

const args = process.argv.slice(2);
const consentAggressive = args.includes("--consent-aggressive");
const modeArg = args.find((a) => a.startsWith("--mode="));
const mode = modeArg?.split("=")[1] ?? "skeleton";
const maxNodesArg = args.find((a) => a.startsWith("--max-nodes="));
const maxNodes = maxNodesArg ? Number.parseInt(maxNodesArg.split("=")[1], 10) : 600;
const headersFileArg = args.find((a) => a.startsWith("--headers-file="));
const url = args.find((a) => !a.startsWith("-"));

if (!url) {
  console.error("Usage: node dom-skeleton-capture.mjs <url> [--mode=skeleton] [--max-nodes=600]");
  process.exit(1);
}

if (mode !== "skeleton") {
  console.log(JSON.stringify({ Ok: false, FailureCode: "unsupported_mode", Message: `mode=${mode}` }));
  process.exit(2);
}

const started = performance.now();

let browser;
try {
  const headersFile = headersFileArg?.slice("--headers-file=".length).replace(/^"|"$/g, "");
  const { userAgent, extraHTTPHeaders, headers } = await resolveBrowserContextOptions(headersFile);
  browser = await chromium.launch(resolveBrowserLaunchOptions());
  const context = await browser.newContext({
    userAgent,
    extraHTTPHeaders,
    viewport: { width: 1280, height: 720 },
    bypassCSP: true,
  });

  // lean-A: mask navigator.webdriver.
  await context.addInitScript(STEALTH_INIT_SCRIPT);

  // SSRF parity with the browser-session guard (Q-031): Chromium resolves DNS and follows redirects
  // itself, so a pre-navigation host check can't stop a redirect / JS navigation to a private host
  // via a DNS-resolving name. Validate the host of every navigation request before it leaves.
  // Drop image/font/media like the browser-session guard: the DOM skeleton is structural, so these
  // don't affect capture, and routing every heavy subresource of a big page (e.g. MDN) through the
  // JS route handler otherwise slows the load enough to thin the captured skeleton.
  const SKELETON_BLOCKED_TYPES = new Set(["image", "font", "media"]);
  await context.route("**/*", async (route) => {
    const req = route.request();
    if (SKELETON_BLOCKED_TYPES.has(req.resourceType())) {
      return route.abort();
    }
    if (!shouldSkipPrivateIpCheck() && req.isNavigationRequest()) {
      try {
        await resolveAndValidateHost(new URL(req.url()).hostname);
      } catch {
        return route.abort("blockedbyclient");
      }
    }
    return route.continue();
  });

  // applySessionCookies signature is (context, url, headers) — passing the URL as the headers arg
  // silently read Cookie off the URL string (always undefined), so a cookie-walled heal target was
  // skeletoned anonymously. Reuse the headers already parsed above (no second file read).
  await applySessionCookies(context, url, headers);

  const page = await context.newPage();
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45_000 });
  if (consentAggressive) {
    await tryDismissConsent(page);
  }
  // Wait for the primary content landmark before capturing. The skeleton is node-capped, so on a
  // page that renders nav/sidebar before main content (e.g. MDN) a fixed delay races the render and
  // sometimes fills the cap with chrome, leaving mainCandidates=0 — the non-deterministic L3 K1
  // heal-capture flake. Best-effort: pages without an explicit main landmark fall through the catch.
  try {
    await page.waitForSelector("main, [role=main], article", { timeout: 8000, state: "attached" });
  } catch {
    // no explicit main landmark — proceed with the generic settle below
  }
  await page.waitForTimeout(800);

  await flattenOpenShadowRoots(page);
  const built = await buildDomSkeleton(page, { maxNodes });
  console.log(
    JSON.stringify({
      Ok: true,
      Backend: "browser_playwright",
      Skeleton: {
        root: built.root,
        stats: built.stats,
      },
      Anchors: built.anchors,
      latency_ms: Math.round(performance.now() - started),
    }),
  );
} catch (err) {
  const message = err instanceof Error ? err.message : String(err);
  const failureCode = /Executable doesn't exist|playwright install/i.test(message)
    ? "playwright_missing"
    : "skeleton_capture_failed";
  console.log(
    JSON.stringify({
      Ok: false,
      FailureCode: failureCode,
      Message: message,
      latency_ms: Math.round(performance.now() - started),
    }),
  );
  process.exit(2);
} finally {
  if (browser) {
    await browser.close();
  }
}
