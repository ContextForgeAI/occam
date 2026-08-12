#!/usr/bin/env node
import assert from "node:assert/strict";
import crypto from "node:crypto";
import {
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { verifyReleaseManifest } from "./verify-release-manifest.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const version = "1.2.3-rc.4";
const rids = ["linux-x64", "osx-arm64", "win-x64"];
const root = mkdtempSync(join(tmpdir(), "occam-release-manifest-"));

function sha256(content) {
  return crypto.createHash("sha256").update(content).digest("hex");
}

function fixturePaths(rid) {
  const base = `ff-occam-${version}-${rid}`;
  return {
    manifestPath: join(root, `${base}-manifest.json`),
    tarballName: `${base}.tar.gz`,
    tarballPath: join(root, `${base}.tar.gz`),
  };
}

function writeManifest(rid, overrides = {}) {
  const paths = fixturePaths(rid);
  const archive = Buffer.from(`archive fixture for ${rid}\n`, "utf8");
  writeFileSync(paths.tarballPath, archive);
  const manifest = {
    version,
    rid,
    tarball: paths.tarballName,
    sha256: sha256(archive),
    runtimeLayout: "self-contained-v1",
    ...overrides,
  };
  writeFileSync(paths.manifestPath, `${JSON.stringify(manifest)}\n`, "utf8");
  return paths;
}

try {
  for (const rid of rids) {
    writeManifest(rid);
    const result = verifyReleaseManifest({ version, rid, outputDir: root });
    assert.equal(result.manifest.version, version);
    assert.equal(result.manifest.rid, rid);
    console.log(`ok: valid ${rid} manifest/archive pair accepted`);
  }

  const rid = "linux-x64";
  for (const [label, overrides, expected] of [
    ["version", { version: "1.2.3-rc.3" }, /manifest version mismatch/],
    ["RID", { rid: "win-x64" }, /manifest RID mismatch/],
    ["tarball", { tarball: "wrong.tar.gz" }, /manifest tarball mismatch/],
    ["sha256 format", { sha256: "not-a-sha" }, /manifest sha256 must be 64 lowercase/],
  ]) {
    writeManifest(rid, overrides);
    assert.throws(() => verifyReleaseManifest({ version, rid, outputDir: root }), expected);
    console.log(`ok: ${label} mismatch rejected`);
  }

  const paths = writeManifest(rid);
  writeFileSync(paths.tarballPath, "tampered archive\n", "utf8");
  assert.throws(
    () => verifyReleaseManifest({ version, rid, outputDir: root }),
    /sha256 mismatch/,
  );
  console.log("ok: archive sha256 mismatch rejected");

  const workflow = readFileSync(join(repoRoot, ".github", "workflows", "occam-release.yml"), "utf8");
  const validationStep = workflow.indexOf("- name: Revalidate exact unsigned artifact set");
  const signingStep = workflow.indexOf("- name: Sign exactly three platform archives");
  assert.ok(validationStep >= 0, "release workflow must invoke manifest validation");
  assert.ok(signingStep > validationStep, "manifest validation must run before cosign signing");
  assert.match(workflow, /node scripts\/lib\/release-asset-policy\.mjs verify-directory/);
  assert.match(workflow, /--version "\$VERSION"/);
  assert.match(workflow, /--directory artifacts\/release/);
  assert.match(workflow, /--signed false/);
  console.log("ok: atomic release workflow validates the exact three RID pairs before cosign");

  console.log("verify-release-manifest.selftest: OK");
} finally {
  rmSync(root, { recursive: true, force: true });
}
