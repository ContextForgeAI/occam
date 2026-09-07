import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { assembleBrief, parseBriefArgs, significanceOf } from "./occam-brief.mjs";
import { runBriefCommand } from "./occam-brief-cli.mjs";

assert.equal(parseBriefArgs([]).error, "need --url or --from-json");
assert.equal(parseBriefArgs(["--help"]).help, true);
assert.equal(significanceOf("The old flag is deprecated in 2.0"), "significant");
assert.equal(significanceOf("See also the tutorial"), "noise");

const unchanged = assembleBrief({
  pages: [
    {
      url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions",
      ok: true,
      unchanged: true,
      contentHash: "abc",
      markdown: "",
    },
  ],
});
assert.equal(unchanged.ok, true);
assert.equal(unchanged.summary.unchanged, 1);
assert.equal(unchanged.pages[0].significance, "none");

const changed = assembleBrief({
  pages: [
    {
      url: "https://nginx.org/en/docs/",
      ok: true,
      unchanged: false,
      markdown: "proxy_buffers default changed in 1.27. The old size is deprecated.",
    },
  ],
});
assert.equal(changed.summary.significant, 1);

const failed = assembleBrief({
  pages: [{ url: "https://example.com/", ok: false, failureCode: "captcha_or_challenge" }],
});
assert.equal(failed.ok, false);

const dir = mkdtempSync(join(tmpdir(), "occam-brief-"));
const input = join(dir, "in.json");
writeFileSync(
  input,
  JSON.stringify({
    toolchain: "ff-occam/1.0.0-rc.2",
    createdAt: "2026-09-07T14:00:00Z",
    pages: [
      {
        url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Functions",
        ok: true,
        unchanged: true,
        contentHash: "bda66461b52d3d0aea8541e47706ebdbe09c242cc5ba2d537c3cb0293517a738",
        markdown: "",
      },
    ],
  }),
);
const out = join(dir, "out");
const code = await runBriefCommand(["--from-json", input, "--out", out, "--json"]);
assert.equal(code, 0);
const written = JSON.parse(readFileSync(join(out, "brief-state.json"), "utf8"));
assert.equal(written.schema, "occam.docs-change-brief.v1");
assert.equal(written.summary.unchanged, 1);
assert.match(readFileSync(join(out, "brief.md"), "utf8"), /unchanged:true/);

console.log("occam-brief.selftest: OK");
