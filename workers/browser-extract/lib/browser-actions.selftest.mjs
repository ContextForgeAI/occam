#!/usr/bin/env node
import assert from "node:assert/strict";
import {
  validateMcpActions,
  redactAction,
  actionPlanHash,
  MAX_MCP_ACTIONS,
} from "./browser-actions.mjs";

const bad = validateMcpActions([]);
assert.equal(bad.ok, false);

const tooMany = validateMcpActions(
  Array.from({ length: MAX_MCP_ACTIONS + 1 }, () => ({ do: "wait", ms: 50 })),
);
assert.equal(tooMany.ok, false);

const jsSmuggle = validateMcpActions([{ do: "wait", ms: 50, js: "1+1" }]);
assert.equal(jsSmuggle.ok, false);

const ok = validateMcpActions([
  { do: "type", selector: "input", text: "secret-password" },
  { do: "press", key: "Enter" },
  { do: "scroll", to: "bottom" },
]);
assert.equal(ok.ok, true);
assert.equal(ok.actions.length, 3);

const redacted = redactAction(ok.actions[0]);
assert.equal(redacted.text, "***");
assert.equal(redacted.text_len, "secret-password".length);

const h1 = actionPlanHash(ok.actions);
const h2 = actionPlanHash(ok.actions);
assert.equal(h1, h2);
assert.match(h1, /^[a-f0-9]{64}$/);

console.log("browser-actions.selftest: OK");
