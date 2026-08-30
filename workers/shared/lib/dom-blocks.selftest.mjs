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

// Provenance generation must stay linear in block count. In particular, it must not issue a
// full-document selector query for every block merely to re-verify a path that was derived from
// that same live DOM.
const manyHtml = `<!DOCTYPE html><html><body>
  <main id="content">${Array.from(
    { length: 400 },
    (_, index) => `<p>Block ${index} with enough content for provenance.</p>`,
  ).join("")}</main>
</body></html>`;
const manyDom = new JSDOM(manyHtml, { url: "https://example.com/large" });
const manyDoc = manyDom.window.document;
const manyRoot = manyDoc.getElementById("content");
const originalQuerySelector = manyDoc.querySelector.bind(manyDoc);
const originalQuerySelectorAll = manyDoc.querySelectorAll.bind(manyDoc);
let documentQueries = 0;
manyDoc.querySelector = (...args) => {
  documentQueries += 1;
  return originalQuerySelector(...args);
};
manyDoc.querySelectorAll = (...args) => {
  documentQueries += 1;
  return originalQuerySelectorAll(...args);
};
const manyBlocks = collectBlocks(manyRoot, { doc: manyDoc, baseUrl: manyDoc.URL });
assert.equal(manyBlocks.length, 400);
assert.equal(documentQueries, 0, "selector provenance performs no repeated document scans");
for (let index = 0; index < manyBlocks.length; index += 1) {
  assert.equal(
    originalQuerySelector(manyBlocks[index].source_selector),
    manyRoot.children[index],
    `block ${index} selector round-trips`,
  );
}

// A duplicate id may anchor the first matching element (querySelector does too), while later
// duplicates must fall back to a structural path so both selectors retain exact provenance.
const duplicateDom = new JSDOM(
  "<!doctype html><html><body><main><section id='dup'><p>First</p></section><section id='dup'><p>Second</p></section></main></body></html>",
);
const duplicateDoc = duplicateDom.window.document;
const duplicateRoot = duplicateDoc.querySelector("main");
const duplicateBlocks = collectBlocks(duplicateRoot, { doc: duplicateDoc });
assert.equal(duplicateBlocks.length, 2);
assert.equal(duplicateDoc.querySelector(duplicateBlocks[0].source_selector)?.textContent, "First");
assert.equal(duplicateDoc.querySelector(duplicateBlocks[1].source_selector)?.textContent, "Second");

console.log("dom-blocks heading-level: OK");
