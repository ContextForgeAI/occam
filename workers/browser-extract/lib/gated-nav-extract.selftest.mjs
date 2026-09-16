// Classifier + fixture shape only (no jsdom). Extraction fidelity:
// workers/browser-extract/lib/gated-nav-html-extract.selftest.mjs
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  GATED_CONTENT_FLOOR,
  classifyGatedNavResult,
  isGatedNavStatus,
  shouldContinueAfterNavStatus,
} from "./gated-nav-extract.mjs";

const fixtures = join(dirname(fileURLToPath(import.meta.url)), "../fixtures");

function visibleText(html) {
  return html
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

assert.equal(shouldContinueAfterNavStatus(200), true);
assert.equal(shouldContinueAfterNavStatus(304), true);
assert.equal(shouldContinueAfterNavStatus(401), true);
assert.equal(shouldContinueAfterNavStatus(403), true);
assert.equal(shouldContinueAfterNavStatus(404), false);
assert.equal(shouldContinueAfterNavStatus(410), false);
assert.equal(shouldContinueAfterNavStatus(429), false);
assert.equal(shouldContinueAfterNavStatus(500), false);
assert.equal(isGatedNavStatus(403), true);
assert.equal(isGatedNavStatus(404), false);

assert.deepEqual(classifyGatedNavResult({ navStatus: 200, markdown: "article" }), { kind: "normal" });

const articleHtml = readFileSync(join(fixtures, "gated-403-article.html"), "utf8");
assert.match(articleHtml, /<article>/i);
assert.match(articleHtml, /<ol>/i);
assert.match(articleHtml, /<code>/i);
const articleText = visibleText(articleHtml);
assert.ok(articleText.length >= GATED_CONTENT_FLOOR, "article fixture must exceed the content floor");
const articleDecision = classifyGatedNavResult({
  navStatus: 403,
  markdown: articleText,
  isChallengeWall: false,
  looksThin: false,
  looksErrorShell: false,
});
assert.equal(articleDecision.kind, "ok");
assert.equal(articleDecision.statusCode, 403);
assert.equal(articleDecision.accessStatus, "blocked-but-content-available");

const challengeHtml = readFileSync(join(fixtures, "gated-403-challenge.html"), "utf8");
assert.match(challengeHtml, /challenge-form|cf-turnstile/i);
const challengeText = visibleText(challengeHtml);
assert.ok(challengeText.length < GATED_CONTENT_FLOOR, "challenge fixture must stay a thin shell");
const challengeDecision = classifyGatedNavResult({
  navStatus: 403,
  markdown: challengeText,
  isChallengeWall: true,
  looksThin: true,
  looksErrorShell: false,
});
assert.equal(challengeDecision.kind, "fail");
assert.equal(challengeDecision.failure, "captcha_or_challenge");
assert.equal(challengeDecision.statusCode, 403);

const thin403 = classifyGatedNavResult({
  navStatus: 403,
  markdown: "Forbidden",
  isChallengeWall: false,
  looksThin: true,
});
assert.equal(thin403.kind, "fail");
assert.equal(thin403.failure, "http_403");

console.log("gated-nav-extract.selftest: OK");
