import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  RESEARCH_SCHEMA,
  assembleResearch,
  canonicalizeUrl,
  dedupeUrls,
  inScope,
  parseExcerptSections,
  parseResearchArgs,
  planResearch,
  restoreExtractedPages,
} from "./occam-research.mjs";
import { runResearchCommand } from "./occam-research-cli.mjs";

assert.equal(canonicalizeUrl("https://Nginx.org/en/docs/#top"), "https://nginx.org/en/docs/");
assert.equal(inScope("https://nginx.org/en/docs/http.html", "https://nginx.org/en/docs/", true), true);
assert.equal(inScope("https://other.example/x", "https://nginx.org/en/docs/", true), false);
assert.deepEqual(
  dedupeUrls(["https://a.example/", "https://a.example/#x", "https://b.example/"]),
  ["https://a.example/", "https://b.example/"],
);

const args = parseResearchArgs(["--seed", "https://nginx.org/", "--out", "tmp/r", "--max-pages", "2"]);
assert.equal(args.seed, "https://nginx.org/");
assert.equal(args.maxPages, 2);
assert.equal(parseResearchArgs(["--help"]).help, true);
assert.equal(parseResearchArgs([]).error, "need --seed or --from-json");

const discovered = [
  { url: "https://other.example/offsite", title: "off" },
  { url: "https://nginx.org/", title: "home" },
  { url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html", title: "proxy module" },
  { url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html#dup", title: "dup" },
  { url: "https://nginx.org/en/docs/beginners_guide.html", title: "beginner" },
];

const scoped = planResearch({
  seed: "https://nginx.org/en/docs/",
  focus: "proxy_read_timeout proxy_pass",
  discovered,
  extracted: [],
  budgets: { maxUrls: 8, maxPages: 2, deadlineMs: 60_000, maxBytes: 1_000_000 },
});
assert.ok(scoped.discovery.outOfScope.includes("https://other.example/offsite"));
assert.ok(scoped.nextUrl.includes("ngx_http_proxy_module"));
assert.equal(scoped.stop, null);

const paged = planResearch({
  seed: "https://nginx.org/en/docs/",
  focus: "proxy_pass",
  discovered,
  extracted: [
    { url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html", ok: true, bytes: 100 },
    { url: "https://nginx.org/en/docs/beginners_guide.html", ok: true, bytes: 80 },
  ],
  budgets: { maxPages: 2, maxUrls: 8, deadlineMs: 60_000, maxBytes: 1_000_000 },
});
assert.equal(paged.stop, "budget_pages");
assert.equal(paged.nextUrl, null);

const timed = planResearch({
  seed: "https://nginx.org/en/docs/",
  discovered,
  extracted: [],
  budgets: { deadlineMs: 10, maxPages: 4, maxUrls: 8, maxBytes: 1_000_000 },
  startedAt: 0,
  now: 50,
});
assert.equal(timed.stop, "budget_time");

const resumed = planResearch({
  seed: "https://nginx.org/en/docs/",
  focus: "proxy_pass",
  discovered,
  extracted: [{ url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html", ok: true, bytes: 10 }],
  budgets: { maxPages: 4, maxUrls: 8, deadlineMs: 60_000, maxBytes: 1_000_000 },
});
assert.ok(resumed.nextUrl);
assert.notEqual(resumed.nextUrl, "https://nginx.org/en/docs/http/ngx_http_proxy_module.html");

const cancelled = planResearch({
  seed: "https://nginx.org/en/docs/",
  discovered,
  extracted: [],
  cancelled: true,
});
assert.equal(cancelled.stop, "cancelled");

const report = assembleResearch({
  seed: "https://nginx.org/en/docs/",
  focus: "proxy_pass",
  discovered,
  extracted: [
    { url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html", ok: true, bytes: 120 },
    { url: "https://nginx.org/en/docs/beginners_guide.html", ok: false, bytes: 0, failureCode: "thin_extract" },
  ],
  budgets: { maxPages: 2, maxUrls: 8, deadlineMs: 60_000, maxBytes: 1_000_000 },
  provider: "fixture",
});
assert.equal(report.schema, RESEARCH_SCHEMA);
assert.ok(Array.isArray(report.discovery.urls));
assert.ok(Array.isArray(report.extraction.urls));
assert.equal(report.extraction.failed, 1);
assert.equal(report.ok, false);
assert.equal(report.stop.reason, "budget_pages");

const dir = mkdtempSync(join(tmpdir(), "occam-research-"));
const input = join(dir, "input.json");
writeFileSync(
  input,
  JSON.stringify({
    seed: "https://nginx.org/en/docs/",
    focus: "proxy_pass",
    provider: "fixture",
    discovered,
    extracted: [
      {
        url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html",
        ok: true,
        bytes: 40,
        markdown: "proxy_pass\n",
      },
    ],
  }),
);
const out = join(dir, "out");
const code = await runResearchCommand(["--from-json", input, "--out", out, "--max-pages", "1", "--json"]);
assert.equal(code, 0);
const written = JSON.parse(readFileSync(join(out, "research-state.json"), "utf8"));
assert.equal(written.schema, RESEARCH_SCHEMA);
assert.equal(written.extraction.pages, 1);
assert.ok(written.discovery.outOfScope.length >= 1);
assert.match(readFileSync(join(out, "excerpts.txt"), "utf8"), /proxy_pass/);
assert.match(readFileSync(join(out, "pages.json"), "utf8"), /proxy_pass/);

assert.deepEqual(
  restoreExtractedPages({
    urls: [{ url: "https://a.example/", ok: true, bytes: 4 }],
    pages: [{ url: "https://a.example/", markdown: "KEEP_ME" }],
    excerpts: "",
  })[0].markdown,
  "KEEP_ME",
);
assert.equal(
  parseExcerptSections("# Site research\n\n## https://a.example/\n\nFIRST\n")[0][1],
  "FIRST",
);

const liveDir = mkdtempSync(join(tmpdir(), "occam-research-live-"));
const seed = "https://example.test/first";
const second = "https://example.test/second";
const third = "https://example.test/third";
const mockPages = {
  [seed]: "FIRST_SOURCE_REQUIRED_COMMAND\n",
  [second]: "SECOND_SOURCE_STEPS\n",
  [third]: "THIRD_PAGE_BODY\n",
};
const mockMap = {
  ok: true,
  links: [
    { url: seed, title: "first" },
    { url: second, title: "second" },
    { url: third, title: "third" },
  ],
};

async function researchWithMock(args, pages = mockPages) {
  return runResearchCommand(args, async (tool, toolArgs) => {
    if (tool === "occam_map") return mockMap;
    const url = String(toolArgs.url ?? "");
    return { ok: true, markdown: pages[url] ?? "" };
  });
}

assert.equal(
  await researchWithMock(["--seed", seed, "--out", liveDir, "--max-pages", "1", "--json"]),
  0,
);
const afterFirst = readFileSync(join(liveDir, "excerpts.txt"), "utf8");
assert.match(afterFirst, /FIRST_SOURCE_REQUIRED_COMMAND/);
assert.doesNotMatch(afterFirst, /SECOND_SOURCE_STEPS/);

assert.equal(
  await researchWithMock(["--seed", seed, "--out", liveDir, "--max-pages", "2", "--resume", "--json"]),
  0,
);
const afterSecond = readFileSync(join(liveDir, "excerpts.txt"), "utf8");
assert.match(afterSecond, /FIRST_SOURCE_REQUIRED_COMMAND/);
assert.match(afterSecond, /SECOND_SOURCE_STEPS/);
const pagesAfterSecond = JSON.parse(readFileSync(join(liveDir, "pages.json"), "utf8"));
assert.equal(pagesAfterSecond.pages.length, 2);
assert.ok(pagesAfterSecond.pages.every((row) => row.markdown));

assert.equal(
  await researchWithMock(["--seed", seed, "--out", liveDir, "--max-pages", "3", "--resume", "--json"]),
  0,
);
const afterThird = readFileSync(join(liveDir, "excerpts.txt"), "utf8");
assert.match(afterThird, /FIRST_SOURCE_REQUIRED_COMMAND/);
assert.match(afterThird, /SECOND_SOURCE_STEPS/);
assert.match(afterThird, /THIRD_PAGE_BODY/);

const discoverFailDir = mkdtempSync(join(tmpdir(), "occam-research-discover-"));
assert.equal(
  await researchWithMock(["--seed", seed, "--out", discoverFailDir, "--max-pages", "1", "--json"]),
  0,
);
const discoverFail = await runResearchCommand(
  ["--seed", seed, "--out", discoverFailDir, "--max-pages", "2", "--resume", "--json"],
  async (tool) => {
    if (tool === "occam_map") return { ok: false, failureCode: "timeout" };
    throw new Error("transcode must not run after discovery failure");
  },
);
assert.equal(discoverFail, 1);
assert.match(readFileSync(join(discoverFailDir, "excerpts.txt"), "utf8"), /FIRST_SOURCE_REQUIRED_COMMAND/);
const discoverFailState = JSON.parse(readFileSync(join(discoverFailDir, "research-state.json"), "utf8"));
assert.equal(discoverFailState.extraction.succeeded, 1);
assert.equal(discoverFailState.ok, false);

const byteDir = mkdtempSync(join(tmpdir(), "occam-research-bytes-"));
const huge = "X".repeat(5000);
const byteCode = await runResearchCommand(
  ["--seed", seed, "--out", byteDir, "--max-pages", "2", "--max-bytes", "1024", "--json"],
  async (tool, toolArgs, options) => {
    assert.ok(options?.requestTimeoutMs > 0, "remaining time must bound the MCP call");
    if (tool === "occam_map") return mockMap;
    return { ok: true, markdown: huge };
  },
);
assert.equal(byteCode, 1);
const byteState = JSON.parse(readFileSync(join(byteDir, "research-state.json"), "utf8"));
assert.ok(byteState.extraction.bytes <= 1024);
assert.equal(byteState.stop.reason, "budget_bytes");
assert.equal(byteState.extraction.succeeded, 0);
assert.equal(byteState.extraction.omitted[0].reason, "budget_bytes");
assert.equal(byteState.extraction.omitted[0].bytes, 5000);
assert.doesNotMatch(readFileSync(join(byteDir, "excerpts.txt"), "utf8"), /XXXX/);

const keepDir = mkdtempSync(join(tmpdir(), "occam-research-keep-"));
const keepCode = await runResearchCommand(
  ["--seed", seed, "--out", keepDir, "--max-pages", "2", "--max-bytes", "1024", "--json"],
  async (tool, toolArgs) => {
    if (tool === "occam_map") return mockMap;
    if (toolArgs.url === seed) return { ok: true, markdown: "SMALL_OK" };
    return { ok: true, markdown: huge };
  },
);
assert.equal(keepCode, 0);
const keepState = JSON.parse(readFileSync(join(keepDir, "research-state.json"), "utf8"));
assert.equal(keepState.stop.reason, "budget_bytes");
assert.equal(keepState.extraction.succeeded, 1);
assert.ok(keepState.extraction.bytes <= 1024);
assert.match(readFileSync(join(keepDir, "excerpts.txt"), "utf8"), /SMALL_OK/);
assert.doesNotMatch(readFileSync(join(keepDir, "excerpts.txt"), "utf8"), /XXXX/);

console.log("occam-research.selftest: OK");
