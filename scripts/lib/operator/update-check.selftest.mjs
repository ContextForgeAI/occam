import assert from "node:assert/strict";
import {
  compareVersions,
  readInstalledVersion,
  releaseBaseToApiUrl,
} from "./update-check.mjs";

assert.equal(compareVersions("0.8.13", "0.8.12"), 1);
assert.equal(compareVersions("0.8.12", "0.8.12"), 0);
assert.equal(compareVersions("0.8.11", "0.8.12"), -1);
assert.equal(compareVersions("v0.8.12", "0.8.12"), 0);

const api = releaseBaseToApiUrl(
  "http://example/releases/download/v0.8.12",
);
assert.equal(api, "http://example/releases");

const installed = readInstalledVersion(process.env.OCCAM_HOME || process.cwd());
assert.ok(typeof installed === "string" && installed.length > 0);

console.log("update-check.selftest: OK");
