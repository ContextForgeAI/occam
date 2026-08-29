#!/usr/bin/env node
import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import {
  evaluateOccamToolList,
  REQUIRED_BASELINE_TOOLS,
  waitForChildSpawn,
} from "./occam-verify.mjs";

const readerTools = REQUIRED_BASELINE_TOOLS.map((name) => ({ name }));

const reader = evaluateOccamToolList(readerTools);
assert.equal(reader.ok, true);
assert.equal(reader.toolCount, 8);
assert.deepEqual(reader.missingTools, []);
assert.deepEqual(reader.duplicateTools, []);

const expanded = evaluateOccamToolList([
  ...readerTools,
  { name: "occam_claim_check" },
  { name: "occam_attest" },
  { name: "occam_dataset_export" },
  { name: "occam_playbook_lint" },
  { name: "occam_playbook_resolve" },
  { name: "occam_playbook_heal" },
  { name: "occam_playbook_save" },
]);
assert.equal(expanded.ok, true);
assert.equal(expanded.toolCount, 15);
assert.deepEqual(expanded.duplicateTools, []);

const missingIdentity = evaluateOccamToolList([
  ...readerTools.slice(0, -1),
  { name: "occam_not_a_real_tool" },
]);
assert.equal(missingIdentity.ok, false);
assert.deepEqual(missingIdentity.missingTools, ["occam_verify"]);

const duplicateIdentity = evaluateOccamToolList([
  ...readerTools,
  { name: "occam_transcode" },
]);
assert.equal(duplicateIdentity.ok, false);
assert.deepEqual(duplicateIdentity.missingTools, []);
assert.deepEqual(duplicateIdentity.duplicateTools, ["occam_transcode"]);

const failedSpawn = new EventEmitter();
const failedSpawnResult = waitForChildSpawn(failedSpawn);
queueMicrotask(() => failedSpawn.emit("error", new Error("spawn ENOENT")));
await assert.rejects(failedSpawnResult, /ENOENT/);
assert.equal(failedSpawn.listenerCount("spawn"), 0);
assert.equal(failedSpawn.listenerCount("error"), 0);

const successfulSpawn = new EventEmitter();
const successfulSpawnResult = waitForChildSpawn(successfulSpawn);
queueMicrotask(() => successfulSpawn.emit("spawn"));
await successfulSpawnResult;
assert.equal(successfulSpawn.listenerCount("spawn"), 0);
assert.equal(successfulSpawn.listenerCount("error"), 0);

console.log("OCCAM_VERIFY_SELFTEST_OK");
