/** Docker docs — consent + SPA (iter 016 phase 1b). */

export default {
  id: "docs.docker.com",
  hosts: ["docs.docker.com"],
  consentAggressive: true,
  waitUntil: "domcontentloaded",
  postLoadWaitMs: 4000,
  selectorTimeoutMs: 12_000,
  gotoTimeoutMs: 45_000,
  extractVariant: "strip-chrome",
  consentSelectors: [
    "#onetrust-accept-btn-handler",
    'button:has-text("Accept All Cookies")',
    'button:has-text("Accept all cookies")',
    'button:has-text("Accept")',
    '[data-testid="accept-all"]',
  ],
  waitSelectors: [
    "main article.prose h1",
    "main article.prose p",
    "[data-pagefind-body] h1",
    "main article h1",
  ],
  articleSelectors: [
    "main article.prose p",
    "main article p",
    "[data-pagefind-body] p",
    "main article h2",
  ],
  contentSelectors: ["main article.prose", "[data-pagefind-body]", "main article"],
  domStripSelectors: [
    "#breadcrumbs",
    "article script",
    "article > div:has([marlin-action])",
  ],
  contentPrefix: "Docker documentation",
};
