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
  parseResearchArgs,
  planResearch,
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

console.log("occam-research.selftest: OK");
