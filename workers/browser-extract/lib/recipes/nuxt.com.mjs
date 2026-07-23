/** Nuxt docs — SPA docs shell (P5-H3). */

export default {
  id: "nuxt.com",
  hosts: ["nuxt.com"],
  waitUntil: "domcontentloaded",
  postLoadWaitMs: 4000,
  selectorTimeoutMs: 12_000,
  gotoTimeoutMs: 45_000,
  extractVariant: "strip-chrome",
  waitSelectors: ["main article", '[data-content-id="docs-content"]', "#__nuxt main", "main h1"],
  articleSelectors: [
    "main article p",
    '[data-content-id="docs-content"] p',
    "main h1",
    "main p",
  ],
  contentSelectors: ["main article", '[data-content-id="docs-content"]', "main"],
  contentPrefix: "Nuxt documentation",
};
