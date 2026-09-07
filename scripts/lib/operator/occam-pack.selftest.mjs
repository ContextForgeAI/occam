import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { assemblePack, estimateTokens, parsePackArgs, planPackBudget, searchHitUrls } from "./occam-pack.mjs";
import { runPackCommand } from "./occam-pack-cli.mjs";

const parsed = parsePackArgs([
  "--task",
  "How do closures work?",
  "--url",
  "https://a.example/",
  "--url=https://b.example/",
  "--budget=800",
  "--focus=closures",
  "--out",
  "tmp/pack",
]);
assert.equal(parsed.task, "How do closures work?");
assert.deepEqual(parsed.urls, ["https://a.example/", "https://b.example/"]);
assert.equal(parsed.budget, 800);
assert.equal(parsed.focus, "closures");
assert.equal(parsePackArgs(["--help"]).help, true);
assert.equal(parsePackArgs([]).error, "missing --task");
assert.equal(parsePackArgs(["--task", "x"]).error, "need --url, --search, or --from-json");
assert.equal(parsePackArgs(["--task", "x", "--budget", "12"]).error, "--budget must be an integer >= 128");

const transcodePack = assemblePack({
  task: "Show function scope and closures",
  toolchain: "ff-occam/1.0.0-rc.2",
  createdAt: "2026-09-05T18:00:00Z",
  settings: { budget: 800, focus: "function scope closures", backend: "http" },
  responses: [
    {
      tool: "occam_transcode",
      payload: {
        ok: true,
        url: { url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions" },
        markdown: "Functions form a scope. A closure remembers variables.",
        backend: "http",
        contentHash: "abc",
        materializationKey: "sha256:mk",
        compile: {
          tokensEstimated: 40,
          omitted: { reason: "budget_exceeded", tokensDropped: 100, regions: ["unchosen"], sections: 2 },
        },
        completeness: { status: "incomplete", incompleteReason: "focus_body_truncated", suggestedMinTokens: 264 },
        focus: { status: "hit" },
      },
    },
  ],
});
assert.equal(transcodePack.manifest.schema, "occam.context-pack.v1");
assert.equal(transcodePack.manifest.ok, true);
assert.equal(transcodePack.manifest.failed, 0);
assert.equal(transcodePack.sources[0].url.includes("developer.mozilla.org"), true);
assert.equal(transcodePack.omissions[0].reason, "budget_exceeded");
assert.match(transcodePack.excerpts, /Functions form a scope/);
assert.equal(transcodePack.manifest.budget.estimator, "heuristic-unicode-v1");
assert.ok(transcodePack.manifest.budget.wrapper > 0);
assert.equal(
  transcodePack.manifest.budget.total,
  transcodePack.manifest.budget.content + transcodePack.manifest.budget.wrapper,
);

const failedPack = assemblePack({
  task: "Read a missing page",
  settings: { budget: 256 },
  responses: [
    {
      tool: "occam_transcode",
      payload: {
        ok: false,
        url: { url: "https://example.com/missing" },
        failure: { code: "http_404", message: "not found" },
      },
    },
  ],
});
assert.equal(failedPack.manifest.ok, false);
assert.equal(failedPack.manifest.failed, 1);
assert.match(failedPack.excerpts, /http_404/);

const digestPack = assemblePack({
  task: "Compare proxy docs",
  settings: { budget: 500, focus: "proxy_pass" },
  responses: [
    {
      tool: "occam_digest",
      payload: {
        ok: true,
        items: [
          {
            url: "https://nginx.org/a",
            ok: true,
            excerpt: "proxy_pass http://localhost:8080;",
            focusMatched: false,
            focus: "weak",
            completeness: { status: "incomplete", incompleteReason: "focus_body_truncated", suggestedMinTokens: 470 },
            tokensEstimated: 320,
          },
          {
            url: "https://nginx.org/b",
            ok: false,
            failureCode: "timeout",
            message: "slow",
          },
        ],
      },
    },
  ],
});
assert.equal(digestPack.manifest.failed, 1);
assert.equal(digestPack.manifest.succeeded, 1);
assert.match(digestPack.excerpts, /proxy_pass/);
assert.equal(digestPack.omissions[0].url, "https://nginx.org/a");

const unchanged = assemblePack({
  task: "Re-read MDN",
  responses: [
    {
      tool: "occam_transcode",
      payload: {
        ok: true,
        url: { url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions" },
        markdown: "",
        unchanged: true,
        contentHash: "same",
        materializationKey: "sha256:mk",
      },
    },
  ],
});
assert.match(unchanged.excerpts, /Unchanged/);
assert.equal(unchanged.sources[0].unchanged, true);

assert.deepEqual(
  searchHitUrls({ results: [{ url: "https://a" }, { url: "https://b" }, { title: "no url" }] }),
  ["https://a", "https://b"],
);
assert.equal(estimateTokens(""), 0);
assert.ok(estimateTokens("abcd") >= 1);

const usage = await runPackCommand(["--help"]);
assert.equal(usage, 0);
const bad = await runPackCommand([]);
assert.equal(bad, 2);

const dir = mkdtempSync(join(tmpdir(), "occam-pack-"));
try {
  const exit = await runPackCommand(
    [
      "--task",
      "Show closures",
      "--url",
      "https://example.com/doc",
      "--budget=256",
      "--focus=closures",
      "--out",
      dir,
      "--json",
    ],
    {
      now: "2026-09-05T18:00:00Z",
      callTool: async (tool, args) => {
        assert.equal(tool, "occam_transcode");
        assert.equal(args.url, "https://example.com/doc");
        const planned = planPackBudget({
          declared: 256,
          task: "Show closures",
          settings: { budget: 256, focus: "closures", urls: ["https://example.com/doc"] },
          urls: ["https://example.com/doc"],
        });
        assert.equal(planned.possible, true);
        assert.equal(args.max_tokens, planned.perSource);
        assert.ok(args.max_tokens < 256);
        assert.equal(args.fit_markdown, true);
        return {
          ok: true,
          url: { url: args.url },
          markdown: "A closure remembers variables.",
          backend: "http",
          contentHash: "h1",
          compile: { tokensEstimated: 8 },
          completeness: { status: "complete" },
        };
      },
    },
  );
  assert.equal(exit, 0);
  const manifest = JSON.parse(readFileSync(join(dir, "manifest.json"), "utf8"));
  assert.equal(manifest.ok, true);
  assert.equal(manifest.budget.declared, 256);
  assert.match(readFileSync(join(dir, "excerpts.txt"), "utf8"), /closure remembers/);
} finally {
  rmSync(dir, { recursive: true, force: true });
}

const searchDir = mkdtempSync(join(tmpdir(), "occam-pack-search-"));
try {
  const calls = [];
  const exit = await runPackCommand(
    ["--task", "Find proxy docs", "--search", "nginx proxy_pass", "--max-sources=2", "--out", searchDir],
    {
      callTool: async (tool, args) => {
        calls.push(tool);
        if (tool === "occam_search") {
          return {
            ok: true,
            results: [
              { id: "S1", title: "A", url: "https://nginx.org/a" },
              { id: "S2", title: "B", url: "https://nginx.org/b" },
            ],
          };
        }
        assert.equal(tool, "occam_digest");
        assert.deepEqual(args.urls, ["https://nginx.org/a", "https://nginx.org/b"]);
        return {
          ok: true,
          items: [
            { url: "https://nginx.org/a", ok: true, excerpt: "proxy_pass" },
            { url: "https://nginx.org/b", ok: true, excerpt: "beginner" },
          ],
        };
      },
    },
  );
  assert.equal(exit, 0);
  assert.deepEqual(calls, ["occam_search", "occam_digest"]);
} finally {
  rmSync(searchDir, { recursive: true, force: true });
}

const overflow = assemblePack({
  task: "Two short excerpts still overflow a tiny total",
  settings: { budget: 128 },
  responses: [
    {
      tool: "occam_digest",
      payload: {
        ok: true,
        items: [
          { url: "https://a.example/", ok: true, excerpt: "alpha ".repeat(40) },
          { url: "https://b.example/", ok: true, excerpt: "bravo ".repeat(40) },
        ],
      },
    },
  ],
});
assert.equal(overflow.manifest.budget.overBudget, true);
assert.equal(overflow.manifest.ok, false);
assert.equal(overflow.manifest.budget.reason, "over_budget");
assert.match(overflow.excerpts, /alpha/);
assert.match(overflow.excerpts, /bravo/);

const manyDir = mkdtempSync(join(tmpdir(), "occam-pack-many-"));
try {
  const seen = [];
  const exit = await runPackCommand(
    [
      "--task",
      "Two sources",
      "--url",
      "https://a.example/",
      "--url",
      "https://b.example/",
      "--budget=128",
      "--out",
      manyDir,
      "--json",
    ],
    {
      callTool: async (tool, args) => {
        seen.push({ tool, perUrl: args.per_url_max_tokens });
        assert.equal(tool, "occam_digest");
        assert.ok(args.per_url_max_tokens < 128);
        return {
          ok: true,
          items: [
            { url: "https://a.example/", ok: true, excerpt: `FIRST_MARK ${"alpha ".repeat(40)}` },
            { url: "https://b.example/", ok: true, excerpt: `SECOND_MARK ${"bravo ".repeat(40)}` },
          ],
        };
      },
    },
  );
  assert.equal(exit, 1);
  const manifest = JSON.parse(readFileSync(join(manyDir, "manifest.json"), "utf8"));
  assert.equal(manifest.ok, false);
  assert.equal(manifest.budget.overBudget, true);
  assert.ok(manifest.budget.allocatedPerSource < 128);
  const excerpts = readFileSync(join(manyDir, "excerpts.txt"), "utf8");
  assert.match(excerpts, /FIRST_MARK/);
  assert.match(excerpts, /SECOND_MARK/);
} finally {
  rmSync(manyDir, { recursive: true, force: true });
}

const wrapPlan = planPackBudget({
  declared: 128,
  task: "W".repeat(2000),
  urls: ["https://a.example/"],
});
assert.equal(wrapPlan.possible, false);
assert.equal(wrapPlan.reason, "wrapper_exceeds_budget");

const wrapDir = mkdtempSync(join(tmpdir(), "occam-pack-wrap-"));
try {
  let fetched = false;
  const exit = await runPackCommand(
    [
      "--task",
      "W".repeat(2000),
      "--url",
      "https://a.example/",
      "--budget=128",
      "--out",
      wrapDir,
      "--json",
    ],
    {
      callTool: async () => {
        fetched = true;
        return { ok: true, url: { url: "https://a.example/" }, markdown: "should-not-fetch" };
      },
    },
  );
  assert.equal(fetched, false);
  assert.equal(exit, 1);
  const manifest = JSON.parse(readFileSync(join(wrapDir, "manifest.json"), "utf8"));
  assert.equal(manifest.ok, false);
  assert.equal(manifest.budget.reason, "wrapper_exceeds_budget");
} finally {
  rmSync(wrapDir, { recursive: true, force: true });
}

console.log("occam-pack.selftest: OK");
