/** PostgreSQL manual — static HTML docs (iter 016 phase 1). */

export default {
  id: "postgresql.org",
  hosts: ["postgresql.org"],
  waitUntil: "domcontentloaded",
  postLoadWaitMs: 2000,
  waitSelectors: ["div.sect1", "#docs", "table.doccontent", "div.chapter"],
  articleSelectors: ["div.sect1 p", "#docs p", "div.chapter p"],
  contentSelectors: ["div.sect1", "#docs", "table.doccontent", "div.chapter"],
};
