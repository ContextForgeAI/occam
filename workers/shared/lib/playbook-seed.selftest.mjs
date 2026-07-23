import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  applySeedPostMarkdown,
  getContentSelectorsForUrl,
  getDomStripSelectorsForUrl,
  loadSeedForUrl,
  resetSeedCache,
  runWithOverlay,
} from "./playbook-seed.mjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
process.env.OCCAM_HOME = root;
resetSeedCache();

const golden = JSON.parse(readFileSync(join(root, "corpora", "golden-hosts.json"), "utf8"));
for (const host of golden.hosts) {
  assert.ok(loadSeedForUrl(`https://${host}/`) || host === "platform.openai.com", `seed for ${host}`);
}

const nginxSelectors = getContentSelectorsForUrl("https://nginx.org/en/docs/");
assert.ok(nginxSelectors.includes("#content"));

const legacySelectors = runWithOverlay(
  { hosts: ["legacy.example"], extract: { content_selectors: ["#legacy-content"] } },
  false,
  () => getContentSelectorsForUrl("https://legacy.example/docs/"),
);
assert.deepEqual(legacySelectors, ["#legacy-content"]);

const nginxMd = `
## Modules reference

* [Alphabetical index of directives](syntax.html)
* [ngx_http_core_module](ngx_http_core_module.html)
`;
const nginxOut = applySeedPostMarkdown(nginxMd, "https://nginx.org/en/docs/");
assert.ok(!nginxOut.includes("ngx_http_core_module"));

const openaiMd = `
## Text generation models

OpenAI text generation models allow text outputs.

An embedding is a vector representation of a piece of data.

Text generation and embeddings models process text in chunks called tokens. Tokens represent commonly occurring sequences.
`;
const openaiOut = applySeedPostMarkdown(openaiMd, "https://platform.openai.com/docs/concepts");
assert.match(openaiOut, /## Embeddings/i);
assert.match(openaiOut, /## Tokens/i);

const nuxtUrl = "https://nuxt.com/docs/getting-started/introduction";
const nuxtFooterMd = `
# Nuxt

Core intro about the framework.

* [Master Nuxt](https://masteringnuxt.com/nuxt3)
* Explain with Agent
`;
const nuxtFooterOut = applySeedPostMarkdown(nuxtFooterMd, nuxtUrl);
assert.ok(!nuxtFooterOut.includes("Master Nuxt"));
assert.ok(!nuxtFooterOut.includes("Explain with Agent"));
assert.ok(nuxtFooterOut.includes("Core intro"));

const nuxtShikiMd = `
## Install

Run npx nuxi init.

html pre.shiki code .s52Pk{--shiki-light:#E2931D;--shiki-default:#E2931D}
`;
const nuxtShikiOut = applySeedPostMarkdown(nuxtShikiMd, "https://nuxt.com/docs/getting-started/installation");
assert.ok(!nuxtShikiOut.includes("--shiki-"));
assert.ok(nuxtShikiOut.includes("nuxi init"));

const nuxtTabMd = `
### Create a New Project

npmyarnpnpmbundeno

\`npm create nuxt@latest <project-name>\`

npmyarnpnpmbundeno

\`npm run dev -- -o\`
`;
const nuxtTabOut = applySeedPostMarkdown(nuxtTabMd, "https://nuxt.com/docs/getting-started/installation");
assert.ok(!nuxtTabOut.includes("npmyarnpnpmbundeno"));
assert.ok(nuxtTabOut.includes("npm create nuxt@latest"));

const dockerMd = `
# Get started | Docker Docs

Ask Gordon Copy Markdown View Markdown

function getCurrentPlaintextUrl(){const e=window.location.href}

If you're new to Docker, this section guides you through the essential resources to get started.

> **Embedded widget:** [_hjSafeContext](about:blank)
`;
const dockerOut = applySeedPostMarkdown(dockerMd, "https://docs.docker.com/get-started/");
assert.ok(!dockerOut.includes("getCurrentPlaintextUrl"));
assert.ok(!dockerOut.includes("Ask Gordon Copy Markdown"));
assert.ok(!dockerOut.includes("_hjSafeContext"));
assert.ok(dockerOut.includes("essential resources"));

const redditUrl =
  "https://www.reddit.com/r/programming/comments/1tlh5aj/announcement_weve_updated_the_rules_and_april_is/";
const redditSeed = loadSeedForUrl(redditUrl);
assert.equal(redditSeed?.id, "reddit.com");
assert.equal(redditSeed?.routing?.preferred_backend, "browser");
assert.ok(getDomStripSelectorsForUrl(redditUrl).some((s) => s.includes("recaptcha")));

const soUrl =
  "https://stackoverflow.com/questions/11227809/why-is-processing-a-sorted-array-faster-than-processing-an-unsorted-array";
const soSeed = loadSeedForUrl(soUrl);
assert.equal(soSeed?.id, "stackoverflow.com");
assert.equal(soSeed?.routing?.preferred_backend, "http_then_browser");
assert.ok(getContentSelectorsForUrl(soUrl).includes("#question"));

const xStatusUrl = "https://x.com/OpenAI/status/2062249312839434452";
const xSeed = loadSeedForUrl(xStatusUrl);
assert.equal(xSeed?.id, "x.com");
assert.equal(xSeed?.routing?.preferred_backend, "browser");
assert.ok(xSeed?.agent_notes?.includes("browser"));

console.log("playbook-seed.selftest: OK");
