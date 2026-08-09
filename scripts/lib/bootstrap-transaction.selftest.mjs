#!/usr/bin/env node
/**
 * Inject a failure after the release-tree swap. The prior tree must be restored,
 * the generated backup removed, and an unrelated sibling left untouched.
 */
import assert from "node:assert/strict";
import {
  chmodSync,
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const shPath = join(repoRoot, "scripts", "get-ff-occam.sh");
const psPath = join(repoRoot, "scripts", "get-ff-occam.ps1");

function assertRestored(root, target) {
  assert.equal(readFileSync(join(target, "old-marker.txt"), "utf8"), "old\n");
  assert.equal(existsSync(join(target, "new-marker.txt")), false, "new tree survived rollback");
  assert.equal(readFileSync(join(root, "unrelated.txt"), "utf8"), "keep\n");
  assert.deepEqual(
    readdirSync(root).filter((name) => name.startsWith("install.pre-replace-")),
    [],
    "transaction backup was not consumed",
  );
}

function writeFixture(root) {
  const target = join(root, "install");
  const staged = join(root, "staged");
  mkdirSync(join(staged, "scripts", "lib"), { recursive: true });
  mkdirSync(target, { recursive: true });
  writeFileSync(join(target, "old-marker.txt"), "old\n");
  writeFileSync(join(staged, "new-marker.txt"), "new\n");
  writeFileSync(join(staged, "scripts", "lib", "prepare-install-replace.mjs"), "// fixture\n");
  writeFileSync(join(root, "unrelated.txt"), "keep\n");
  return { target, staged };
}

function testBashRollback() {
  const root = mkdtempSync(join(tmpdir(), "occam-bootstrap-rollback-sh-"));
  const { target, staged } = writeFixture(root);
  const fakeBin = join(root, "fake-bin");
  const fakeNode = join(fakeBin, "node");
  const runner = join(root, "runner.sh");
  mkdirSync(fakeBin);
  writeFileSync(
    fakeNode,
    `#!/usr/bin/env bash\nif [[ "\${1:-}" == "-e" ]]; then exec "$REAL_NODE" "$@"; fi\nprintf '{"ok":true}\\n'\n`,
  );
  chmodSync(fakeNode, 0o755);

  const source = readFileSync(shPath, "utf8");
  const marker = source.lastIndexOf('\nmain "$@"');
  assert.ok(marker > 0, "bash bootstrap main marker not found");
  const head = source.slice(0, marker);
  writeFileSync(
    runner,
    `${head}\nRID=linux-x64\nINSTALL_DIR=${shellQuote(target)}\ntrap bootstrap_on_exit EXIT\nreplace_install_tree ${shellQuote(target)} ${shellQuote(staged)}\necho injected-post-install-failure >&2\nfalse\n`,
  );
  chmodSync(runner, 0o755);

  try {
    const result = spawnSync("bash", [runner], {
      cwd: repoRoot,
      encoding: "utf8",
      env: {
        ...process.env,
        OCCAM_RID: "linux-x64",
        PATH: `${fakeBin}:${process.env.PATH || ""}`,
        REAL_NODE: process.execPath,
      },
      timeout: 30_000,
    });
    const output = `${result.stdout || ""}\n${result.stderr || ""}`;
    assert.notEqual(result.status, 0, `injected failure unexpectedly succeeded:\n${output}`);
    assert.match(output, /previous Occam install was restored/i);
    assertRestored(root, target);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testPowerShellRollback() {
  const root = mkdtempSync(join(tmpdir(), "occam-bootstrap-rollback-ps-"));
  const { target, staged } = writeFixture(root);
  const fakeBin = join(root, "fake-bin");
  const fakeNode = join(fakeBin, "node.cmd");
  const runner = join(root, "runner.ps1");
  mkdirSync(fakeBin);
  writeFileSync(fakeNode, '@echo off\r\necho {"ok":true}\r\nexit /b 0\r\n');

  const source = readFileSync(psPath, "utf8");
  const marker = source.search(/\r?\nResolve-SetupMode\r?\n/);
  assert.ok(marker > 0, "PowerShell bootstrap top-level marker not found");
  const head = source.slice(0, marker);
  writeFileSync(
    runner,
    `${head}\n$Rid = 'win-x64'\n$env:PATH = '${psQuote(fakeBin)};' + $env:PATH\n$homeRejected = $false\ntry { Assert-SafeInstallPath $env:USERPROFILE 'install' } catch { $homeRejected = $true }\nif (-not $homeRejected) { throw 'user profile accepted as install path' }\ntry {\n  Replace-OccamInstallTree -TargetDir '${psQuote(target)}' -StagedDir '${psQuote(staged)}'\n  Set-Location -LiteralPath '${psQuote(target)}'\n  throw 'injected-post-install-failure'\n} catch {\n  if (-not (Restore-OccamInstallTransaction)) { throw 'rollback failed' }\n  Write-Error 'injected-post-install-failure'\n  exit 77\n}\n`,
  );

  try {
    const result = spawnSync(
      "powershell.exe",
      ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", runner],
      {
        cwd: repoRoot,
        encoding: "utf8",
        env: { ...process.env, OCCAM_RID: "win-x64" },
        timeout: 30_000,
      },
    );
    const output = `${result.stdout || ""}\n${result.stderr || ""}`;
    assert.notEqual(result.status, 0, `injected failure unexpectedly succeeded:\n${output}`);
    assert.match(output, /previous Occam install was restored/i);
    assertRestored(root, target);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function shellQuote(value) {
  return `'${String(value).replace(/'/g, `'\\''`)}'`;
}

function psQuote(value) {
  return String(value).replace(/'/g, "''");
}

function testSourceGuards() {
  const sh = readFileSync(shPath, "utf8");
  const ps = readFileSync(psPath, "utf8");
  assert.ok(sh.lastIndexOf("commit_install_transaction") > sh.lastIndexOf("run_post_install"));
  assert.ok(ps.lastIndexOf("Complete-OccamInstallTransaction") > ps.indexOf("& node @postArgs"));
  assert.match(sh, /Stopping processes started from the new install before rollback/);
  assert.match(ps, /Stop-NewOccamInstallForRollback/);
  assert.match(ps, /path resolves to the user profile/);
  const shPost = sh.slice(sh.indexOf("run_post_install()"), sh.indexOf("main()"));
  assert.ok(
    shPost.indexOf('install_occam_user_command "$INSTALL_DIR"') > shPost.indexOf('node "$post_ux"'),
    "bash launcher must be installed after modern post-install UX",
  );
  assert.ok(
    shPost.lastIndexOf('install_occam_user_command "$INSTALL_DIR"') > shPost.indexOf('node "$connect_js"'),
    "bash launcher must be installed after legacy Connect",
  );
  const psPost = ps.slice(ps.indexOf("if (Test-Path $postUx)"));
  assert.ok(
    psPost.indexOf("Install-OccamUserCommand $InstallDir") > psPost.indexOf("& node @postArgs"),
    "PowerShell launcher must be installed after modern post-install UX",
  );
  assert.ok(
    psPost.lastIndexOf("Install-OccamUserCommand $InstallDir") > psPost.indexOf("& node @connectArgs"),
    "PowerShell launcher must be installed after legacy Connect",
  );
}

testSourceGuards();
if (process.platform === "win32") {
  testPowerShellRollback();
  console.log("bootstrap-transaction.selftest: OK (PowerShell)");
} else {
  testBashRollback();
  console.log("bootstrap-transaction.selftest: OK (bash)");
}
