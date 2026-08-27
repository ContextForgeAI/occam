#!/usr/bin/env node
/**
 * Regression: release bootstraps must use the install-user-cli helper bundled in
 * the verified archive. The helper's local dependency closure must stay valid.
 */
import assert from "node:assert/strict";
import {
  copyFileSync,
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { spawnSync } from "node:child_process";
import { RELEASE_RUNTIME_FILES, validateReleaseRoot } from "./install-user-cli.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "../../..");
const INSTALL_USER_CLI_ENTRY = "scripts/lib/operator/install-user-cli.mjs";

/**
 * Collect relative file imports (./ or ../) from an ESM module, recursively.
 * @param {string} absFile
 * @param {Set<string>} seenAbs
 * @returns {string[]} repo-relative posix paths
 */
function collectLocalImportClosure(absFile, seenAbs = new Set()) {
  const abs = resolve(absFile);
  if (seenAbs.has(abs)) return [];
  seenAbs.add(abs);
  const text = readFileSync(abs, "utf8");
  const re = /from\s+["'](\.\.?\/[^"']+)["']/g;
  /** @type {string[]} */
  const out = [];
  let m;
  while ((m = re.exec(text))) {
    let spec = m[1];
    if (!spec.endsWith(".mjs") && !spec.endsWith(".js")) spec += ".mjs";
    const childAbs = resolve(dirname(abs), spec);
    if (!existsSync(childAbs)) {
      throw new Error(`broken import ${spec} from ${abs}`);
    }
    const rel = childAbs.slice(repoRoot.length + 1).replace(/\\/g, "/");
    out.push(rel);
    out.push(...collectLocalImportClosure(childAbs, seenAbs));
  }
  return out;
}

function testRuntimeManifestCoversImportGraph() {
  const entryAbs = join(repoRoot, INSTALL_USER_CLI_ENTRY);
  assert.ok(existsSync(entryAbs), "install-user-cli.mjs must exist");
  const closure = collectLocalImportClosure(entryAbs);
  const required = new Set([INSTALL_USER_CLI_ENTRY, ...closure]);
  for (const rel of required) {
    assert.ok(
      RELEASE_RUNTIME_FILES.includes(rel),
      `RELEASE_RUNTIME_FILES missing transitive local import: ${rel}`,
    );
  }
  console.log(`ok: manifest covers ${required.size} module(s)`);
}

function testBootstrapsUseBundledHelpers() {
  const sh = readFileSync(join(repoRoot, "scripts/get-ff-occam.sh"), "utf8");
  const ps1 = readFileSync(join(repoRoot, "scripts/get-ff-occam.ps1"), "utf8");
  assert.match(sh, /INSTALL_CONTRACT=self-contained-v1/);
  assert.match(sh, /INSTALL_CONTRACT=legacy/);
  assert.match(sh, /\$home\/scripts\/lib\/operator\/install-user-cli\.mjs/);
  assert.match(sh, /--no-overlay/);
  assert.match(sh, /OCCAM_OVERLAY_BASE_URL/);
  assert.match(sh, /Self-contained install does not fall back to legacy overlay mode/);
  assert.match(ps1, /\$script:InstallContract = "self-contained-v1"/);
  assert.match(ps1, /\$script:InstallContract = "legacy"/);
  assert.match(ps1, /Join-Path \$OccamHome "scripts\\lib\\operator\\install-user-cli\.mjs"/);
  assert.match(ps1, /"--no-overlay"/);
  assert.match(ps1, /OCCAM_OVERLAY_BASE_URL/);
  assert.match(ps1, /self-contained does not fall back to legacy overlay/);
  assert.match(sh, /prepare_install_replace "\$INSTALL_DIR" "\$staged"/);
  assert.match(ps1, /Invoke-PrepareInstallReplace \$TargetDir \$StagedDir/);
  assert.match(sh, /--dir "\$dir" --rid "\$RID" --json/);
  assert.match(ps1, /--dir \$Dir --rid \$Rid --json/);
  assert.match(sh, /preflight_release_archive/);
  assert.match(ps1, /Assert-ReleaseArchivePreflight/);
  assert.match(sh, /signaturePolicy/);
  assert.match(ps1, /signaturePolicy/);
  assert.ok(
    sh.indexOf("preflight_release_archive") < sh.indexOf('tar -xzf "$tarball_path"'),
    "shell bootstrap must preflight before tar extract",
  );
  assert.ok(
    ps1.indexOf("Assert-ReleaseArchivePreflight") < ps1.indexOf("tar.exe -xzf $tarballPath"),
    "PowerShell bootstrap must preflight before tar extract",
  );
  const shCheck = sh.indexOf('node "$runtime_checker" --check-release-root');
  const shReplace = sh.indexOf('prepare_install_replace "$INSTALL_DIR" "$staged"');
  const psCheck = ps1.indexOf("& node $runtimeChecker --check-release-root");
  const psReplace = ps1.indexOf("Replace-OccamInstallTree -TargetDir $InstallDir -StagedDir $staged");
  assert.ok(shCheck >= 0 && shCheck < shReplace, "shell runtime check must precede install swap");
  assert.ok(psCheck >= 0 && psCheck < psReplace, "PowerShell runtime check must precede install swap");
  assert.match(sh, /OCCAM_VERSION:-\s*1\.0\.0-rc\.4|OCCAM_VERSION:-1\.0\.0-rc\.4/);
  assert.match(ps1, /1\.0\.0-rc\.4/);
  console.log("ok: sh/ps1 dual-contract bootstrap (legacy overlay + self-contained no-overlay)");
}

function testReleaseRootValidation() {
  const stage = mkdtempSync(join(tmpdir(), "occam-release-root-"));
  const version = "9.8.7";
  const rid = "win-x64";
  try {
    assert.equal(
      new Set(RELEASE_RUNTIME_FILES).size,
      RELEASE_RUNTIME_FILES.length,
      "release runtime file list must not contain duplicates",
    );
    for (const rel of RELEASE_RUNTIME_FILES) {
      const from = join(repoRoot, ...rel.split("/"));
      const to = join(stage, ...rel.split("/"));
      mkdirSync(dirname(to), { recursive: true });
      copyFileSync(from, to);
    }
    writeFileSync(join(stage, "VERSION"), "9.8.6\n");
    writeFileSync(
      join(stage, "release-manifest.json"),
      `${JSON.stringify({ version: "9.8.6", rid: "linux-x64", layout: "level-b" })}\n`,
    );

    const problems = validateReleaseRoot(stage, { version, rid });
    assert.ok(problems.some((problem) => problem === "missing OccamMcp.Core.exe"));
    assert.ok(problems.some((problem) => problem.startsWith("VERSION mismatch")));
    assert.ok(problems.some((problem) => problem.startsWith("inner manifest version mismatch")));
    assert.ok(problems.some((problem) => problem.startsWith("inner manifest RID mismatch")));

    writeFileSync(join(stage, "OccamMcp.Core.exe"), "fixture");
    writeFileSync(join(stage, "VERSION"), `${version}\n`);
    writeFileSync(
      join(stage, "release-manifest.json"),
      `${JSON.stringify({ version, rid, layout: "level-b" })}\n`,
    );
    assert.deepEqual(validateReleaseRoot(stage, { version, rid }), []);
    console.log("ok: release root rejects host/VERSION/inner-manifest mismatch before swap");
  } finally {
    rmSync(stage, { recursive: true, force: true });
  }
}

function testIsolatedHelperTreeExecutes() {
  const stage = mkdtempSync(join(tmpdir(), "occam-user-cli-stage-"));
  try {
    const entryAbs = join(repoRoot, INSTALL_USER_CLI_ENTRY);
    const closure = collectLocalImportClosure(entryAbs);
    for (const rel of new Set([INSTALL_USER_CLI_ENTRY, ...closure])) {
      const from = join(repoRoot, rel);
      const to = join(stage, rel);
      mkdirSync(dirname(to), { recursive: true });
      copyFileSync(from, to);
    }
    const entry = join(stage, INSTALL_USER_CLI_ENTRY);
    // Import graph must resolve from the staged tree (the exact public failure mode).
    const importProbe = spawnSync(
      process.execPath,
      [
        "-e",
        `import(${JSON.stringify(pathToFileURL(entry).href)}).then(() => console.log("IMPORT_OK")).catch((e) => { console.error(e); process.exit(1); })`,
      ],
      { encoding: "utf8", cwd: stage },
    );
    assert.equal(importProbe.status, 0, importProbe.stderr || importProbe.stdout);
    assert.equal(importProbe.stdout.trim(), "IMPORT_OK", "module import must not run install CLI");
    assert.equal(importProbe.stderr.trim(), "");

    // Helper CLI still answers --help without needing a real install home.
    // Must actually run main() — macOS /tmp vs /private/tmp used to make
    // resolve(argv[1]) !== import.meta.url and silently exit 0 with empty stdout.
    const help = spawnSync(process.execPath, [entry, "--help"], {
      encoding: "utf8",
      cwd: stage,
    });
    assert.equal(help.status, 0, help.stderr || help.stdout);
    assert.match(help.stdout + help.stderr, /install-user-cli/);
    assert.ok(
      (help.stdout + help.stderr).trim().length > 0,
      "direct CLI must print help (realpath isDirect guard)",
    );

    const src = readFileSync(entry, "utf8");
    assert.match(src, /realpathSync/, "install-user-cli must realpath isDirect (macOS /private)");
    assert.match(src, /isDirectCliInvocation/, "install-user-cli must export isDirectCliInvocation");
    console.log("ok: isolated helper tree imports + --help");
  } finally {
    rmSync(stage, { recursive: true, force: true });
  }
}

testRuntimeManifestCoversImportGraph();
testBootstrapsUseBundledHelpers();
testReleaseRootValidation();
testIsolatedHelperTreeExecutes();
console.log("release-runtime-files.selftest: OK");
