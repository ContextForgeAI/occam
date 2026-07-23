import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import { collectMediaRefs } from "./media-refs.mjs";

const html = `<!DOCTYPE html><html><body>
<main>
  <h2>Pod lifecycle</h2>
  <p>Overview diagram.</p>
  <figure>
    <img src="/assets/diagram.png" alt="Pod lifecycle diagram" data-src="/ignored.png" />
  </figure>
  <h3>Downloads</h3>
  <a href="/docs/guide.pdf">PDF guide</a>
  <a href="https://cdn.example.com/archive.zip" download>Bundle</a>
  <img src="data:image/png;base64,abc" alt="inline" />
  <img data-lazy-src="https://lazy.example/photo.jpg" alt="lazy photo" />
</main>
</body></html>`;

const dom = new JSDOM(html, { url: "https://kubernetes.io/docs/concepts/" });
const refs = collectMediaRefs(dom.window.document, dom.window.document.URL);

assert.equal(refs.length, 4);

const diagram = refs.find((r) => r.kind === "image" && r.url.endsWith("/assets/diagram.png"));
assert.ok(diagram);
assert.equal(diagram.alt, "Pod lifecycle diagram");
assert.equal(diagram.context_heading, "## Pod lifecycle");

const pdf = refs.find((r) => r.kind === "pdf");
assert.ok(pdf);
assert.equal(pdf.url, "https://kubernetes.io/docs/guide.pdf");

const zip = refs.find((r) => r.kind === "file");
assert.ok(zip);
assert.equal(zip.url, "https://cdn.example.com/archive.zip");

const lazy = refs.find((r) => r.url.includes("lazy.example"));
assert.ok(lazy);
assert.equal(lazy.kind, "image");

assert.equal(refs.some((r) => r.url.startsWith("data:")), false);

console.log("media-refs.selftest: OK");
