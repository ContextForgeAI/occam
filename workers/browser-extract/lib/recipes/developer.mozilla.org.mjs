/** MDN Web Docs — hybrid shell; #content hydrates after domcontentloaded (Track 2). */

export default {
  id: "developer.mozilla.org",
  hosts: ["developer.mozilla.org"],
  waitUntil: "domcontentloaded",
  postLoadWaitMs: 2500,
  selectorTimeoutMs: 20_000,
  gotoTimeoutMs: 55_000,
  waitSelectors: [
    "#content h1",
    "main#content",
    "main.layout__content h1",
    ".content-section",
  ],
  articleSelectors: [
    "#content p",
    "main#content p",
    "main.layout__content p",
    ".content-section p",
    "#content h1",
  ],
  contentSelectors: ["#content", "main#content", "main.layout__content"],
};
