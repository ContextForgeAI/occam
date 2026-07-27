import assert from "node:assert/strict";
import { stampNodeRuntimeEnv } from "./stamp-node-runtime-env.mjs";

const sep = process.platform === "win32" ? ";" : ":";

function testStampSetsNodeBinAndPrependsPath() {
  const env = { PATH: `/usr/bin${sep}/bin` };
  stampNodeRuntimeEnv(env, process.execPath);
  assert.equal(env.OCCAM_NODE_BIN, process.execPath);
  const nodeDir = process.execPath.replace(/\\/g, "/").replace(/\/[^/]+$/, "");
  assert.ok(env.PATH.replace(/\\/g, "/").startsWith(nodeDir) || env.PATH.includes(nodeDir.replace(/\//g, "\\")));
}

function testStampDoesNotOverrideNodeBin() {
  const env = { PATH: `/usr/bin${sep}/bin`, OCCAM_NODE_BIN: "/custom/node" };
  stampNodeRuntimeEnv(env, process.execPath);
  assert.equal(env.OCCAM_NODE_BIN, "/custom/node");
}

function testStampEmptyPath() {
  const env = {};
  stampNodeRuntimeEnv(env, process.execPath);
  assert.equal(env.OCCAM_NODE_BIN, process.execPath);
}

testStampSetsNodeBinAndPrependsPath();
testStampDoesNotOverrideNodeBin();
testStampEmptyPath();
console.log("stamp-node-runtime-env.selftest: ok");
