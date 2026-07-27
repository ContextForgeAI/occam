#!/usr/bin/env node
/**
 * Selftest for install-user-cli PATH/launcher helpers (no real User PATH writes).
 */
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  appendPathDir,
  prependPathDir,
  needsOperatorOverlay,
  pathHasDir,
  renderUnixLauncher,
  renderWindowsCmdLauncher,
  renderWindowsPs1Launcher,
  resolveUserBinDir,
  shellSingleQuote,
  splitPathEntries,
  writeUserLauncher,
  ensureWindowsUserPath,
} from "./install-user-cli.mjs";

function testPathHelpers() {
  const entries = splitPathEntries("C:\\a;C:\\b;;C:\\a", ";");
  assert.deepEqual(entries, ["C:\\a", "C:\\b", "C:\\a"]);
  assert.equal(pathHasDir(["C:\\a", "C:\\b"], "C:\\a", { caseInsensitive: true }), true);
  const once = appendPathDir(["C:\\a"], "C:\\b", { caseInsensitive: true });
  assert.equal(once.changed, true);
  assert.deepEqual(once.entries, ["C:\\a", "C:\\b"]);
  const twice = appendPathDir(once.entries, "C:\\b", { caseInsensitive: true });
  assert.equal(twice.changed, false);
  assert.deepEqual(twice.entries, once.entries);

  const pre = prependPathDir(["C:\\scripts"], "C:\\Users\\x\\.local\\bin", { caseInsensitive: true });
  assert.equal(pre.changed, true);
  assert.deepEqual(pre.entries, ["C:\\Users\\x\\.local\\bin", "C:\\scripts"]);
  const pre2 = prependPathDir(pre.entries, "C:\\Users\\x\\.local\\bin", { caseInsensitive: true });
  assert.equal(pre2.changed, false);
}

function testLaunchers() {
  const home = "C:\\Users\\Test User\\.local\\share\\ff-occam";
  const node = "C:\\Program Files\\nodejs\\node.exe";
  const cmd = renderWindowsCmdLauncher(home, node);
  assert.match(cmd, /set "OCCAM_HOME=C:\\Users\\Test User\\.local\\share\\ff-occam"/);
  assert.match(cmd, /set "OCCAM_NODE_BIN=C:\\Program Files\\nodejs\\node\.exe"/);
  assert.match(cmd, /"%OCCAM_NODE_BIN%" "%OCCAM_HOME%\\scripts\\occam\.mjs" %\*/);
  assert.doesNotMatch(cmd, /FF-Occam/);

  const ps1 = renderWindowsPs1Launcher(home, node);
  assert.match(ps1, /\$env:OCCAM_HOME = 'C:\\Users\\Test User\\.local\\share\\ff-occam'/);
  assert.match(ps1, /\$env:OCCAM_NODE_BIN = 'C:\\Program Files\\nodejs\\node\.exe'/);
  assert.match(ps1, /scripts\\occam\.mjs/);
  assert.match(ps1, /@args/);

  const sh = renderUnixLauncher("/opt/Occam Home/ff-occam", "/opt/homebrew/bin/node");
  assert.match(sh, /OCCAM_HOME='.*Occam Home.*ff-occam'/);
  assert.match(sh, /OCCAM_NODE_BIN='\/opt\/homebrew\/bin\/node'/);
  assert.match(sh, /occam-chat\.mjs/);
  assert.match(sh, /\[ "\$\{1:-\}" = "chat" \]/);
  assert.match(sh, /exec "\$OCCAM_NODE_BIN" "\$OCCAM_HOME\/scripts\/occam\.mjs"/);
  assert.equal(shellSingleQuote("a'b"), `'a'\\''b'`);
}

function testWriteLauncherIsolated() {
  const root = mkdtempSync(join(tmpdir(), "occam-cli-"));
  try {
    const home = join(root, "ff-occam");
    const bin = join(root, "bin");
    const win = writeUserLauncher(bin, home, { platform: "win32" });
    assert.equal(win.kind, "cmd+ps1");
    assert.ok(existsSync(win.launcherPath));
    assert.ok(existsSync(join(bin, "occam.cmd")));
    assert.ok(existsSync(join(bin, "occam.ps1")));
    const body = readFileSync(win.launcherPath, "utf8");
    assert.match(body, /occam\.mjs/);
    assert.match(readFileSync(join(bin, "occam.cmd"), "utf8"), /occam\.mjs/);

    const uni = writeUserLauncher(bin, home, { platform: "linux" });
    assert.equal(uni.kind, "shell");
    assert.ok(existsSync(uni.launcherPath));
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testNeedsOverlay() {
  const root = mkdtempSync(join(tmpdir(), "occam-ov-"));
  try {
    assert.equal(needsOperatorOverlay(root), true);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testEnsurePathDryRunAndMock() {
  const dry = ensureWindowsUserPath("C:\\Users\\x\\.local\\bin", { dryRun: true });
  assert.equal(dry.changed, true);
  assert.equal(dry.dryRun, true);

  let calls = 0;
  const mocked = ensureWindowsUserPath("C:\\Users\\x\\.local\\bin", {
    run: () => {
      calls += 1;
      return { status: 0, stdout: "UNCHANGED\n", stderr: "" };
    },
  });
  assert.equal(calls, 1);
  assert.equal(mocked.changed, false, "UNCHANGED must not match CHANGED substring");

  const changed = ensureWindowsUserPath("C:\\Users\\x\\.local\\bin", {
    run: () => ({ status: 0, stdout: "CHANGED\n", stderr: "" }),
  });
  assert.equal(changed.changed, true);

  // PS script body must prepend (not append) when writing User PATH.
  let captured = "";
  ensureWindowsUserPath("C:\\Users\\x\\.local\\bin", {
    run: (scriptText) => {
      captured = scriptText;
      return { status: 0, stdout: "CHANGED\n", stderr: "" };
    },
  });
  assert.match(captured, /\(@\(\$dir\) \+ \$parts\)/);
  assert.doesNotMatch(captured, /\(\$parts \+ \$dir\)/);
}

function testBinDir() {
  assert.ok(resolveUserBinDir("/home/me").replace(/\\/g, "/").endsWith("/.local/bin"));
}

function main() {
  testPathHelpers();
  testLaunchers();
  testWriteLauncherIsolated();
  testNeedsOverlay();
  testEnsurePathDryRunAndMock();
  testBinDir();
  console.log("install-user-cli.selftest: OK");
}

main();
