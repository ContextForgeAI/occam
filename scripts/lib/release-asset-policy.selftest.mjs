#!/usr/bin/env node

import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  RELEASE_RIDS,
  assertReleaseTagAbsent,
  expectedReleaseAssetNames,
  verifyReleaseDirectory,
  verifyReleaseList,
  verifyReleaseRecord,
} from "./release-asset-policy.mjs";

const version = "1.0.0-rc.3";
const tag = `v${version}`;
const root = fs.mkdtempSync(path.join(os.tmpdir(), "occam-release-assets-"));

try {
  for (const rid of RELEASE_RIDS) {
    const stem = `ff-occam-${version}-${rid}`;
    const archive = `${stem}.tar.gz`;
    const bytes = Buffer.from(`archive:${rid}`);
    fs.writeFileSync(path.join(root, archive), bytes);
    fs.writeFileSync(
      path.join(root, `${stem}-manifest.json`),
      `${JSON.stringify(
        {
          version,
          rid,
          tarball: archive,
          sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
          runtimeLayout: "self-contained-v1",
          signaturePolicy: "required-cosign-v1",
        },
        null,
        2,
      )}\n`,
    );
  }

  assert.equal(expectedReleaseAssetNames(version, false).length, 6);
  assert.equal(expectedReleaseAssetNames(version, true).length, 9);
  verifyReleaseDirectory({ directory: root, version, signed: false });

  for (const rid of RELEASE_RIDS) {
    fs.writeFileSync(path.join(root, `ff-occam-${version}-${rid}.tar.gz.bundle`), `bundle:${rid}`);
  }
  verifyReleaseDirectory({ directory: root, version, signed: true });

  fs.writeFileSync(path.join(root, "unexpected.txt"), "nope");
  assert.throws(
    () => verifyReleaseDirectory({ directory: root, version, signed: true }),
    /unexpected: unexpected\.txt/,
  );
  fs.rmSync(path.join(root, "unexpected.txt"));

  const record = {
    id: 42,
    tag_name: tag,
    name: tag,
    draft: true,
    prerelease: true,
    assets: expectedReleaseAssetNames(version, true).map((name) => ({ name })),
  };
  assert.equal(
    verifyReleaseRecord({ record, tag, version, draft: true, prerelease: true }),
    42,
  );
  assert.equal(
    verifyReleaseList({ releases: [[record]], tag, version, draft: true, prerelease: true }),
    42,
  );
  assert.throws(
    () => assertReleaseTagAbsent([[record]], tag),
    /already exists and will not be mutated/,
  );
  assert.doesNotThrow(() => assertReleaseTagAbsent([[record]], "v1.0.0-rc.4"));
  assert.throws(
    () =>
      verifyReleaseRecord({
        record: { ...record, assets: record.assets.slice(0, -1) },
        tag,
        version,
        draft: true,
        prerelease: true,
      }),
    /missing:/,
  );
  assert.throws(
    () =>
      verifyReleaseRecord({
        record: { ...record, assets: [...record.assets, { name: "extra" }] },
        tag,
        version,
        draft: true,
        prerelease: true,
      }),
    /unexpected: extra/,
  );
  console.log("release-asset-policy.selftest: OK");
} finally {
  fs.rmSync(root, { recursive: true, force: true });
}
