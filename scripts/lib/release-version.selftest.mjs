#!/usr/bin/env node

import assert from "node:assert/strict";
import { parseReleaseTagRef, parseSemanticVersion } from "./release-version.mjs";

const stable = parseReleaseTagRef("refs/tags/v1.1.0");
assert.equal(stable.version, "1.1.0");
assert.equal(stable.assemblyVersion, "1.1.0.0");
assert.equal(stable.prerelease, false);

const requiredPrereleases = [
  "1.1.0-rc.1",
  "1.1.0-preview.2",
  "1.1.0-dev.1",
  "1.1.0-rc1",
  "1.1.0-custom.7",
  "1.1.0-alpha.1",
  "1.1.0-beta.2",
];
for (const version of requiredPrereleases) {
  const parsed = parseSemanticVersion(version);
  assert.equal(parsed.prerelease, true, `${version} must be a prerelease`);
  assert.equal(parsed.version, version);
  assert.equal(parsed.assemblyVersion, "1.1.0.0");
}

assert.equal(parseReleaseTagRef("refs/tags/v1.1.0-rc.1").prerelease, true);
assert.equal(parseReleaseTagRef("refs/tags/v1.1.0-custom.7").prerelease, true);

assert.throws(() => parseReleaseTagRef("refs/tags/baseline-2026-07-22"));
assert.throws(() => parseReleaseTagRef("refs/tags/v1.1"));
assert.throws(() => parseSemanticVersion("1.1.0-rc.01"));

for (const version of ["1.1.0+build.5", "1.1.0-rc.1+build.5"]) {
  assert.throws(
    () => parseSemanticVersion(version),
    /build metadata/,
    `${version} must be rejected`,
  );
  assert.throws(
    () => parseReleaseTagRef(`refs/tags/v${version}`),
    /build metadata/,
    `refs/tags/v${version} must be rejected`,
  );
}

console.log("release-version.selftest: OK");
