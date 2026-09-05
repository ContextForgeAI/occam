import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import { collectDomSkeleton } from "./dom-skeleton.mjs";

const SKIP = ["script", "style", "noscript", "svg", "path", "link", "meta"];
const INTERACTIVE = ["a", "button", "input", "select", "textarea", "summary"];

function collect(html, maxNodes) {
  const dom = new JSDOM(html);
  return collectDomSkeleton(dom.window.document, {
    maxNodes,
    maxDepth: 12,
    skipTags: SKIP,
    interactiveTags: INTERACTIVE,
  });
}

// Chrome-first DOM: a deep nav before main. Document-order DFS with a tiny budget
// historically exhausted the cap inside <nav> and never reached <main>.
function chromeFirstHtml(navDepth) {
  let nav = "";
  let open = "";
  let close = "";
  for (let i = 0; i < navDepth; i++) {
    open += `<div class="nav-level-${i}">`;
    close = `</div>${close}`;
    nav += `<a href="#${i}">Nav item ${i} with enough text to count</a>`;
  }
  return `<!doctype html><body>
  <header><div class="brand">Docs</div></header>
  <nav>${open}${nav}${close}</nav>
  <main id="content">
    <h1>Primary documentation title</h1>
    <p>${"Real article prose that should be selected as main content. ".repeat(8)}</p>
  </main>
  <footer>Copyright</footer>
</body>`;
}

const tight = collect(chromeFirstHtml(40), 80);
assert.ok(
  tight.anchors.mainCandidates.some((c) => /content|main/i.test(c.selector)),
  "prefer-content / landmark seed must surface #content or main under a tight node cap",
);
assert.ok(
  tight.anchors.landmarks.includes("main") || tight.root,
  "skeleton still returns a root under chrome-first DOM",
);

// Tree walk must prefer main over nav: with a modest budget the captured tree should
// include the content landmark, not only nav chrome.
const modest = collect(chromeFirstHtml(30), 120);
const json = JSON.stringify(modest.root);
assert.ok(
  json.includes('"tag":"main"') || json.includes('"id":"content"'),
  "prefer-content child order must reach main before exhausting a modest maxNodes budget",
);

// Explicit article landmark still scores as a main candidate.
const articleOnly = collect(
  `<!doctype html><body>
    <nav><a href="/">Home</a></nav>
    <article id="readme"><h1>Readme</h1><p>${"x".repeat(200)}</p></article>
  </body>`,
  50,
);
assert.ok(
  articleOnly.anchors.mainCandidates.some((c) => c.selector.includes("readme") || c.selector === "article"),
  "article landmarks must appear in mainCandidates",
);

console.log("dom-skeleton-prefer-content.selftest: OK");
