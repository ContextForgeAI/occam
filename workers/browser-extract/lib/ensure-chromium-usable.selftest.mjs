#!/usr/bin/env node
/**
 * Regression for the GHA macOS defect: a resolvable chromium executable while the
 * headless-shell artifact Playwright actually launches is absent.
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  ensurePlaywrightChromiumUsable,
  isMissingBrowserRuntime,
} from "./ensure-chromium-usable.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../..");

const MISSING_HEADLESS_SHELL = [
  "browserType.launch: Executable doesn't exist at",
  "/Users/runner/Library/Caches/ms-playwright/chromium_headless_shell-1223/chrome-headless-shell-mac-arm64/chrome-headless-shell",
  "╔════════════════════════════════════════════════════════════╗",
  "║ Looks like Playwright was just installed or updated.       ║",
  "║ Please run the following command to download new browsers: ║",
  "║     npx playwright install                                 ║",
  "╚════════════════════════════════════════════════════════════╝",
].join("\n");

function scenario({ launchOutcomes, installResult = true }) {
  const calls = { launches: 0, installs: 0 };
  const logs = [];
  return {
    calls,
    logs,
    options: {
      allowInstall: true,
      log: (line) => logs.push(line),
      logError: (line) => logs.push(line),
      launch: async () => {
        const outcome = launchOutcomes[calls.launches];
        calls.launches += 1;
        if (outcome) {
          throw new Error(outcome);
        }
      },
      install: () => {
        calls.installs += 1;
        return installResult;
      },
    },
  };
}

async function main() {
  // A: an ordinary chromium executable exists, but the launched artifact is missing.
  const a = scenario({ launchOutcomes: [MISSING_HEADLESS_SHELL, null] });
  const aResult = await ensurePlaywrightChromiumUsable(a.options);
  assert.equal(aResult.ok, true, "A: recovered launch must pass");
  assert.equal(aResult.installed, true, "A: install must run");
  assert.equal(a.calls.installs, 1, "A: install must run exactly once");
  assert.equal(a.calls.launches, 2, "A: launch must be retried after install");

  // B: the first launch succeeds — never install.
  const b = scenario({ launchOutcomes: [null] });
  const bResult = await ensurePlaywrightChromiumUsable(b.options);
  assert.equal(bResult.ok, true, "B: usable runtime must pass");
  assert.equal(b.calls.installs, 0, "B: install must not run");
  assert.equal(b.calls.launches, 1, "B: a single launch must decide");

  // C: install reports success but the runtime is still missing.
  const c = scenario({
    launchOutcomes: [MISSING_HEADLESS_SHELL, MISSING_HEADLESS_SHELL],
  });
  const cResult = await ensurePlaywrightChromiumUsable(c.options);
  assert.equal(cResult.ok, false, "C: unrecovered runtime must fail");
  assert.equal(cResult.reason, "missing_after_install");
  assert.equal(c.calls.launches, 2, "C: exactly one retry");

  // D: the install command itself fails.
  const d = scenario({
    launchOutcomes: [MISSING_HEADLESS_SHELL, null],
    installResult: false,
  });
  const dResult = await ensurePlaywrightChromiumUsable(d.options);
  assert.equal(dResult.ok, false, "D: failed install must fail");
  assert.equal(dResult.reason, "install_failed");
  assert.equal(d.calls.launches, 1, "D: no retry after a failed install");

  // E: generic launch failures must never be treated as a missing runtime.
  for (const message of [
    "browserType.launch: EACCES: permission denied",
    "browserType.launch: Browser closed unexpectedly (crashed)",
    "dyld: Library not loaded: @rpath/libffmpeg.dylib",
    "browserType.launch: Host system is missing dependencies to run browsers: libnspr4",
    "some arbitrary failure",
  ]) {
    assert.equal(
      isMissingBrowserRuntime(message),
      false,
      `E: "${message}" must not be classified as a missing runtime`,
    );
    const e = scenario({ launchOutcomes: [message, null] });
    const eResult = await ensurePlaywrightChromiumUsable(e.options);
    assert.equal(eResult.ok, false, "E: generic failure must fail");
    assert.equal(eResult.reason, "launch_failed");
    assert.equal(e.calls.installs, 0, "E: install must not run");
    assert.match(e.logs.join("\n"), /error: browser launch failed:/);
  }

  assert.equal(
    isMissingBrowserRuntime(MISSING_HEADLESS_SHELL),
    true,
    "missing headless shell must classify as a missing runtime",
  );

  // F: a present executablePath must not shortcut the launch verdict.
  const doctorShell = fs.readFileSync(
    path.join(root, "scripts/occam-doctor.sh"),
    "utf8",
  );
  const doctorPowerShell = fs.readFileSync(
    path.join(root, "scripts/occam-doctor.ps1"),
    "utf8",
  );
  for (const [name, doctor] of [
    ["occam-doctor.sh", doctorShell],
    ["occam-doctor.ps1", doctorPowerShell],
  ]) {
    assert.doesNotMatch(
      doctor,
      /has-chromium|require-chromium/,
      `F: ${name} must not gate on executable detection`,
    );
    assert.match(
      doctor,
      /ensure-chromium-usable\.mjs/,
      `F: ${name} must gate on the launch probe`,
    );
    assert.equal(
      doctor.match(/ensure-chromium-usable\.mjs/g).length,
      1,
      `F: ${name} must run a single launch probe`,
    );
  }
  assert.match(doctorShell, /browser runtime unavailable[\s\S]*exit 1/);
  assert.match(doctorPowerShell, /Write-Error "browser runtime unavailable"/);

  console.log("ensure-chromium-usable.selftest: OK");
  console.log("A missing headless shell: install + retry launch");
  console.log("B usable runtime: no install");
  console.log("C runtime missing after install: FAIL");
  console.log("D install command failure: FAIL");
  console.log("E generic launch failure: FAIL without install");
  console.log("F doctor gates on launch, not executablePath");
}

main();
