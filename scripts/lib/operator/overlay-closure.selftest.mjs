#!/usr/bin/env node
/**
 * Regression: operator overlay must be a closed dependency set for legacy rc.2
 * install trees. Simulates the live macOS failure mode where connect-onboarding
 * was overlaid without tty.mjs.
 */
import assert from "node:assert/strict";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname } from "node:path";
import {
  OPERATOR_OVERLAY_ENTRYPOINTS,
  OPERATOR_OVERLAY_FILES,
  applyOperatorOverlay,
  assertOperatorOverlayImports,
  collectLocalImportClosure,
  needsOperatorOverlay,
} from "./install-user-cli.mjs";
import { formatInstallerComponentError } from "./install-ux.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..", "..");

function testManifestCoversClosure() {
  const closure = collectLocalImportClosure(repoRoot);
  const overlay = new Set(OPERATOR_OVERLAY_FILES);
  const missing = closure.filter(
    (rel) =>
      !overlay.has(rel) &&
      rel !== "scripts/lib/operator/install-user-cli.mjs",
  );
  assert.deepEqual(
    missing,
    [],
    `OPERATOR_OVERLAY_FILES missing closure deps:\n${missing.join("\n")}`,
  );
  for (const ep of OPERATOR_OVERLAY_ENTRYPOINTS) {
    assert.ok(overlay.has(ep) || ep.endsWith("install-user-cli.mjs"), `entrypoint ${ep}`);
  }
  assert.ok(overlay.has("scripts/lib/operator/tty.mjs"), "tty.mjs must be overlaid");
}

function testHumanErrorHidesModuleNotFound() {
  const err = new Error(
    "Cannot find module '/x/scripts/lib/operator/tty.mjs' imported from connect-onboarding.mjs",
  );
  err.code = "ERR_MODULE_NOT_FOUND";
  const quiet = formatInstallerComponentError(err, { verbose: false });
  assert.match(quiet, /Occam installation could not be completed/);
  assert.match(quiet, /required installer component is missing/);
  assert.doesNotMatch(quiet, /ERR_MODULE_NOT_FOUND/);
  assert.doesNotMatch(quiet, /Cannot find module/);
  const verbose = formatInstallerComponentError(err, { verbose: true });
  assert.match(verbose, /Cannot find module/);
}

async function testRc2PartialOverlayThenHeal() {
  const root = mkdtempSync(join(tmpdir(), "occam-overlay-rc2-"));
  try {
    // Legacy rc.2-shaped tree: no connect CLI.
    assert.equal(needsOperatorOverlay(root), true);

    // Reproduce the broken live state: new onboarding without tty.mjs.
    mkdirSync(join(root, "scripts", "lib", "operator"), { recursive: true });
    writeFileSync(
      join(root, "scripts", "lib", "operator", "connect-onboarding.mjs"),
      'import { canPromptInteractively } from "./tty.mjs";\nexport function runConnectOnboarding() {}\n',
      "utf8",
    );
    writeFileSync(join(root, "scripts", "occam-connect.mjs"), "// stub connect\n", "utf8");
    assert.equal(
      needsOperatorOverlay(root),
      true,
      "missing tty.mjs must still require overlay (not connect-only gate)",
    );
    assert.equal(existsSync(join(root, "scripts/lib/operator/tty.mjs")), false);

    const r = await applyOperatorOverlay(repoRoot, root);
    assert.ok(r.written.includes("scripts/lib/operator/tty.mjs"));
    assert.ok(r.written.includes("scripts/lib/operator/connect-onboarding.mjs"));
    assert.equal(needsOperatorOverlay(root), false);
    assert.ok(existsSync(join(root, "scripts/lib/operator/tty.mjs")));

    // Real ESM resolve from the staged install tree.
    await assertOperatorOverlayImports(root);
    const onboardingHref = pathToFileURL(
      join(root, "scripts/lib/operator/connect-onboarding.mjs"),
    ).href;
    const mod = await import(onboardingHref);
    assert.equal(typeof mod.runConnectOnboarding, "function");

    // Closure files from entrypoints must all exist on disk after overlay.
    const closure = collectLocalImportClosure(root);
    for (const rel of closure) {
      assert.ok(existsSync(join(root, ...rel.split("/"))), `missing after overlay: ${rel}`);
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

async function testAtomicOverlayAbortsOnFetchFailure() {
  const root = mkdtempSync(join(tmpdir(), "occam-overlay-atomic-"));
  try {
    let calls = 0;
    await assert.rejects(
      () =>
        applyOperatorOverlay(repoRoot, root, {
          files: [
            "scripts/lib/operator/tty.mjs",
            "scripts/lib/operator/connect-onboarding.mjs",
          ],
          fetchText: async (rel) => {
            calls += 1;
            if (String(rel).includes("connect-onboarding")) {
              throw new Error("simulated download failure");
            }
            return readFileSync(join(repoRoot, ...String(rel).split("/")), "utf8");
          },
        }),
      /simulated download failure/,
    );
    assert.ok(calls >= 1);
    assert.equal(
      existsSync(join(root, "scripts/lib/operator/tty.mjs")),
      false,
      "failed overlay must not leave partial files in the install tree",
    );
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

function testShellAndPs1UseSharedOverlayHelper() {
  const sh = readFileSync(join(repoRoot, "scripts", "get-ff-occam.sh"), "utf8");
  const ps1 = readFileSync(join(repoRoot, "scripts", "get-ff-occam.ps1"), "utf8");
  assert.match(sh, /install-user-cli\.mjs/);
  assert.match(ps1, /install-user-cli\.mjs/);
  assert.match(sh, /OCCAM_OVERLAY_BASE_URL/);
  assert.match(ps1, /OCCAM_OVERLAY_BASE_URL/);
  // No divergent hard-coded overlay file lists in the bootstraps.
  assert.doesNotMatch(sh, /OPERATOR_OVERLAY_FILES|connect-onboarding\.mjs"\s*\\/);
  assert.doesNotMatch(ps1, /OPERATOR_OVERLAY_FILES/);
}

async function main() {
  testManifestCoversClosure();
  testHumanErrorHidesModuleNotFound();
  testShellAndPs1UseSharedOverlayHelper();
  await testAtomicOverlayAbortsOnFetchFailure();
  await testRc2PartialOverlayThenHeal();
  console.log("overlay-closure.selftest: OK");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
