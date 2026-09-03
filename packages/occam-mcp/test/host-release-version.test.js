import test from "node:test";
import assert from "node:assert/strict";
import {
  HOST_RELEASE_VERSION,
  resolveHostReleaseVersion,
} from "../lib/host-release-version.mjs";

test("HOST_RELEASE_VERSION is pinned for npm wrapper patches", () => {
  assert.equal(HOST_RELEASE_VERSION, "1.0.0");
});

test("resolveHostReleaseVersion honors OCCAM_HOST_RELEASE_VERSION", () => {
  assert.equal(resolveHostReleaseVersion(""), HOST_RELEASE_VERSION);
  assert.equal(resolveHostReleaseVersion("1.0.1"), "1.0.1");
});
