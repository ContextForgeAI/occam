#!/usr/bin/env node
/** Public release RID matrix and fail-before-network regression. */
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import {
  PUBLISHED_RELEASE_RIDS,
  isPublishedReleaseRid,
  resolvePublishedRid,
  resolveRid,
} from "./resolve-rid.mjs";
import { detectReleaseRid } from "./operator/update-check.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const shPath = join(repoRoot, "scripts", "get-ff-occam.sh");
const psPath = join(repoRoot, "scripts", "get-ff-occam.ps1");

function testSharedMatrix() {
  assert.deepEqual(PUBLISHED_RELEASE_RIDS, ["win-x64", "linux-x64", "osx-arm64"]);
  assert.equal(resolvePublishedRid("win32", "x64"), "win-x64");
  assert.equal(resolvePublishedRid("linux", "x64"), "linux-x64");
  assert.equal(resolvePublishedRid("darwin", "arm64"), "osx-arm64");
  for (const [platform, arch] of [
    ["win32", "arm64"],
    ["linux", "arm64"],
    ["darwin", "x64"],
    ["freebsd", "x64"],
  ]) {
    assert.throws(() => resolvePublishedRid(platform, arch), /unsupported public release platform/);
  }
  for (const rid of PUBLISHED_RELEASE_RIDS) assert.equal(isPublishedReleaseRid(rid), true);
  for (const rid of ["win-arm64", "linux-arm64", "osx-x64", "junk"]) {
    assert.equal(isPublishedReleaseRid(rid), false);
    assert.throws(() => detectReleaseRid("linux", "x64", rid), /unsupported OCCAM_RID/);
  }
  assert.equal(detectReleaseRid("linux", "x64", ""), "linux-x64");
  assert.equal(detectReleaseRid("darwin", "arm64", ""), "osx-arm64");

  // Contributor/source resolution remains backward compatible.
  assert.equal(resolveRid("darwin", "x64"), "osx-x64");
  assert.equal(resolveRid("linux", "arm64"), "linux-arm64");
  assert.equal(resolveRid("win32", "arm64"), "win-arm64");
}

function testSourceOrdering() {
  const sh = readFileSync(shPath, "utf8");
  const ps = readFileSync(psPath, "utf8");
  assert.ok(sh.indexOf('assert_published_rid "$RID"') < sh.indexOf('download "$MANIFEST_URL"'));
  assert.ok(ps.indexOf("Assert-PublishedRid $Rid") < ps.indexOf("Download-File $ManifestUrl"));
  for (const file of [
    "scripts/lib/build-release.mjs",
    "scripts/lib/verify-release-artifact.mjs",
    "scripts/lib/verify-release-manifest.mjs",
    "scripts/build-release.sh",
    "scripts/build-release-all.sh",
    "scripts/ci-release-build.sh",
  ]) {
    assert.doesNotMatch(readFileSync(join(repoRoot, file), "utf8"), /osx-x64/);
  }
}

function testUnsupportedOverrideFailsBeforeNetwork() {
  const script = process.platform === "win32" ? psPath : shPath;
  const args =
    process.platform === "win32"
      ? ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script]
      : [script];
  const command = process.platform === "win32" ? "powershell.exe" : "bash";
  const result = spawnSync(command, args, {
    cwd: repoRoot,
    encoding: "utf8",
    env: {
      ...process.env,
      OCCAM_RID: "osx-x64",
      OCCAM_RELEASE_ALLOW_HTTP: "1",
      OCCAM_RELEASE_MANIFEST_URL: "http://127.0.0.1:9/must-not-download.json",
      OCCAM_RELEASE_URL: "http://127.0.0.1:9/must-not-download.tar.gz",
    },
    timeout: 15_000,
  });
  const output = `${result.stdout || ""}\n${result.stderr || ""}`;
  assert.notEqual(result.status, 0);
  assert.match(output, /unsupported OCCAM_RID: osx-x64/);
  assert.doesNotMatch(output, /download failed|127\.0\.0\.1:9/);
}

function testShellDetectorMatrix() {
  if (process.platform === "win32") return;
  const source = readFileSync(shPath, "utf8");
  const marker = source.indexOf('\nVERSION="${OCCAM_VERSION');
  assert.ok(marker > 0);
  const root = mkdtempSync(join(tmpdir(), "occam-rid-sh-"));
  const runner = join(root, "rid.sh");
  writeFileSync(
    runner,
    `${source.slice(0, marker)}\nrid="$(detect_rid "$1" "$2")"\nprintf '%s\\n' "$rid"\n`,
  );
  try {
    for (const [os, arch, expected] of [
      ["Linux", "x86_64", "linux-x64"],
      ["Darwin", "arm64", "osx-arm64"],
      ["MINGW64_NT", "x86_64", "win-x64"],
    ]) {
      const result = spawnSync("bash", [runner, os, arch], { encoding: "utf8" });
      assert.equal(result.status, 0, result.stderr);
      assert.equal(result.stdout.trim(), expected);
    }
    for (const [os, arch] of [
      ["Linux", "aarch64"],
      ["Darwin", "x86_64"],
      ["MINGW64_NT", "arm64"],
    ]) {
      const result = spawnSync("bash", [runner, os, arch], { encoding: "utf8" });
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /no public Occam release/);
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testPowerShellDetectorMatrix() {
  if (process.platform !== "win32") return;
  const source = readFileSync(psPath, "utf8");
  const marker = source.search(/\r?\nResolve-SetupMode\r?\n/);
  assert.ok(marker > 0);
  const root = mkdtempSync(join(tmpdir(), "occam-rid-ps-"));
  const runner = join(root, "rid.ps1");
  writeFileSync(
    runner,
    `${source.slice(0, marker)}\nif ((Resolve-PublishedRid -Os Windows_NT -Architecture AMD64) -ne 'win-x64') { throw 'x64 mismatch' }\ntry { Resolve-PublishedRid -Os Windows_NT -Architecture ARM64; throw 'ARM64 accepted' } catch { if ($_.Exception.Message -eq 'ARM64 accepted') { throw } }\nWrite-Output 'RID_MATRIX_OK'\n`,
  );
  try {
    const result = spawnSync(
      "powershell.exe",
      ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", runner],
      { encoding: "utf8", env: { ...process.env, OCCAM_RID: "win-x64" } },
    );
    assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
    assert.match(result.stdout, /RID_MATRIX_OK/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

testSharedMatrix();
testSourceOrdering();
testUnsupportedOverrideFailsBeforeNetwork();
testShellDetectorMatrix();
testPowerShellDetectorMatrix();
console.log("bootstrap-rid.selftest: OK");
