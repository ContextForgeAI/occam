#!/usr/bin/env node
/**
 * Selftest for install-user-cli PATH/launcher helpers (no real User PATH writes).
 */
import assert from "node:assert/strict";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  renameSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  appendPathDir,
  prependPathDir,
  looksLikeOwnedUserLauncher,
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
  assert.equal(looksLikeOwnedUserLauncher("occam.cmd", cmd), true);
  assert.equal(looksLikeOwnedUserLauncher("occam.cmd", `${cmd}rem custom\r\n`), false);

  const ps1 = renderWindowsPs1Launcher(home, node);
  assert.match(ps1, /\$env:OCCAM_HOME = 'C:\\Users\\Test User\\.local\\share\\ff-occam'/);
  assert.match(ps1, /\$env:OCCAM_NODE_BIN = 'C:\\Program Files\\nodejs\\node\.exe'/);
  assert.match(ps1, /scripts\\occam\.mjs/);
  assert.match(ps1, /@args/);
  assert.equal(looksLikeOwnedUserLauncher("occam.ps1", ps1), true);

  const sh = renderUnixLauncher("/opt/Occam Home/ff-occam", "/opt/homebrew/bin/node");
  assert.match(sh, /OCCAM_HOME='.*Occam Home.*ff-occam'/);
  assert.match(sh, /OCCAM_NODE_BIN='\/opt\/homebrew\/bin\/node'/);
  assert.match(sh, /occam-chat\.mjs/);
  assert.match(sh, /\[ "\$\{1:-\}" = "chat" \]/);
  assert.match(sh, /exec "\$OCCAM_NODE_BIN" "\$OCCAM_HOME\/scripts\/occam\.mjs"/);
  assert.equal(looksLikeOwnedUserLauncher("occam", sh), true);
  assert.equal(
    looksLikeOwnedUserLauncher(
      "occam",
      renderUnixLauncher("/opt/Occam's Home/ff-occam", "/opt/node's/bin/node"),
    ),
    true,
  );
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

function assertNoLauncherTransactionDebris(binDir) {
  const debris = readdirSync(binDir).filter((name) => /\.(?:tmp|bak)-/.test(name));
  assert.deepEqual(debris, [], `launcher transaction debris: ${debris.join(", ")}`);
}

function testLauncherCollisionFailsClosed() {
  const root = mkdtempSync(join(tmpdir(), "occam-cli-collision-"));
  try {
    const home = join(root, "ff-occam");
    const bin = join(root, "bin");
    mkdirSync(bin, { recursive: true });
    const unrelated = "#!/usr/bin/env bash\necho unrelated\n";
    writeFileSync(join(bin, "occam"), unrelated, "utf8");
    assert.throws(
      () => writeUserLauncher(bin, home, { platform: "linux" }),
      /refusing to overwrite unrelated launcher/i,
    );
    assert.equal(readFileSync(join(bin, "occam"), "utf8"), unrelated);
    assert.equal(existsSync(join(home, "runtime", "node-bin")), false);
    assertNoLauncherTransactionDebris(bin);

    const winBin = join(root, "win-bin");
    mkdirSync(winBin, { recursive: true });
    const oldHome = "C:\\Users\\Test\\ff-occam";
    const node = "C:\\Program Files\\nodejs\\node.exe";
    const ownedCmd = renderWindowsCmdLauncher(oldHome, node);
    const unrelatedPs1 = "Write-Output 'not Occam'\r\n";
    writeFileSync(join(winBin, "occam.cmd"), ownedCmd, "utf8");
    writeFileSync(join(winBin, "occam.ps1"), unrelatedPs1, "utf8");
    assert.throws(
      () => writeUserLauncher(winBin, "C:\\Users\\Test\\new-occam", {
        platform: "win32",
        nodeBin: node,
      }),
      /refusing to overwrite unrelated launcher/i,
    );
    assert.equal(readFileSync(join(winBin, "occam.cmd"), "utf8"), ownedCmd);
    assert.equal(readFileSync(join(winBin, "occam.ps1"), "utf8"), unrelatedPs1);
    assertNoLauncherTransactionDebris(winBin);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testLauncherIdempotencyAndPreviousRelease() {
  const root = mkdtempSync(join(tmpdir(), "occam-cli-idempotent-"));
  try {
    const bin = join(root, "bin");
    const firstHome = join(root, "first", "ff-occam");
    const secondHome = join(root, "second", "ff-occam");
    const first = writeUserLauncher(bin, firstHome, { platform: "linux" });
    assert.equal(first.changed, true);
    const firstBody = readFileSync(join(bin, "occam"), "utf8");
    assert.equal(looksLikeOwnedUserLauncher("occam", firstBody), true);

    const unchanged = writeUserLauncher(bin, firstHome, { platform: "linux" });
    assert.equal(unchanged.changed, false);
    assert.equal(readFileSync(join(bin, "occam"), "utf8"), firstBody);

    const previousReleaseBody = firstBody.replace(
      "# Auto-generated by Occam install - do not edit.\n",
      "",
    );
    writeFileSync(join(bin, "occam"), previousReleaseBody, "utf8");
    assert.equal(looksLikeOwnedUserLauncher("occam", previousReleaseBody), true);
    const updated = writeUserLauncher(bin, secondHome, { platform: "linux" });
    assert.equal(updated.changed, true);
    assert.match(readFileSync(join(bin, "occam"), "utf8"), /second\/ff-occam/);
    assertNoLauncherTransactionDebris(bin);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testWindowsLauncherRollback() {
  const root = mkdtempSync(join(tmpdir(), "occam-cli-rollback-"));
  try {
    const bin = join(root, "bin");
    const firstHome = "C:\\Users\\Test\\old-occam";
    const nextHome = join(root, "next-occam");
    const node = "C:\\Program Files\\nodejs\\node.exe";
    writeUserLauncher(bin, firstHome, { platform: "win32", nodeBin: node });
    const cmdPath = join(bin, "occam.cmd");
    const ps1Path = join(bin, "occam.ps1");
    const originalCmd = readFileSync(cmdPath, "utf8");
    const originalPs1 = readFileSync(ps1Path, "utf8");
    let injected = false;
    const failingRename = (source, destination) => {
      if (!injected && source.includes(".tmp-") && destination.endsWith("occam.ps1")) {
        injected = true;
        throw new Error("injected second-launcher failure");
      }
      renameSync(source, destination);
    };
    assert.throws(
      () =>
        writeUserLauncher(bin, nextHome, {
          platform: "win32",
          nodeBin: node,
          fsOps: { renameSync: failingRename },
        }),
      /failed to install Occam launcher transaction: injected second-launcher failure/,
    );
    assert.equal(injected, true);
    assert.equal(readFileSync(cmdPath, "utf8"), originalCmd);
    assert.equal(readFileSync(ps1Path, "utf8"), originalPs1);
    assert.equal(existsSync(join(nextHome, "runtime", "node-bin")), false);
    assertNoLauncherTransactionDebris(bin);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testNeedsOverlay() {
  const root = mkdtempSync(join(tmpdir(), "occam-ov-"));
  try {
    assert.equal(needsOperatorOverlay(root), true);
    // Partial overlay: connect present but tty.mjs missing → still needs overlay.
    mkdirSync(join(root, "scripts", "lib", "operator"), { recursive: true });
    writeFileSync(join(root, "scripts", "occam-connect.mjs"), "// stub\n", "utf8");
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
  testLauncherCollisionFailsClosed();
  testLauncherIdempotencyAndPreviousRelease();
  testWindowsLauncherRollback();
  testNeedsOverlay();
  testEnsurePathDryRunAndMock();
  testBinDir();
  console.log("install-user-cli.selftest: OK");
}

main();
