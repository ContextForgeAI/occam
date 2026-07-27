#!/usr/bin/env node
/**
 * Regression: bootstrap temp staging for install-user-cli must include the FULL
 * local ESM dependency closure with preserved relative paths.
 *
 * Catches the public Mac failure:
 *   ERR_MODULE_NOT_FOUND resolve-node-runtime.mjs
 * when get-ff-occam.sh downloaded only a flat install-user-cli.mjs into /tmp.
 */
import assert from "node:assert/strict";
import { copyFileSync, existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { spawnSync } from "node:child_process";
import {
  INSTALL_USER_CLI_TEMP_ENTRY,
  INSTALL_USER_CLI_TEMP_RELPATHS,
} from "./install-user-cli-temp-manifest.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "../../..");

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

function testManifestCoversImportGraph() {
  const entryAbs = join(repoRoot, INSTALL_USER_CLI_TEMP_ENTRY);
  assert.ok(existsSync(entryAbs), "install-user-cli.mjs must exist");
  const closure = collectLocalImportClosure(entryAbs);
  const required = new Set([INSTALL_USER_CLI_TEMP_ENTRY, ...closure]);
  for (const rel of required) {
    assert.ok(
      INSTALL_USER_CLI_TEMP_RELPATHS.includes(rel),
      `INSTALL_USER_CLI_TEMP_RELPATHS missing transitive local import: ${rel}`,
    );
  }
  console.log(`ok: manifest covers ${required.size} module(s)`);
}

function testBootstrapScriptsStageClosure() {
  const sh = readFileSync(join(repoRoot, "scripts/get-ff-occam.sh"), "utf8");
  const ps1 = readFileSync(join(repoRoot, "scripts/get-ff-occam.ps1"), "utf8");
  // Must not use the broken flat single-file temp pattern alone.
  assert.doesNotMatch(sh, /mktemp "\$\{TMPDIR:-\/tmp\}\/occam-install-user-cli\.XXXXXX\.mjs"/);
  assert.match(sh, /mktemp -d "\$\{TMPDIR:-\/tmp\}\/occam-install-user-cli\.XXXXXX"/);
  assert.match(ps1, /occam-install-user-cli-/);
  assert.match(ps1, /scripts\\lib\\resolve-node-runtime\.mjs|scripts\/lib\/resolve-node-runtime\.mjs/);
  for (const rel of INSTALL_USER_CLI_TEMP_RELPATHS) {
    const posix = rel;
    const win = rel.replace(/\//g, "\\");
    assert.ok(
      sh.includes(posix),
      `get-ff-occam.sh must stage ${posix}`,
    );
    assert.ok(
      ps1.includes(posix) || ps1.includes(win),
      `get-ff-occam.ps1 must stage ${posix}`,
    );
  }
  console.log("ok: sh/ps1 stage full temp helper closure");
}

function testStagedTempTreeExecutes() {
  const stage = mkdtempSync(join(tmpdir(), "occam-user-cli-stage-"));
  try {
    for (const rel of INSTALL_USER_CLI_TEMP_RELPATHS) {
      const from = join(repoRoot, rel);
      const to = join(stage, rel);
      mkdirSync(dirname(to), { recursive: true });
      copyFileSync(from, to);
    }
    const entry = join(stage, INSTALL_USER_CLI_TEMP_ENTRY);
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
    assert.match(importProbe.stdout, /IMPORT_OK/);

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
    console.log("ok: staged temp tree imports + --help");
  } finally {
    rmSync(stage, { recursive: true, force: true });
  }
}

testManifestCoversImportGraph();
testBootstrapScriptsStageClosure();
testStagedTempTreeExecutes();
console.log("install-user-cli-temp-helper.selftest: OK");
