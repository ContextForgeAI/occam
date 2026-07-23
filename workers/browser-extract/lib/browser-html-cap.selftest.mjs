import assert from "node:assert";
import { isBrowserHtmlTooLarge } from "./browser-session.mjs";

assert.strictEqual(isBrowserHtmlTooLarge("x".repeat(900_000)), false, "the exact cap is accepted");
assert.strictEqual(isBrowserHtmlTooLarge("x".repeat(900_001)), true, "one char above the cap is rejected");

// Regression: a SPA can be small at the first snapshot and grow while the short-response retry
// settles. Both snapshots use the same exported predicate in renderAndExtract.
const snapshots = ["x".repeat(100), "x".repeat(900_001)];
assert.deepStrictEqual(
  snapshots.map(isBrowserHtmlTooLarge),
  [false, true],
  "post-settle DOM growth must trip the cap");

console.log("browser-html-cap.selftest: OK");
