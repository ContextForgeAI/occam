import assert from "node:assert/strict";
import { stampNodeRuntimeEnv } from "./stamp-node-runtime-env.mjs";

const sep = process.platform === "win32" ? ";" : ":";

function testStampSetsNodeBinAndPrependsPath() {
  const env = { PATH: `/usr/bin${sep}/bin` };
  stampNodeRuntimeEnv(env, "/opt/homebrew/bin/node");
  assert.equal(env.OCCAM_NODE_BIN, "/opt/homebrew/bin/node");
  assert.equal(env.PATH, `/opt/homebrew/bin${sep}/usr/bin${sep}/bin`);
}

function testStampDoesNotOverrideNodeBin() {
  const env = { PATH: `/usr/bin${sep}/bin`, OCCAM_NODE_BIN: "/custom/node" };
  stampNodeRuntimeEnv(env, "/opt/homebrew/bin/node");
  assert.equal(env.OCCAM_NODE_BIN, "/custom/node");
  assert.equal(env.PATH, `/opt/homebrew/bin${sep}/usr/bin${sep}/bin`);
}

function testStampIdempotentPath() {
  const env = { PATH: `/opt/homebrew/bin${sep}/usr/bin${sep}/bin` };
  stampNodeRuntimeEnv(env, "/opt/homebrew/bin/node");
  assert.equal(env.PATH, `/opt/homebrew/bin${sep}/usr/bin${sep}/bin`);
}

function testStampEmptyPath() {
  const env = {};
  stampNodeRuntimeEnv(env, "/usr/local/bin/node");
  assert.equal(env.OCCAM_NODE_BIN, "/usr/local/bin/node");
  assert.equal(env.PATH, "/usr/local/bin");
}

testStampSetsNodeBinAndPrependsPath();
testStampDoesNotOverrideNodeBin();
testStampIdempotentPath();
testStampEmptyPath();
console.log("stamp-node-runtime-env.selftest: ok");
