import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import { Readability } from "@mozilla/readability";
import TurndownService from "turndown";
import { preserveCodeWrappers } from "../../shared/lib/preserve-code-wrappers.mjs";

function parse(html) {
  return new JSDOM(html, { url: "https://abr.local/code" }).window.document;
}

function turndownHtml(html) {
  const td = new TurndownService({ headingStyle: "atx", codeBlockStyle: "fenced" });
  return td.turndown(html);
}

function readabilityThenTurndown(html, normalize) {
  const document = parse(html);
  if (normalize) {
    preserveCodeWrappers(document);
  }
  const article = new Readability(document).parse();
  const td = new TurndownService({ headingStyle: "atx", codeBlockStyle: "fenced" });
  return article?.content ? td.turndown(article.content) : "";
}

const b4 = `<!DOCTYPE html><html><body><article><h1>HTTP</h1>
<pre><code>import { <button>createServer</button> } from 'node:http';</code></pre>
</article></body></html>`;

assert.equal(b4.includes("createServer"), true);
const turndownOnly = turndownHtml(b4);
assert.equal(turndownOnly.includes("createServer"), true);
const lost = readabilityThenTurndown(b4, false);
assert.equal(lost.includes("createServer"), false, "loss stage must be Readability without normalize");
const preserved = readabilityThenTurndown(b4, true);
assert.equal(preserved.includes("createServer"), true);
console.log("LOSS_STAGE rawHTML=YES turndownDirect=YES readabilityThenTurndown=NO helperThenReadability=YES");

const b1 = parse(`<pre>import { <button class="twoslash-hover">createServer</button> } from 'node:http';
const server = <button class="twoslash-hover">createServer</button>();</pre>`);
preserveCodeWrappers(b1);
const b1Text = b1.querySelector("pre").textContent;
assert.match(b1Text, /import \{ createServer \} from 'node:http'/);
assert.equal([...b1Text.matchAll(/createServer/g)].length, 2);

const tooltip = parse(`<pre><button>createServer<span role="tooltip">More info about createServer</span></button></pre>`);
preserveCodeWrappers(tooltip);
const tooltipText = tooltip.querySelector("pre").textContent;
assert.equal(tooltipText.includes("createServer"), true);
assert.equal(tooltipText.includes("More info"), false);
assert.equal([...tooltipText.matchAll(/createServer/g)].length, 1);

const mixed = parse(`<pre>const n = <span class="twoslash-hover">widget_timeout<span class="twoslash-popup" role="tooltip">Max wait</span></span>;
const p = <a class="hover">parseConfig</a>();
const ac = <button>AbortController</button>;</pre>`);
preserveCodeWrappers(mixed);
const mixedText = mixed.querySelector("pre").textContent;
assert.equal(mixedText.includes("widget_timeout"), true);
assert.equal(mixedText.includes("parseConfig"), true);
assert.equal(mixedText.includes("AbortController"), true);
assert.equal(mixedText.includes("Max wait"), false);
assert.ok(mixedText.indexOf("widget_timeout") < mixedText.indexOf("parseConfig"));
assert.ok(mixedText.indexOf("parseConfig") < mixedText.indexOf("AbortController"));

const toolbar = parse(`<div class="toolbar"><button>Run</button></div><pre>const x = 1;</pre>`);
preserveCodeWrappers(toolbar);
assert.equal(toolbar.querySelector("div.toolbar button")?.textContent, "Run");
assert.equal(toolbar.querySelector("pre").textContent.includes("const x = 1"), true);
assert.equal(toolbar.querySelector("pre").textContent.includes("Run"), false);

const ordinary = parse(`<pre><code>const x = 1;
function add(a, b) { return a + b; }
</code></pre>`);
const before = ordinary.querySelector("pre").textContent;
preserveCodeWrappers(ordinary);
assert.equal(ordinary.querySelector("pre").textContent, before);

const generic = parse(`<pre>listen(<button>client_max_body_size</button>);</pre>`);
preserveCodeWrappers(generic);
assert.equal(generic.querySelector("pre").textContent.includes("client_max_body_size"), true);

const idemp = parse(`<pre>import { <button>createServer</button> } from 'node:http';</pre>`);
preserveCodeWrappers(idemp);
const htmlOnce = idemp.querySelector("pre").innerHTML;
preserveCodeWrappers(idemp);
assert.equal(idemp.querySelector("pre").innerHTML, htmlOnce);

console.log("preserve-code-wrappers: OK");
