#!/usr/bin/env node
/**
 * Regression: install-user-cli must emit JSON when executed from a temp path
 * (get-ff-occam.sh downloads the helper via mktemp — macOS /tmp symlink class).
 * Shell wrappers must not JSON.parse empty stdout.
 */
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { copyFileSync, mkdtempSync, rmSync, symlinkSync, realpathSync, writeFileSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const helperSrc = join(here, "operator", "install-user-cli.mjs");

function runHelper(scriptPath, home) {
  return spawnSync(
    process.execPath,
    [scriptPath, "--home", home, "--no-overlay", "--json"],
    {
      encoding: "utf8",
      env: { ...process.env, CI: "1" },
      cwd: repoRoot,
    },
  );
}

function assertJsonStdout(r, label) {
  assert.equal(r.status, 0, `${label} status=${r.status} stderr=${r.stderr}`);
  assert.ok(String(r.stdout || "").trim().length > 0, `${label}: empty stdout`);
  const parsed = JSON.parse(String(r.stdout).trim());
  assert.equal(parsed.ok, true);
  assert.ok(parsed.binDir || parsed.pathForCurrentProcess);
  // Compact JSON — single line preferred for shell argv capture.
  assert.equal(String(r.stdout).trim().includes("\n"), false);
}

function testShellRejectsEmptyParse() {
  // Mirrors get-ff-occam.sh guard: never JSON.parse("") uncaught.
  const r = spawnSync(
    process.execPath,
    [
      "-e",
      `const raw=process.argv[1]||''; if(!raw.trim()) process.exit(2); JSON.parse(raw);`,
      "",
    ],
    { encoding: "utf8" },
  );
  assert.equal(r.status, 2);
}

function testGetFfOccamShHasGuard() {
  const sh = readFileSync(join(repoRoot, "scripts", "get-ff-occam.sh"), "utf8");
  assert.match(sh, /if \[\[ -n "\$json" \]\]/);
  assert.match(sh, /warning: occam launcher helper returned no JSON/);
  assert.doesNotMatch(
    sh,
    /bin_dir="\$\(node -e "const j=JSON\.parse\(process\.argv\[1\]\); process\.stdout\.write/,
  );
}

function main() {
  testShellRejectsEmptyParse();
  testGetFfOccamShHasGuard();

  const root = mkdtempSync(join(tmpdir(), "occam-sh-json-"));
  const home = join(root, "ff-occam");
  mkdirSync(join(home, "scripts"), { recursive: true });
  // Pretend connect exists so overlay is skipped.
  writeFileSync(join(home, "scripts", "occam-connect.mjs"), "// stub\n");
  try {
    const copied = join(root, "install-user-cli.mjs");
    copyFileSync(helperSrc, copied);
    assertJsonStdout(runHelper(copied, home), "direct copy");

    const linked = join(root, "linked-install-user-cli.mjs");
    try {
      symlinkSync(realpathSync(copied), linked);
      assertJsonStdout(runHelper(linked, home), "symlink path");
    } catch (err) {
      if (err && (err.code === "EPERM" || /symlink/i.test(String(err.message)))) {
        console.log("  symlink path skipped");
      } else {
        throw err;
      }
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }

  console.log("bootstrap-sh-json.selftest: OK");
}

main();
