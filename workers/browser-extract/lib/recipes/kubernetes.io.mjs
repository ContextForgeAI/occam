/** Kubernetes docs — static HTTP-first; browser recipe for deep task/reference pages. */

export default {
  id: "kubernetes.io",
  hosts: ["kubernetes.io"],
  waitUntil: "domcontentloaded",
  postLoadWaitMs: 1000,
  selectorTimeoutMs: 15_000,
  gotoTimeoutMs: 50_000,
  articleWaitBudgetMs: 8_000,
  waitSelectors: ["main", "#main", ".td-content", "article", "h1"],
  articleSelectors: ["main", "#main", ".td-content", "article", "main h1"],
  contentSelectors: ["main", "#main", ".td-content", "article"],
  virtualScroll: { mode: "disabled" },
};
