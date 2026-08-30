import assert from "node:assert/strict";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  renderSourceAdapterPayload,
  resolveSourceAdapter,
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

console.log("source-adapter.selftest: OK");
