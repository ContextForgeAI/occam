#!/usr/bin/env node
/**
 * Selftests for install-tree replace / lock handling (no live process kills required).
 */
import assert from "node:assert/strict";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  inferHoldingApps,
  isFileLocked,
  isInstallHostLocked,
  installHostExePath,
  prepareInstallTreeReplace,
  renderInstallInUseMessage,
} from "./stop-occam-processes.mjs";
import { prepareOwnedInstallTreeReplace } from "./prepare-install-replace.mjs";
import { resolveRid } from "./resolve-rid.mjs";

function writeReleaseTree(root, overrides = {}) {
  const rid = overrides.rid || resolveRid();
  const version = overrides.version || "1.0.0-rc.2";
  mkdirSync(join(root, "scripts"), { recursive: true });
  writeFileSync(join(root, "VERSION"), `${overrides.versionFile || version}\n`);
  writeFileSync(
    join(root, "release-manifest.json"),
    `${JSON.stringify({
      version: overrides.manifestVersion || version,
      rid: overrides.manifestRid || rid,
      layout: overrides.layout || "level-b",
    })}\n`,
  );
  const hostName = overrides.legacyHost
    ? rid.startsWith("win-")
      ? "FFOccamMcp.Core.exe"
      : "FFOccamMcp.Core"
    : rid.startsWith("win-")
      ? "OccamMcp.Core.exe"
      : "OccamMcp.Core";
  writeFileSync(join(root, hostName), "host");
  writeFileSync(join(root, "scripts", "occam.mjs"), "// marker\n");
  writeFileSync(join(root, "scripts", "launch-mcp-host.mjs"), "// marker\n");
  return { rid, version };
}

function testInferAndMessage() {
  const apps = inferHoldingApps([
    {
      pid: 1,
      name: "node.exe",
      commandLine: "node C:\\Users\\x\\.local\\share\\ff-occam\\scripts\\launch-mcp-host.mjs",
      executablePath: "C:\\Users\\x\\AppData\\Local\\Programs\\cursor\\resources\\app\\resources\\helpers\\node.exe",
    },
  ]);
  assert.ok(apps.includes("Cursor"));
  const msg = renderInstallInUseMessage({ apps });
  assert.match(msg, /Occam is currently in use/);
  assert.match(msg, /Cursor/);
  assert.match(msg, /No files were changed/);
  assert.doesNotMatch(msg, /Remove-Item/);
}

function testUnlockedTree() {
  const root = mkdtempSync(join(tmpdir(), "occam-repl-"));
  try {
    writeFileSync(installHostExePath(root), "fake-exe");
    assert.equal(isInstallHostLocked(root), false);
    const r = prepareInstallTreeReplace(root, { dryRun: true });
    assert.equal(r.ok, true);
    assert.equal(r.locked, false);
    assert.ok(existsSync(root));
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testMissingTreeOk() {
  const root = join(tmpdir(), "occam-missing-" + Date.now());
  const r = prepareInstallTreeReplace(root);
  assert.equal(r.ok, true);
  assert.equal(existsSync(root), false);
}

function testIsFileLockedMissing() {
  assert.equal(isFileLocked(join(tmpdir(), "no-such-occam-exe-" + Date.now())), false);
}

function testReplacementOwnershipGate() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-replace-owner-"));
  const rid = resolveRid();
  let prepareCalls = 0;
  const prepare = () => {
    prepareCalls += 1;
    return { ok: true, stopped: [], locked: false };
  };
  const assertRefusedWithoutMutation = (target, label) => {
    const sentinel = join(target, "keep.txt");
    if (!existsSync(sentinel)) writeFileSync(sentinel, label);
    const before = prepareCalls;
    const result = prepareOwnedInstallTreeReplace(target, { rid, prepare });
    assert.equal(result.ok, false, `${label} must be refused`);
    assert.equal(result.failureKind, "unowned_install_target");
    assert.equal(prepareCalls, before, `${label} must not call process preparation`);
    assert.equal(existsSync(target), true, `${label} target must remain`);
    assert.equal(existsSync(sentinel), true, `${label} sentinel must remain`);
    return result;
  };

  try {
    const arbitrary = join(fixture, "arbitrary");
    mkdirSync(arbitrary);
    assertRefusedWithoutMutation(arbitrary, "arbitrary directory");

    const source = join(fixture, "source-checkout");
    writeReleaseTree(source, { rid });
    mkdirSync(join(source, ".git"));
    const sourceResult = assertRefusedWithoutMutation(source, "source checkout");
    assert.match(sourceResult.message, /source checkout/i);

    for (const [name, overrides, expected] of [
      ["version-file", { versionFile: "1.0.0-rc.1" }, /version/i],
      ["manifest-version", { manifestVersion: "1.0.0-rc.1" }, /version/i],
      ["manifest-rid", { manifestRid: rid === "win-x64" ? "linux-x64" : "win-x64" }, /rid/i],
      ["manifest-layout", { layout: "source" }, /layout/i],
    ]) {
      const target = join(fixture, name);
      writeReleaseTree(target, { rid, ...overrides });
      const result = assertRefusedWithoutMutation(target, name);
      assert.match(result.message, expected);
    }

    const real = join(fixture, "real-release");
    writeReleaseTree(real, { rid });
    const linked = join(fixture, "linked-release");
    symlinkSync(real, linked, process.platform === "win32" ? "junction" : "dir");
    const linkedResult = prepareOwnedInstallTreeReplace(linked, { rid, prepare });
    assert.equal(linkedResult.ok, false, "symlink/reparse target must be refused");
    assert.equal(prepareCalls, 0, "symlink/reparse target must not call process preparation");
    assert.equal(existsSync(real), true);
    assert.equal(existsSync(linked), true);

    const legacy = join(fixture, "legacy-level-b");
    writeReleaseTree(legacy, { rid, version: "0.9.1", legacyHost: true });
    const accepted = prepareOwnedInstallTreeReplace(legacy, { rid, prepare });
    assert.equal(accepted.ok, true, "legitimate older Level B release must be updatable");
    assert.equal(prepareCalls, 1);
    assert.equal(existsSync(legacy), true, "ownership gate itself never moves/deletes target");

    const emptyForeign = join(fixture, "empty-foreign");
    mkdirSync(emptyForeign);
    assertRefusedWithoutMutation(emptyForeign, "empty foreign directory");

    const fileTarget = join(fixture, "file-target");
    writeFileSync(fileTarget, "not-a-directory");
    const fileResult = prepareOwnedInstallTreeReplace(fileTarget, { rid, prepare });
    assert.equal(fileResult.ok, false, "file install root must be refused");
    assert.equal(fileResult.failureKind, "unowned_install_target");
    assert.equal(prepareCalls, 1, "file target must not call process preparation");
  } finally {
    rmSync(fixture, { recursive: true, force: true });
  }
}

function main() {
  testInferAndMessage();
  testUnlockedTree();
  testMissingTreeOk();
  testIsFileLockedMissing();
  testReplacementOwnershipGate();
  console.log("prepare-install-replace.selftest: OK");
}

main();
