#!/usr/bin/env node
/**
 * Selftests for install-tree replace / lock handling (no live process kills required).
 */
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, existsSync } from "node:fs";
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

function main() {
  testInferAndMessage();
  testUnlockedTree();
  testMissingTreeOk();
  testIsFileLockedMissing();
  console.log("prepare-install-replace.selftest: OK");
}

main();
