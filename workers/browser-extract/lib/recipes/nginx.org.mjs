/** nginx.org — static docs index (iter 016 phase 1). */

export default {
  id: "nginx.org",
  hosts: ["nginx.org"],
  waitUntil: "domcontentloaded",
  postLoadWaitMs: 1000,
  waitSelectors: ["#content"],
  articleSelectors: ["#content"],
  contentSelectors: ["#content"],
  virtualScroll: { mode: "disabled" },
};
