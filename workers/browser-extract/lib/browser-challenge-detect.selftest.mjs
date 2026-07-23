import assert from "node:assert";
import { isChallengeWall } from "./browser-session.mjs";

// Q-019: fail-fast anti-bot wall detection is a pure decision over a lightweight DOM probe.
// A wall = challenge marker (node or phrase) AND near-zero readable content.

// Cloudflare "Just a moment" interstitial: challenge node + empty body → wall.
assert.strictEqual(
  isChallengeWall({ title: "Just a moment...", textLen: 12, sampleLower: "", hasChallengeNode: true }),
  true,
  "cloudflare interstitial with challenge node must be a wall");

// Phrase-only wall (no recognisable node), tiny content.
assert.strictEqual(
  isChallengeWall({ title: "Attention Required! | Cloudflare", textLen: 30, sampleLower: "checking your browser before accessing", hasChallengeNode: false }),
  true,
  "phrase hit with tiny content must be a wall");

// Real content page that merely embeds a turnstile widget in a form (lots of prose) → NOT a wall.
assert.strictEqual(
  isChallengeWall({ title: "Sign up | Example", textLen: 4200, sampleLower: "welcome to example, the platform for…", hasChallengeNode: true }),
  false,
  "a real page with a captcha widget but real content must NOT be short-circuited");

// Normal article, no markers → NOT a wall.
assert.strictEqual(
  isChallengeWall({ title: "How to bake bread", textLen: 8000, sampleLower: "bread has been a staple…", hasChallengeNode: false }),
  false,
  "normal content page is not a wall");

// Marker present but content is right at/above the floor → NOT a wall (avoid false positives).
assert.strictEqual(
  isChallengeWall({ title: "Just a moment", textLen: 200, sampleLower: "", hasChallengeNode: true }),
  false,
  "content at the 200-char floor is not treated as a wall");

// Defensive: null/empty probe (page.evaluate failed) → NOT a wall (fail open to normal extract).
assert.strictEqual(isChallengeWall(null), false, "null probe must not be a wall");
assert.strictEqual(isChallengeWall({}), false, "empty probe must not be a wall");

console.log("browser-challenge-detect.selftest: OK");
