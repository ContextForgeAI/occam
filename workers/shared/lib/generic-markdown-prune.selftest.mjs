import assert from "node:assert/strict";
import { genericMarkdownPrune } from "./generic-markdown-prune.mjs";

const noisy = `
# Docs

Core paragraph about the framework.

Community

Was this helpful?

- [Discord](https://discord.com/x)
- [Introduction](/docs/intro)
- [Installation](/docs/install)
- [Configuration](/docs/config)
- [Deployment](/docs/deploy)

Help improve MDN

Become a Sponsor
`;

const pruned = genericMarkdownPrune(noisy);
assert.ok(!pruned.includes("Community"));
assert.ok(!pruned.includes("Was this helpful"));
assert.ok(!pruned.includes("Help improve MDN"));
assert.ok(pruned.includes("Core paragraph"));

console.log("generic-markdown-prune.selftest: OK");
