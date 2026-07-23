import assert from "node:assert/strict";
import {
  isPlainTextContentType,
  isUtf8TextBody,
  looksLikeMarkdown,
  plainTextToMarkdown,
  shouldPassThroughPlainText,
} from "./plain-text-pass-through.mjs";

assert.equal(isPlainTextContentType("text/plain; charset=utf-8"), true);
assert.equal(isPlainTextContentType("text/html"), false);
assert.equal(isPlainTextContentType("application/octet-stream"), true);

const readme = `# TypeScript

For the latest stable version:

\`\`\`
npm install -D typescript
\`\`\`
`;
assert.ok(looksLikeMarkdown(readme));
assert.ok(shouldPassThroughPlainText("text/plain", readme));
const md = plainTextToMarkdown(readme, "https://raw.githubusercontent.com/microsoft/TypeScript/main/README.md");
assert.ok(md.includes("npm install -D typescript"));
assert.ok(!md.startsWith("```\n# TypeScript"));

const plain = "hello world\nline two";
assert.ok(!looksLikeMarkdown(plain));
const fenced = plainTextToMarkdown(plain, "https://example.com/data.txt");
assert.ok(fenced.startsWith("```"));
assert.ok(fenced.includes("hello world"));

assert.equal(isUtf8TextBody("ok\x00bad"), false);

console.log("plain-text-pass-through.selftest: OK");
