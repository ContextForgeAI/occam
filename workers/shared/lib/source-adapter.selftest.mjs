import assert from "node:assert/strict";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  buildSourceAdapterFetchHeaders,
  CRATES_README_SELECTOR,
  createSerialRateLimiter,
  renderCratesReadmeHtml,
  renderSourceAdapterPayload,
  resolveCratesReadmeRequest,
  resolveSourceAdapter,
  selectLatestNonYankedVersion,
  sparseIndexUrlForCrate,
  staticCratesReadmeUrl,
} from "./source-adapter.mjs";
import { resetSeedCache } from "./playbook-seed.mjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
process.env.OCCAM_HOME = root;
resetSeedCache();

const unscoped = resolveSourceAdapter("https://www.npmjs.com/package/react");
assert.equal(unscoped?.adapterName, "npm_registry_package");
assert.equal(unscoped?.sourceUrl, "https://registry.npmjs.org/react/latest");

const scoped = resolveSourceAdapter("https://npmjs.com/package/@scope/example");
assert.equal(scoped?.sourceUrl, "https://registry.npmjs.org/%40scope%2Fexample/latest");

assert.equal(resolveSourceAdapter("https://www.npmjs.com/search?q=react"), null);
assert.equal(resolveSourceAdapter("https://example.com/package/react"), null);
assert.equal(resolveSourceAdapter("https://www.npmjs.com/package/%E0%A4%A"), null);

const crates = resolveSourceAdapter("https://crates.io/crates/tokio");
assert.equal(crates?.adapterName, "crates_io_readme");
assert.equal(crates?.eager, true);
assert.equal(crates?.sourceUrl, "https://index.crates.io/to/ki/tokio");
assert.deepEqual(resolveCratesReadmeRequest("https://crates.io/crates/Serde/"), {
  crateName: "serde",
  indexUrl: "https://index.crates.io/se/rd/serde",
});
assert.equal(resolveCratesReadmeRequest("https://crates.io/crates/serde/1.0.0"), null);
assert.equal(resolveCratesReadmeRequest("https://crates.io/crates/serde?tab=versions"), null);
assert.equal(resolveCratesReadmeRequest("https://crates.io/crates/%2Fetc"), null);
assert.equal(resolveCratesReadmeRequest("https://crates.io/crates/%E0%A4%A"), null);
assert.equal(resolveSourceAdapter("https://example.com/crates/tokio"), null);
assert.equal(sparseIndexUrlForCrate("a"), "https://index.crates.io/1/a");
assert.equal(sparseIndexUrlForCrate("ab"), "https://index.crates.io/2/ab");
assert.equal(sparseIndexUrlForCrate("abc"), "https://index.crates.io/3/a/abc");
assert.equal(
  staticCratesReadmeUrl("tokio", "1.53.1"),
  "https://static.crates.io/readmes/tokio/tokio-1.53.1.html",
);

const latestVersion = selectLatestNonYankedVersion([
  JSON.stringify({ vers: "1.9.0", yanked: false }),
  JSON.stringify({ vers: "2.0.0-beta.2", yanked: false }),
  JSON.stringify({ vers: "2.0.0-beta.10", yanked: false }),
  JSON.stringify({ vers: "2.0.0", yanked: true }),
].join("\n"));
assert.equal(latestVersion, "2.0.0-beta.10");
assert.equal(selectLatestNonYankedVersion('{"vers":"1.0.0","yanked":false}\nnot-json'), null);
assert.equal(selectLatestNonYankedVersion('{"vers":"1.0.0","yanked":true}'), null);

const adapterHeaders = buildSourceAdapterFetchHeaders({
  "User-Agent": "caller-user-agent",
  Accept: "text/plain",
  "X-API-Key": "test-only",
  "X-Site-Session": "test-only",
});
assert.deepEqual(Object.keys(adapterHeaders).sort(), ["Accept", "User-Agent"]);
assert.equal(adapterHeaders.Accept, "application/json");
assert.notEqual(adapterHeaders["User-Agent"], "caller-user-agent");

const cratesHeaders = buildSourceAdapterFetchHeaders({
  "User-Agent": "caller-user-agent",
  Authorization: "Bearer test-only",
}, "crates_io_readme", "text/html");
assert.deepEqual(Object.keys(cratesHeaders).sort(), ["Accept", "User-Agent"]);
assert.equal(cratesHeaders.Accept, "text/html");
assert.match(cratesHeaders["User-Agent"], /^FF-Occam\/1\.0 \(\+https:\/\/github\.com\//);

const markdown = renderSourceAdapterPayload("npm_registry_package", {
  name: "example",
  version: "1.2.3",
  description: "Example package.",
  license: "MIT",
  homepage: "https://example.test/",
  repository: { url: "git+https://github.com/example/example.git" },
  dependencies: { alpha: "^1.0.0" },
}, "https://registry.npmjs.org/example/latest");

assert.match(markdown, /^# example/m);
assert.match(markdown, /Example package\./);
assert.match(markdown, /Latest version: 1\.2\.3/);
assert.match(markdown, /https:\/\/github\.com\/example\/example/);
assert.match(markdown, /`alpha`: `\^1\.0\.0`/);
assert.match(markdown, /Source: \[npm registry package metadata\]/);
assert.doesNotMatch(markdown, /author|maintainer|email/i);

assert.equal(CRATES_README_SELECTOR, "#crate-readme");
const cratesMarkdown = renderCratesReadmeHtml(`
  <h1>Serde</h1>
  <p>A serialization framework with <a href="https://serde.rs/">documentation</a>.</p>
  <pre><code class="language-rust">fn main() {
  println!("rich");
}</code></pre>
  <table><thead><tr><th>Feature</th></tr></thead><tbody><tr><td>derive</td></tr></tbody></table>
`, "serde", "1.0.229");
assert.match(cratesMarkdown, /^# serde 1\.0\.229/m);
assert.match(cratesMarkdown, /\[documentation\]\(https:\/\/serde\.rs\/\)/);
assert.match(cratesMarkdown, /```rust\nfn main\(\)/);
assert.match(cratesMarkdown, /\| Feature \|/);

let fakeNow = 0;
const starts = [];
const limiter = createSerialRateLimiter({
  intervalMs: 1_000,
  now: () => fakeNow,
  wait: async (delayMs) => {
    fakeNow += delayMs;
  },
});
await Promise.all([
  limiter.run(() => starts.push(fakeNow)),
  limiter.run(() => starts.push(fakeNow)),
  limiter.run(() => starts.push(fakeNow)),
]);
assert.deepEqual(starts, [0, 1_000, 2_000]);

console.log("source-adapter.selftest: OK");
