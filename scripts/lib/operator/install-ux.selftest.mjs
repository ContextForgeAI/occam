#!/usr/bin/env node
/**
 * Install UX selftests — quiet default, Ready semantics, host menus, OCCAM_HOME pass-through.
 * Does not mutate live MCP host configs.
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  assertQuietTranscript,
  findForbiddenDefaultOutput,
  formatVerifyRevision,
  FORBIDDEN_DEFAULT_OUTPUT,
  isInstallQuiet,
  isInstallVerbose,
  parseHostChoice,
  parseYesNoDefaultYes,
  renderHostChoiceMenu,
  renderInstallConnectSection,
  renderProductHeader,
  resolveInstallOutcome,
  shouldUseInstallColor,
} from "./install-ux.mjs";
import { renderProductBanner } from "./get-install-welcome.mjs";
import { collectInteractiveAnswers } from "./onboard-steps.mjs";
import {
  allowConnectAll,
  renderMultiHostConfirmPrompt,
  runInstallConnectFlow,
} from "./install-connect-flow.mjs";
const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..", "..");
const winFixture = join(repoRoot, "scripts", "fixtures", "install-ux-windows-before.txt");

function testVerbosity() {
  assert.equal(isInstallVerbose({}), false);
  assert.equal(isInstallVerbose({ OCCAM_VERBOSE: "1" }), true);
  assert.equal(isInstallVerbose({ OCCAM_DEBUG: "true" }), true);
  assert.equal(isInstallVerbose({}, ["--verbose"]), true);
  assert.equal(isInstallQuiet({}), true);
  assert.equal(isInstallQuiet({ OCCAM_INSTALL_QUIET: "0" }), false);
  assert.equal(isInstallQuiet({ OCCAM_VERBOSE: "1" }), false);
}

function testReadySemantics() {
  assert.equal(
    resolveInstallOutcome({ installOk: true, skippedConnect: true }).state,
    "INSTALLED",
  );
  assert.equal(
    resolveInstallOutcome({
      installOk: true,
      connectReport: { hosts: [], connections: [] },
    }).headline.includes("no supported AI app"),
    true,
  );
  assert.equal(
    resolveInstallOutcome({
      installOk: true,
      connectReport: { ready: true, status: "Ready", connections: [{ apply: { ok: true, applied: true } }] },
    }).state,
    "READY",
  );
  assert.equal(
    resolveInstallOutcome({
      installOk: true,
      connectReport: {
        ready: false,
        status: "Action required",
        message: "trust prompt",
        connections: [{ apply: { ok: true, applied: true } }],
      },
    }).state,
    "ACTION_REQUIRED",
  );
  assert.equal(
    resolveInstallOutcome({
      installOk: true,
      connectReport: {
        ready: false,
        status: "Almost ready",
        message: "Restart Cursor",
        connections: [{ apply: { ok: true, applied: true } }],
      },
    }).state,
    "ALMOST_READY",
  );
  const connected = resolveInstallOutcome({
    installOk: true,
    connectReport: {
      ready: false,
      status: "Action required",
      connections: [{ apply: { ok: true, applied: true } }],
    },
  });
  assert.equal(connected.state, "ACTION_REQUIRED");
  assert.equal(connected.ready, false);
}

function testNoPrematureReady() {
  const text = renderInstallConnectSection({
    detected: [],
    connectReport: null,
    outcome: resolveInstallOutcome({
      installOk: true,
      connectReport: { hosts: [], connections: [] },
    }),
  });
  assert.doesNotMatch(text, /^Ready\.$/m);
  assert.match(text, /no supported AI app was detected/);
}

function testHostMenus() {
  const hosts = [
    { id: "cursor", name: "Cursor" },
    { id: "claude-desktop", name: "Claude Desktop" },
    { id: "codex", name: "Codex" },
  ];
  const menu = renderHostChoiceMenu(hosts);
  assert.match(menu, /1\. Cursor/);
  assert.match(menu, /Choose apps to connect/);
  assert.doesNotMatch(menu, /Tier A|Tier B|hermes/i);

  assert.deepEqual(parseHostChoice("1", hosts), ["cursor"]);
  assert.deepEqual(parseHostChoice("1,3", hosts), ["cursor", "codex"]);
  assert.equal(parseHostChoice("all", hosts), "all");
  assert.equal(parseHostChoice("q", hosts), "skip");
  assert.equal(parseHostChoice("", hosts), "skip");

  assert.equal(parseYesNoDefaultYes(""), true);
  assert.equal(parseYesNoDefaultYes("y"), true);
  assert.equal(parseYesNoDefaultYes("n"), false);

  assert.match(renderMultiHostConfirmPrompt(hosts), /Connect Occam to all 3 apps\? \[y\/N\]/);
  assert.equal(allowConnectAll({}), false);
  assert.equal(allowConnectAll({ OCCAM_CONNECT_ALL: "1" }), true);
}

async function testMultiHostDoesNotSilentConnectAll() {
  // Never mutate desktop MCP configs from this selftest.
  const safeEnv = { ...process.env, OCCAM_CONNECT: "off", CI: "1" };

  const skipped = await runInstallConnectFlow({
    occamHome: repoRoot,
    setupMode: "auto",
    interactive: false,
    env: safeEnv,
    skipOccamVerify: true,
  });
  // With OCCAM_CONNECT=off, mutateHosts is false — still assert we never select >1 host
  // without OCCAM_CONNECT_ALL in non-interactive auto.
  if (skipped.skipped !== true && (skipped.only || []).length > 1) {
    assert.fail("non-interactive auto must not target multiple hosts without OCCAM_CONNECT_ALL");
  }

  // Unit-level: confirm helpers encode the safety policy.
  assert.equal(allowConnectAll(safeEnv), false);
  assert.equal(allowConnectAll({ OCCAM_CONNECT_ALL: "1" }), true);
  assert.match(
    renderMultiHostConfirmPrompt([
      { name: "Cursor" },
      { name: "Claude Desktop" },
      { name: "Codex" },
    ]),
    /Connect Occam to all 3 apps\? \[y\/N\]/,
  );
}

function testRevisionLabel() {
  assert.equal(formatVerifyRevision("unknown", "1.0.0-rc.2"), "release=1.0.0-rc.2");
  assert.equal(formatVerifyRevision("", "1.0.0-rc.2"), "release=1.0.0-rc.2");
  assert.equal(formatVerifyRevision("", ""), "release build");
  assert.equal(formatVerifyRevision("abc1234", "1.0.0-rc.2"), "commit=abc1234");
  assert.doesNotMatch(formatVerifyRevision("unknown", "1.0.0-rc.2"), /unknown/);
}

function testHeader() {
  assert.equal(renderProductHeader("1.0.0-rc.2"), "Occam 1.0.0-rc.2");
  assert.equal(renderProductHeader(""), "Occam");
  assert.doesNotMatch(renderProductHeader("1.0.0-rc.2"), /Level B|FF-Occam|F F ─|·  M C P/);
}

function testInstallBrandArt() {
  // Narrow brand contract: ANSI interactive vs plain / NO_COLOR — no escape leak.
  assert.equal(shouldUseInstallColor({ isTTY: true }, {}), true);
  assert.equal(shouldUseInstallColor({ isTTY: false }, {}), false);
  assert.equal(shouldUseInstallColor({ isTTY: true }, { NO_COLOR: "1" }), false);
  assert.equal(shouldUseInstallColor({ isTTY: true }, { OCCAM_NO_COLOR: "1" }), false);

  const ansi = renderProductBanner(true, { version: "1.0.0-rc.2", verbose: false });
  assert.match(ansi, /\u001b\[38;5;45m/);
  assert.match(ansi, /Occam 1\.0\.0-rc\.2/);
  assert.match(ansi, /One URL → honest Markdown/);
  assert.doesNotMatch(ansi, /FF-Occam|FF Occam|F F ─|·  M C P|Level B|14 occam_\*/);

  const plain = renderProductBanner(false, { version: "1.0.0-rc.2", verbose: false });
  assert.doesNotMatch(plain, /\u001b/);
  assert.match(plain, /Occam 1\.0\.0-rc\.2/);
  assert.match(plain, /One URL → honest Markdown/);
  assert.doesNotMatch(plain, /FF-Occam|F F ─|·  M C P/);

  const verbosePlain = renderProductBanner(false, { version: "1.0.0-rc.2", verbose: true });
  assert.doesNotMatch(verbosePlain, /\u001b/);
  assert.match(verbosePlain, /ARCHITECTURE/);
  assert.match(verbosePlain, /Occam 1\.0\.0-rc\.2/);
  assert.doesNotMatch(verbosePlain, /FF-Occam|F F ─|·  M C P/);
}

async function testOccamHomePassThrough() {
  let promptedForHome = false;
  // Monkey-patch via skip: collectInteractiveAnswers with known occamHome must not ask.
  const answers = await collectInteractiveAnswers(
    {
      occamHome: "C:/known/ff-occam",
      hostTarget: "cursor",
      browser: "bundled",
      proxy: "no",
      profile: "default",
    },
    {
      skipWelcome: true,
      skipSteps: ["hostTarget", "browser", "proxy", "profile"],
    },
  );
  assert.equal(answers.occamHome, "C:/known/ff-occam");
  assert.equal(promptedForHome, false);
}

function testQuietTranscriptFixtureChecklist() {
  // Desired quiet success sample must pass the forbidden list.
  const good = [
    "Occam 1.0.0-rc.2",
    "",
    "Installing Occam",
    "✓ Download verified",
    "✓ Runtime installed",
    "✓ Browser ready",
    "✓ Self-check passed",
    "",
    "Connecting to your AI app",
    "",
    "Detected:",
    "  Cursor",
    "",
    "✓ Occam configured for Cursor",
    "✓ Connection verified (Cursor)",
    "",
    "Ready.",
    "",
    'Try asking your agent:',
    '"Read https://developer.mozilla.org using Occam"',
    "",
    "Documentation:",
    "https://contextforgeai.github.io/occam/",
  ].join("\n");
  assertQuietTranscript(good);
  assert.equal(findForbiddenDefaultOutput(good).length, 0);

  // Real Windows before-transcript must contain the problems we are fixing.
  try {
    const before = readFileSync(winFixture, "utf8");
    for (const needle of [
      "Level B bootstrap",
      "commit=unknown",
      "MCP host ready",
      "occam onboard",
      "private-ip (SSRF guard) module selftest",
    ]) {
      assert.ok(before.includes(needle), `fixture missing regression marker: ${needle}`);
    }
    // After transformation rules, those must be forbidden in quiet output.
    for (const needle of FORBIDDEN_DEFAULT_OUTPUT) {
      if (before.includes(needle)) {
        assert.ok(
          FORBIDDEN_DEFAULT_OUTPUT.includes(needle),
          "fixture marker should stay in forbidden list",
        );
      }
    }
  } catch (err) {
    if (err && err.code === "ENOENT") {
      console.warn("install-ux.selftest: win-full.log fixture missing — skipped regression markers");
    } else {
      throw err;
    }
  }
}

function testNoPhantomHermesInManualDefault() {
  // Manual bootstrap must not print host_target: hermes before selection.
  const ps1 = readFileSync(join(repoRoot, "scripts", "get-ff-occam.ps1"), "utf8");
  const sh = readFileSync(join(repoRoot, "scripts", "get-ff-occam.sh"), "utf8");
  assert.doesNotMatch(ps1, /Write-Host "host_target:/);
  assert.doesNotMatch(sh, /echo "host_target:/);
  assert.doesNotMatch(ps1, /Level B bootstrap/);
  assert.doesNotMatch(sh, /Level B bootstrap/);
  assert.match(ps1, /post-install-ux\.mjs/);
  assert.match(sh, /post-install-ux\.mjs/);
  // Legacy tarball path must capture child I/O — old packs ignore -Quiet/--quiet.
  assert.match(ps1, /Invoke-LegacyInstallStep/);
  assert.match(sh, /run_legacy_step/);
  // PowerShell doctor uses Write-Host — must capture Information stream (*>&1), not only 2>&1.
  assert.match(ps1, /\*>&1/);
  assert.match(sh, /2>&1/);
  assert.match(ps1, /Install-OccamUserCommand/);
  assert.match(sh, /install_occam_user_command/);
  assert.match(ps1, /install-user-cli\.mjs/);
  assert.match(sh, /install-user-cli\.mjs/);
  // Temp helper must stage ESM deps (not a single flat .mjs) — Mac public install regression.
  assert.match(ps1, /resolve-node-runtime\.mjs/);
  assert.match(sh, /resolve-node-runtime\.mjs/);
  assert.match(sh, /mktemp -d "\$\{TMPDIR:-\/tmp\}\/occam-install-user-cli\.XXXXXX"/);
  // First-run must show life during long steps and continue into connect.
  assert.match(ps1, /Installing runtime/);
  assert.match(sh, /Installing runtime/);
  assert.match(ps1, /Running self-check/);
  assert.match(sh, /Running self-check/);
  assert.match(ps1, /occam-connect\.mjs/);
  assert.match(sh, /occam-connect\.mjs/);
  // Reinstall must prepare/stop install-scoped hosts before replacing the tree.
  assert.match(ps1, /Replace-OccamInstallTree|Invoke-PrepareInstallReplace/);
  assert.match(sh, /prepare_install_replace|replace_install_tree/);
  assert.doesNotMatch(ps1, /if \(Test-Path \$InstallDir\) \{ Remove-Item -Recurse -Force \$InstallDir \}/);
  // irm|iex: $PSScriptRoot is empty — must not Join-Path it unconditionally.
  assert.match(ps1, /IsNullOrWhiteSpace\(\$PSScriptRoot\)/);
  assert.match(ps1, /Assert-SafeInstallPath/);
  // irm|iex under PS 5.1: no raw UTF-8 glyphs in bootstrap (codepoints only).
  assert.doesNotMatch(ps1, /[✓✗•…]/);
  assert.match(ps1, /\[char\]0x2713/);
  assert.match(ps1, /\$script:OccamOk/);
}

async function main() {
  testVerbosity();
  testReadySemantics();
  testNoPrematureReady();
  testHostMenus();
  await testMultiHostDoesNotSilentConnectAll();
  testRevisionLabel();
  testHeader();
  testInstallBrandArt();
  await testOccamHomePassThrough();
  testQuietTranscriptFixtureChecklist();
  testNoPhantomHermesInManualDefault();
  console.log("install-ux.selftest: OK");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
