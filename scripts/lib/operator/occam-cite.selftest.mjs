import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { assembleCite, parseCiteArgs } from "./occam-cite.mjs";
import { runCiteCommand } from "./occam-cite-cli.mjs";

assert.equal(parseCiteArgs([]).error, "need --claim and --url, or --from-json");
assert.equal(parseCiteArgs(["--help"]).help, true);

const found = assembleCite({
  claim: "closures remember variables",
  url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions",
  payload: {
    ok: true,
    found: true,
    retrieved: true,
    verdict: "not_evaluated",
    matches: [{ text: "A closure is any piece of source code", score: 51, sourceSelector: "#content" }],
  },
});
assert.equal(found.ok, true);
assert.equal(found.found, true);
assert.equal(found.verdict, "not_evaluated");
assert.equal(found.matchCount, 1);
assert.match(found.reviewer.not, /does not decide support/);

const missing = assembleCite({
  claim: "requires a cloud API key",
  url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions",
  payload: { ok: true, found: false, retrieved: false, verdict: "not_evaluated", proven: true, matches: [] },
});
assert.equal(missing.ok, true);
assert.equal(missing.found, false);
assert.equal(missing.proven, true);

const failed = assembleCite({
  claim: "x",
  url: "https://example.com/",
  payload: { ok: false, failure: { code: "timeout" } },
});
assert.equal(failed.ok, false);
assert.equal(failed.found, null);

const dir = mkdtempSync(join(tmpdir(), "occam-cite-"));
const input = join(dir, "in.json");
writeFileSync(
  input,
  JSON.stringify({
    claim: "closures remember variables",
    url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions",
    toolchain: "ff-occam/1.0.0-rc.2",
    createdAt: "2026-09-07T14:08:06Z",
    payload: {
      ok: true,
      found: true,
      retrieved: true,
      verdict: "not_evaluated",
      matches: [{ text: "A closure is any piece of source code", score: 51.4, sourceSelector: "#content" }],
    },
  }),
);
const out = join(dir, "out");
const code = await runCiteCommand(["--from-json", input, "--out", out, "--json"]);
assert.equal(code, 0);
const written = JSON.parse(readFileSync(join(out, "inspect.json"), "utf8"));
assert.equal(written.schema, "occam.citation-inspect.v1");
assert.equal(written.found, true);
assert.match(readFileSync(join(out, "evidence.txt"), "utf8"), /does not decide support/);

console.log("occam-cite.selftest: OK");
