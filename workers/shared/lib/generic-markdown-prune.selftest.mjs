import assert from "node:assert/strict";
import { genericMarkdownPrune } from "./generic-markdown-prune.mjs";

const noisy = `
# Docs

Core paragraph about the framework.

Discord's safety work includes teen protections and family-center controls.

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
assert.ok(pruned.includes("Discord's safety work"));
assert.ok(!pruned.includes("[Discord](https://discord.com/x)"));

console.log("generic-markdown-prune.selftest: OK");
