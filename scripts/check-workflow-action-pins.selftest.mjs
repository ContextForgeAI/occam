#!/usr/bin/env node
import assert from "node:assert/strict";
import { validateUsesLine } from "./check-workflow-action-pins.mjs";

const commit = "a".repeat(40);
const digest = "b".repeat(64);

assert.deepEqual(validateUsesLine("      - uses: ./local-action"), {
  failures: [],
  externalActions: 0,
});
assert.deepEqual(validateUsesLine(`      - uses: actions/checkout@${commit} # v4.4.0`), {
  failures: [],
  externalActions: 1,
});
assert.deepEqual(validateUsesLine(`      - uses: docker://ghcr.io/example/tool@sha256:${digest}`), {
  failures: [],
  externalActions: 1,
});

const mutableAction = validateUsesLine("      - 'uses' : actions/checkout@v4", "mutable.yml", 7);
assert.equal(mutableAction.externalActions, 1);
assert.match(mutableAction.failures.join("\n"), /40-hex commit SHA/);

const undocumentedPin = validateUsesLine(`      uses: actions/checkout@${commit}`);
assert.match(undocumentedPin.failures.join("\n"), /version comment/);

const mutableContainer = validateUsesLine(
  "      uses: docker://ghcr.io/example/tool:latest",
  "container.yml",
  9,
);
assert.equal(mutableContainer.externalActions, 1);
assert.match(mutableContainer.failures.join("\n"), /immutable sha256 digest/);

const inlineAction = validateUsesLine("      - { uses: actions/checkout@v4 }", "inline.yml", 3);
assert.equal(inlineAction.externalActions, 1);
assert.match(inlineAction.failures.join("\n"), /inline uses syntax is not auditable/);

console.log("workflow-action-pins.selftest: OK");
