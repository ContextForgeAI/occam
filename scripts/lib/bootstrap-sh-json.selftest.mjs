#!/usr/bin/env node
/**
 * Regression: install-user-cli must emit JSON when executed from a temp path
 * (get-ff-occam.sh downloads the helper via mktemp — macOS /tmp symlink class).
 * Shell wrappers must not JSON.parse empty stdout.
 *
 * Also: Bash 3.2 + set -u treats empty "${arr[@]}" as unbound. Legacy
 * run_post_install must not expand an empty optional-args array when calling
 * occam-connect (live macOS `curl | bash` after self-check).
 */
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import {
  copyFileSync,
  mkdtempSync,
  rmSync,
  symlinkSync,
  realpathSync,
  writeFileSync,
  mkdirSync,
  readFileSync,
  existsSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

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

/** Prefer real Git Bash over Windows System32 bash.exe (WSL launcher stub). */
function resolveBash() {
  const candidates = [
    process.env.OCCAM_TEST_BASH,
    "C:\\Program Files\\Git\\bin\\bash.exe",
    "C:\\Program Files\\Git\\usr\\bin\\bash.exe",
    join(process.env.LOCALAPPDATA || "", "Programs", "Git", "bin", "bash.exe"),
    "/bin/bash",
    "/usr/bin/bash",
  ].filter(Boolean);
  for (const c of candidates) {
    if (existsSync(c)) return c;
  }
  const which = spawnSync(process.platform === "win32" ? "where" : "command", ["bash"], {
    encoding: "utf8",
    shell: true,
  });
  const line = String(which.stdout || "")
    .split(/\r?\n/)
    .map((s) => s.trim())
    .find((s) => s && !/system32\\bash\.exe$/i.test(s));
  return line || null;
}

function testBash32NounsetEmptyArrayContract(bash) {
  // Document the macOS Bash 3.2 failure mode. On Bash ≥4.4 empty arrays are OK
  // under set -u — still assert our bootstrap never uses the dangerous pattern.
  const sh = readFileSync(join(repoRoot, "scripts", "get-ff-occam.sh"), "utf8");
  assert.match(sh, /set -euo pipefail/);
  assert.doesNotMatch(sh, /local\s+carg=\(\)/);
  assert.doesNotMatch(sh, /"\$\{carg\[@\]\}"/);
  assert.match(sh, /Bash 3\.2 \+ set -u/);
  assert.match(sh, /node "\$connect_js" --verbose/);
  assert.match(sh, /node "\$connect_js"/);
  // /dev/tty consent plumbing must remain for curl|bash.
  assert.match(sh, /read -r choice < \/dev\/tty/);
  assert.match(sh, /\[\[ -r \/dev\/tty && -w \/dev\/tty \]\]/);

  if (!bash) {
    console.log("  bash not found — static nounset contract only");
    return;
  }

  // Fixed optional-args path under nounset (empty verbose branch).
  const fixed = spawnSync(
    bash,
    [
      "-c",
      [
        "set -euo pipefail",
        "VERBOSE=0",
        'connect_js="stub"',
        'if [[ "$VERBOSE" -eq 1 ]]; then',
        '  printf "node %s --verbose\\n" "$connect_js"',
        "else",
        '  printf "node %s\\n" "$connect_js"',
        "fi",
      ].join("\n"),
    ],
    { encoding: "utf8" },
  );
  assert.equal(fixed.status, 0, `fixed pattern failed: ${fixed.stderr}`);
  assert.equal(String(fixed.stdout).trim(), "node stub");

  // Reproduce empty-array unbound when the running bash is old enough.
  const oldPattern = spawnSync(
    bash,
    [
      "-c",
      [
        "set -euo pipefail",
        "f() {",
        "  local carg=()",
        '  printf "x%s\\n" "${carg[@]}"',
        "}",
        "f",
      ].join("\n"),
    ],
    { encoding: "utf8" },
  );
  const err = `${oldPattern.stderr || ""}${oldPattern.stdout || ""}`;
  if (/unbound variable/i.test(err) || oldPattern.status !== 0) {
    assert.match(err, /unbound variable|carg\[@\]/i);
    console.log("  bash reproduces empty-array nounset failure (macOS-class)");
  } else {
    console.log("  bash ≥4.4: empty array under set -u is allowed (static guard still applies)");
  }
}

/**
 * Streamed stdin contract (`curl | bash` class): after "Self-check passed",
 * legacy connect call must not throw carg[@] unbound; stub prints onboarding
 * boundary marker. Uses `cat | bash` so the HTTP event-loop is not blocked by
 * spawnSync (same stdin-stream semantics as curl|bash).
 */
function testStreamedLegacyConnectBoundary(bash) {
  if (!bash) {
    console.log("  streamed connect boundary skipped (no bash)");
    return;
  }

  const root = mkdtempSync(join(tmpdir(), "occam-sh-nounset-"));
  try {
    const stubConnect = join(root, "occam-connect.cjs");
    writeFileSync(
      stubConnect,
      [
        "#!/usr/bin/env node",
        "const fs = require('fs');",
        "process.stdout.write('Looking for AI apps...\\n');",
        "process.stdout.write('No AI app configurations have been changed yet.\\n');",
        "process.stdout.write('Connect Occam to all N apps? [y/N]\\n');",
        "try { const fd = fs.openSync('/dev/tty', 'r+'); fs.closeSync(fd); process.stdout.write('TTY_OPEN_OK\\n'); }",
        "catch (e) { process.stdout.write('TTY_OPEN_SKIP\\n'); }",
      ].join("\n"),
    );

    const streamedSh = join(root, "streamed-connect.sh");
    const stubPosix = stubConnect.replace(/\\/g, "/");
    const nodePosix = process.execPath.replace(/\\/g, "/");
    writeFileSync(
      streamedSh,
      [
        "#!/usr/bin/env bash",
        "set -euo pipefail",
        'echo "✓ Self-check passed"',
        `connect_js="${stubPosix}"`,
        "VERBOSE=0",
        "# Bash 3.2 + set -u: empty \"${arr[@]}\" is an unbound variable.",
        'if [[ "$VERBOSE" -eq 1 ]]; then',
        `  "${nodePosix}" "$connect_js" --verbose`,
        "else",
        `  "${nodePosix}" "$connect_js"`,
        "fi",
        "",
      ].join("\n"),
      { encoding: "utf8" },
    );

    const bashPosix = bash.replace(/\\/g, "/");
    const shPosix = streamedSh.replace(/\\/g, "/");
    const streamed = spawnSync(
      bash,
      ["-c", `cat "${shPosix}" | "${bashPosix}"`],
      {
        encoding: "utf8",
        env: { ...process.env, CI: "1" },
        cwd: root,
        timeout: 30_000,
      },
    );
    assert.equal(
      streamed.status,
      0,
      `streamed cat|bash failed status=${streamed.status}\nstdout=${streamed.stdout}\nstderr=${streamed.stderr}`,
    );
    const out = `${streamed.stdout || ""}${streamed.stderr || ""}`;
    assert.doesNotMatch(out, /unbound variable/i);
    assert.doesNotMatch(out, /carg\[@\]/);
    assert.match(out, /Self-check passed/);
    assert.match(out, /Looking for AI apps/);
    assert.match(out, /No AI app configurations have been changed yet/);
    assert.match(out, /Connect Occam to all N apps\? \[y\/N\]/);
    assert.match(out, /TTY_OPEN_(OK|SKIP)/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function main() {
  testShellRejectsEmptyParse();
  testGetFfOccamShHasGuard();

  const bash = resolveBash();
  testBash32NounsetEmptyArrayContract(bash);
  testStreamedLegacyConnectBoundary(bash);

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
