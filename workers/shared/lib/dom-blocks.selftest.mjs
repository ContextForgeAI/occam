import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import { collectBlocks } from "./dom-blocks.mjs";

// Heading-level enrichment (PR-3 part 2): h1..h6 emit `level`; other blocks do not.
const html = `<!DOCTYPE html><html><body><main>
  <h1>Title</h1>
  <p>Intro paragraph with enough words to survive the length filter.</p>
  <h2>Section</h2>
  <h3>Subsection</h3>
  <ul><li>A list item with enough words here.</li></ul>
</main></body></html>`;

const dom = new JSDOM(html, { url: "https://example.com/" });
const root = dom.window.document.querySelector("main");
const blocks = collectBlocks(root, { doc: dom.window.document, baseUrl: dom.window.document.URL });

const h1 = blocks.find((b) => b.type === "heading" && b.text === "Title");
assert.ok(h1, "h1 block present");
assert.equal(h1.level, 1);

const h2 = blocks.find((b) => b.text === "Section");
assert.ok(h2);
assert.equal(h2.level, 2);

const h3 = blocks.find((b) => b.text === "Subsection");
assert.ok(h3);
assert.equal(h3.level, 3);

const p = blocks.find((b) => b.type === "paragraph");
assert.ok(p, "paragraph present");
assert.equal(p.level, undefined, "paragraph carries no level");

const li = blocks.find((b) => b.type === "list_item");
assert.ok(li, "list item present");
assert.equal(li.level, undefined, "list item carries no level");

console.log("dom-blocks heading-level: OK");
