import assert from "node:assert/strict";
import { parseToolJson } from "./mcp-tool-json.mjs";

const ok = parseToolJson({
  isError: false,
  content: [{ type: "text", text: '{"ok":true,"markdown":"hi"}' }],
});
assert.equal(ok.isError, false);
assert.equal(ok.parsed?.ok, true);

const typedFail = parseToolJson({
  isError: true,
  content: [{ type: "text", text: '{"ok":false,"failure":{"code":"http_403"}}' }],
});
assert.equal(typedFail.isError, true);
assert.equal(typedFail.parsed?.ok, false);
assert.equal(typedFail.parsed?.failure?.code, "http_403");

const probeFail = parseToolJson({
  isError: true,
  content: [{ type: "text", text: '{"ok":false,"failureCode":"http_403"}' }],
});
assert.equal(probeFail.parsed?.failureCode, "http_403");

const digestFail = parseToolJson({
  isError: true,
  content: [{
    type: "text",
    text: '{"ok":false,"failureCode":"digest_failed","items":[{"ok":false,"failure":{"code":"http_429"}}]}',
  }],
});
assert.equal(digestFail.parsed?.failureCode, "digest_failed");
assert.equal(digestFail.parsed?.items?.[0]?.failure?.code, "http_429");

const empty = parseToolJson({ isError: true, content: [] });
assert.equal(empty.parsed, null);
assert.equal(empty.isError, true);

console.log("mcp-tool-json.selftest: OK");
