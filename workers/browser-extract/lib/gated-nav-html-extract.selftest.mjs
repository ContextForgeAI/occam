/**
 * Worker-level P0: run 403 fixtures through extractMarkdownFromHtml (jsdom +
 * Readability + turndown), then the same gated-nav decision the browser worker
 * uses. The classifier-only selftest stays dependency-free for docs-check.
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { JSDOM } from "jsdom";
import { extractMarkdownFromHtml } from "./extract-html.mjs";
import { isChallengeWall } from "./browser-session.mjs";
import {
  GATED_CONTENT_FLOOR,
  classifyGatedNavResult,
} from "./gated-nav-extract.mjs";

const fixtures = join(dirname(fileURLToPath(import.meta.url)), "../fixtures");

/** Keep in sync with the page.evaluate probe in browser-session.mjs. */
const CHALLENGE_NODE =
  'script[src*="challenges.cloudflare.com"], .cf-turnstile, #cf-turnstile, ' +
  "#challenge-form, #cf-challenge-running, iframe[src*=\"turnstile\"], " +
  'iframe[src*="hcaptcha"], iframe[title*="hCaptcha"], iframe[src*="recaptcha/api2"]';

function challengeProbeFromHtml(html, url) {
  const doc = new JSDOM(html, { url }).window.document;
  const bodyText = (doc.body?.textContent || "").trim();
  return {
    title: doc.title || "",
    textLen: bodyText.length,
    sampleLower: bodyText.slice(0, 400).toLowerCase(),
    hasChallengeNode: Boolean(doc.querySelector(CHALLENGE_NODE)),
  };
}

function decideFromHtml(html, url, navStatus) {
  const probe = challengeProbeFromHtml(html, url);
  const wall = isChallengeWall(probe);
  if (wall) {
    return {
      extracted: null,
      probe,
      wall,
      gated: classifyGatedNavResult({
        navStatus,
        markdown: "",
        isChallengeWall: true,
        looksThin: true,
        looksErrorShell: false,
      }),
    };
  }

  const extracted = extractMarkdownFromHtml(html, url);
  const markdown = extracted?.markdown ?? "";
  return {
    extracted,
    probe,
    wall,
    gated: classifyGatedNavResult({
      navStatus,
      markdown,
      isChallengeWall: false,
      looksThin: (extracted?.text_length ?? 0) < GATED_CONTENT_FLOOR,
      looksErrorShell: Boolean(extracted?.access?.error_shell),
    }),
  };
}

const articleUrl = "https://example.test/q/1";
const articleHtml = readFileSync(join(fixtures, "gated-403-article.html"), "utf8");
const article = decideFromHtml(articleHtml, articleUrl, 403);
assert.equal(article.wall, false, "article fixture must not be a challenge wall");
assert.ok(article.extracted?.markdown, "article fixture must extract markdown");
const md = article.extracted.markdown;
assert.match(md, /^# How closures capture variables/m, "extracted markdown keeps the heading");
assert.match(md, /^1\.\s+Declare the outer function/m, "extracted markdown keeps the list");
assert.match(md, /```[\s\S]*function makeAdder/, "extracted markdown keeps the code block");
assert.ok(md.length >= GATED_CONTENT_FLOOR, "extracted article stays above the content floor");
assert.equal(article.gated.kind, "ok");
assert.equal(article.gated.statusCode, 403);
assert.equal(article.gated.accessStatus, "blocked-but-content-available");

const challengeUrl = "https://example.test/blocked";
const challengeHtml = readFileSync(join(fixtures, "gated-403-challenge.html"), "utf8");
const challenge = decideFromHtml(challengeHtml, challengeUrl, 403);
assert.equal(challenge.wall, true, "challenge fixture must trip the wall probe");
assert.equal(challenge.gated.kind, "fail");
assert.equal(challenge.gated.failure, "captcha_or_challenge");
assert.equal(challenge.gated.statusCode, 403);

console.log("gated-nav-html-extract.selftest: OK");
