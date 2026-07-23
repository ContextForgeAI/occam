import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  containsForbiddenKeys,
  findViolations,
  isDenylistedSelector,
  publishPlaybook,
  sanitizeForExport,
} from "./playbook-publish-sanitize.mjs";

assert.ok(isDenylistedSelector("body"));
assert.ok(isDenylistedSelector("[style*=display:none]"));
assert.ok(!isDenylistedSelector("main"));

const cookieFixture = {
  schema_version: "1.0",
  id: "gate.neg-cookie.example",
  hosts: ["gate.neg-cookie.example"],
  request: { headers: { Cookie: "session=gate-fixture-secret" } },
  extract: { contentSelectors: ["main"] },
};

assert.ok(containsForbiddenKeys(cookieFixture), "cookie header key is forbidden");
assert.deepEqual(
  findViolations(cookieFixture).some((v) => v.startsWith("forbidden")),
  true,
  "cookie fixture has forbidden violations",
);

const denylistFixture = {
  schema_version: "1.0",
  id: "bad-selectors.example",
  hosts: ["bad-selectors.example"],
  extract: { contentSelectors: ["body"] },
};
assert.ok(
  findViolations(denylistFixture).includes("denylist_selector:body"),
  "body selector rejected",
);

const goodFixture = {
  schema_version: "1.0",
  id: "good.example",
  hosts: ["good.example"],
  agent_notes: "Scoped selectors only.",
  extract: { contentSelectors: ["main", "#content"] },
};
assert.deepEqual(findViolations(goodFixture), [], "clean fixture passes");

const capped = sanitizeForExport({
  ...goodFixture,
  agent_notes: "x".repeat(2500),
  lessons: Array.from({ length: 12 }, (_, i) => ({ note: `lesson ${i}` })),
});
assert.equal(capped.agent_notes.length, 2000);
assert.equal(capped.lessons.length, 10);

const tempRoot = mkdtempSync(join(tmpdir(), "occam-publish-selftest-"));
try {
  const reject = publishPlaybook({
    inputPath: null,
    outputDir: tempRoot,
    ackCommunityReview: true,
    occamHome: tempRoot,
  });
  assert.equal(reject.ok, false);

  const inputPath = join(tempRoot, "cookie.playbook.json");
  writeFileSync(inputPath, JSON.stringify(cookieFixture));

  const blocked = publishPlaybook({
    inputPath,
    outputDir: join(tempRoot, "blocked"),
    ackCommunityReview: true,
    occamHome: tempRoot,
  });
  assert.equal(blocked.ok, false);
  assert.equal(blocked.failureCode, "secrets_detected");
  assert.ok(!blocked.exportPath);

  const ackMissing = publishPlaybook({
    inputPath: join(tempRoot, "good.playbook.json"),
    outputDir: join(tempRoot, "no-ack"),
    ackCommunityReview: false,
    occamHome: tempRoot,
  });
  assert.equal(ackMissing.failureCode, "ack_required");

  writeFileSync(join(tempRoot, "good.playbook.json"), JSON.stringify(goodFixture));
  const okDir = join(tempRoot, "export");
  const ok = publishPlaybook({
    inputPath: join(tempRoot, "good.playbook.json"),
    outputDir: okDir,
    ackCommunityReview: true,
    summary: "selftest export",
    occamHome: tempRoot,
  });
  assert.equal(ok.ok, true);
  assert.ok(readFileSync(join(okDir, "good.example.json"), "utf8").includes("good.example"));
  assert.ok(readFileSync(join(okDir, "PULL_REQUEST.md"), "utf8").includes("selftest export"));
  const manifestRow = JSON.parse(readFileSync(join(okDir, "manifest-row.json"), "utf8"));
  assert.equal(manifestRow.id, "good.example");
  assert.match(manifestRow.sha256, /^[a-f0-9]{64}$/);
  assert.equal(manifestRow.signature, null);
} finally {
  rmSync(tempRoot, { recursive: true, force: true });
}

console.log("playbook-publish-hygiene.selftest: OK");
