import assert from "node:assert/strict";
import { buildToolCall, formatPlain, parseDataArgs, runDataCommand } from "./occam-data-cli.mjs";

const read = parseDataArgs("read", [
  "https://example.com/",
  "--json",
  "--focus",
  "timeout",
  "--max-tokens",
  "800",
  "--fit",
  "--backend",
  "http",
]);
assert.equal(read.json, true);
assert.equal(read.focus, "timeout");
assert.equal(read.maxTokens, 800);
assert.equal(read.fit, true);
assert.equal(read.backend, "http");
assert.deepEqual(read.positionals, ["https://example.com/"]);
assert.deepEqual(buildToolCall(read), {
  tool: "occam_transcode",
  arguments: {
    url: "https://example.com/",
    backend_policy: "http",
    focus_query: "timeout",
    max_tokens: 800,
    fit_markdown: true,
  },
});

const search = parseDataArgs("search", ["nginx", "proxy", "--max-results=5"]);
assert.deepEqual(buildToolCall(search), {
  tool: "occam_search",
  arguments: { query: "nginx proxy", max_results: 5 },
});

const digest = parseDataArgs("digest", [
  "https://a.example/",
  "https://b.example/",
  "--focus=proxy_pass",
  "--max-tokens=400",
]);
assert.deepEqual(buildToolCall(digest), {
  tool: "occam_digest",
  arguments: {
    urls: ["https://a.example/", "https://b.example/"],
    focus_query: "proxy_pass",
    per_url_max_tokens: 400,
  },
});

assert.equal(buildToolCall(parseDataArgs("read", [])).error, "missing url");
assert.equal(parseDataArgs("read", ["--max-tokens", "12"]).error, "--max-tokens must be an integer >= 128");

assert.match(formatPlain("read", { ok: false, failure: { code: "http_404", message: "gone" } }), /http_404/);
assert.equal(formatPlain("read", { ok: true, markdown: "hello" }), "hello");
assert.equal(
  formatPlain("search", { ok: true, results: [{ id: "S1", title: "A", url: "https://a" }] }),
  "S1\tA\thttps://a",
);

const usage = await runDataCommand("read", ["--help"]);
assert.equal(usage, 0);
const bad = await runDataCommand("read", []);
assert.equal(bad, 2);

const hooked = await runDataCommand("read", ["https://example.com/", "--json"], {
  callTool: async (tool, args) => {
    assert.equal(tool, "occam_transcode");
    assert.equal(args.url, "https://example.com/");
    return { ok: true, markdown: "ok" };
  },
});
assert.equal(hooked, 0);

const failed = await runDataCommand("search", ["q"], {
  callTool: async () => ({ ok: false, failure: { code: "search_unconfigured", message: "off" } }),
});
assert.equal(failed, 1);

console.log("OCCAM_DATA_CLI_SELFTEST_OK");
