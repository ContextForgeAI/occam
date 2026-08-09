#!/usr/bin/env node
import assert from "node:assert/strict";
import {
  chmodSync,
  constants as fsConstants,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join, parse as parsePath, resolve } from "node:path";
import {
  buildDisconnectPlan,
  buildLocalUninstallPlan,
  executeDisconnectPlan,
  executeLocalUninstallPlan,
  inspectInstallTarget,
  inspectResponseCacheTarget,
  looksLikeGeneratedLauncher,
  validateScopedPath,
} from "./uninstall.mjs";
import {
  OCCAM_MANAGED_ENV_KEY,
  OCCAM_MANAGED_MARKER,
} from "./connect/kinds.mjs";
import { resolveRid } from "../resolve-rid.mjs";
import {
  renderUnixLauncher,
  renderWindowsCmdLauncher,
  renderWindowsPs1Launcher,
} from "./install-user-cli.mjs";

function managedEntry(occamHome) {
  return {
    command: process.execPath,
    args: [join(occamHome, "scripts", "launch-mcp-host.mjs")],
    env: {
      OCCAM_HOME: occamHome,
      [OCCAM_MANAGED_ENV_KEY]: OCCAM_MANAGED_MARKER,
    },
  };
}

function fakeAdapter(id, occamHome, initial) {
  let registered = initial.registered === true;
  let entry = initial.entry || null;
  let rollbackCalls = 0;
  return {
    id,
    name: initial.name || id,
    inspect() {
      return { registered, entry: registered ? entry : null, path: initial.path || null };
    },
    rollback() {
      rollbackCalls += 1;
      registered = false;
      return { ok: true, removed: true };
    },
    replaceEntry(next) {
      registered = true;
      entry = next;
    },
    get rollbackCalls() {
      return rollbackCalls;
    },
  };
}

function writeReleaseTree(root) {
  const rid = resolveRid();
  mkdirSync(join(root, "scripts"), { recursive: true });
  writeFileSync(join(root, "VERSION"), "1.0.0-rc.2\n", "utf8");
  writeFileSync(
    join(root, "release-manifest.json"),
    `${JSON.stringify({ version: "1.0.0-rc.2", rid, layout: "level-b" })}\n`,
    "utf8",
  );
  writeFileSync(join(root, rid.startsWith("win-") ? "OccamMcp.Core.exe" : "OccamMcp.Core"), "fixture", "utf8");
  writeFileSync(join(root, "scripts", "occam.mjs"), "// fixture\n", "utf8");
  writeFileSync(join(root, "scripts", "launch-mcp-host.mjs"), "// fixture\n", "utf8");
}

function writeUnixLauncher(homeDir, occamHome, body) {
  const bin = join(homeDir, ".local", "bin");
  mkdirSync(bin, { recursive: true });
  const path = join(bin, "occam");
  writeFileSync(
    path,
    body ||
      [
        "#!/usr/bin/env bash",
        "set -euo pipefail",
        `export OCCAM_HOME='${occamHome.replace(/'/g, "'\\''")}'`,
        'exec "$OCCAM_NODE_BIN" "$OCCAM_HOME/scripts/occam.mjs" "$@"',
        "",
      ].join("\n"),
    "utf8",
  );
  return path;
}

function writeGeneratedLaunchers(homeDir, occamHome) {
  const bin = join(homeDir, ".local", "bin");
  mkdirSync(bin, { recursive: true });
  if (process.platform === "win32") {
    const cmd = join(bin, "occam.cmd");
    const ps1 = join(bin, "occam.ps1");
    writeFileSync(cmd, renderWindowsCmdLauncher(occamHome), "utf8");
    writeFileSync(ps1, renderWindowsPs1Launcher(occamHome), "utf8");
    return [cmd, ps1];
  }
  const launcher = join(bin, "occam");
  writeFileSync(launcher, renderUnixLauncher(occamHome), "utf8");
  return [launcher];
}

function testDisconnectOwnershipDryRunAndIdempotency() {
  const occamHome = resolve(tmpdir(), "occam-managed-fixture");
  const siblingHome = `${occamHome}-old`;
  const managed = fakeAdapter("managed", occamHome, {
    registered: true,
    entry: managedEntry(occamHome),
  });
  const sibling = fakeAdapter("sibling", occamHome, {
    registered: true,
    entry: managedEntry(siblingHome),
  });
  const legacySibling = fakeAdapter("legacy-sibling", occamHome, {
    registered: true,
    entry: {
      command: process.execPath,
      args: [join(siblingHome, "scripts", "launch-mcp-host.mjs")],
      env: {},
    },
  });
  const unmanaged = fakeAdapter("unmanaged", occamHome, {
    registered: true,
    entry: { command: "custom", args: ["serve"], env: {} },
  });
  const absent = fakeAdapter("absent", occamHome, { registered: false });
  const adapters = { managed, sibling, "legacy-sibling": legacySibling, unmanaged, absent };

  const dryPlan = buildDisconnectPlan({ occamHome, adapters });
  assert.equal(dryPlan.ok, true);
  assert.equal(dryPlan.rows.find((row) => row.id === "managed").action, "remove");
  assert.equal(dryPlan.rows.find((row) => row.id === "sibling").action, "preserve");
  assert.equal(dryPlan.rows.find((row) => row.id === "legacy-sibling").action, "preserve");
  assert.equal(dryPlan.rows.find((row) => row.id === "unmanaged").action, "preserve");
  assert.equal(managed.rollbackCalls, 0, "planning/dry-run must not mutate");
  assert.equal(unmanaged.rollbackCalls, 0);

  const applied = executeDisconnectPlan(dryPlan);
  assert.equal(applied.ok, true);
  assert.equal(managed.rollbackCalls, 1);
  assert.equal(sibling.rollbackCalls, 0, "a sibling install registration must survive");
  assert.equal(legacySibling.rollbackCalls, 0, "a legacy sibling launcher must survive");
  assert.equal(unmanaged.rollbackCalls, 0, "unmanaged ff-occam entry must survive");

  const secondPlan = buildDisconnectPlan({ occamHome, adapters });
  assert.equal(secondPlan.rows.find((row) => row.id === "managed").action, "absent");
  const second = executeDisconnectPlan(secondPlan);
  assert.equal(second.ok, true);
  assert.equal(second.applied, false);
  assert.equal(managed.rollbackCalls, 1, "second disconnect must be idempotent");

  const changed = fakeAdapter("changed", occamHome, {
    registered: true,
    entry: managedEntry(occamHome),
  });
  const changedPlan = buildDisconnectPlan({ occamHome, adapters: { changed } });
  changed.replaceEntry({ command: "user-command", args: [], env: {} });
  const changedResult = executeDisconnectPlan(changedPlan);
  assert.equal(changedResult.ok, false);
  assert.equal(changed.rollbackCalls, 0, "ownership must be rechecked before removal");
  assert.equal(changedResult.rows[0].outcome, "preserved");

  const unknown = buildDisconnectPlan({ occamHome, adapters, only: ["no-such-host"] });
  assert.equal(unknown.blocked, true);
  assert.match(unknown.error, /unknown host/i);
}

function testBroadAndUnresolvedPathsRefused() {
  const home = resolve(tmpdir(), "occam-home-safety");
  assert.equal(validateScopedPath("", { homeDir: home }).ok, false);
  assert.equal(validateScopedPath("relative/path", { homeDir: home }).ok, false);
  assert.equal(validateScopedPath(parsePath(home).root, { homeDir: home }).ok, false);
  assert.equal(validateScopedPath(home, { homeDir: home }).ok, false);
}

function testLauncherOwnershipIsRootExact() {
  const upper = "/tmp/Occam-Case-Fixture";
  const lower = "/tmp/occam-case-fixture";
  const body = [
    "#!/usr/bin/env bash",
    "set -euo pipefail",
    `export OCCAM_HOME='${upper}'`,
    'exec "$OCCAM_NODE_BIN" "$OCCAM_HOME/scripts/occam.mjs" "$@"',
  ].join("\n");
  assert.equal(looksLikeGeneratedLauncher(body, upper, "occam", "linux"), true);
  assert.equal(
    looksLikeGeneratedLauncher(body, lower, "occam", "linux"),
    false,
    "case-distinct Unix install paths must not share launcher ownership",
  );
  const unixRoot = "/tmp/ff-occam";
  const unixSibling = "/tmp/ff-occam-old";
  assert.equal(
    looksLikeGeneratedLauncher(renderUnixLauncher(unixSibling), unixRoot, "occam", "linux"),
    false,
    "a Unix sibling install must not match by path prefix",
  );

  const windowsRoot = "C:\\Users\\Example\\ff-occam";
  const windowsSibling = "C:\\Users\\Example\\ff-occam-old";
  assert.equal(
    looksLikeGeneratedLauncher(
      renderWindowsCmdLauncher(windowsRoot),
      "c:\\users\\example\\FF-OCCAM",
      "occam.cmd",
      "win32",
    ),
    true,
    "Windows launcher ownership remains case-insensitive",
  );
  assert.equal(
    looksLikeGeneratedLauncher(
      renderWindowsCmdLauncher(windowsSibling),
      windowsRoot,
      "occam.cmd",
      "win32",
    ),
    false,
    "a Windows .cmd sibling install must not match by path prefix",
  );
  assert.equal(
    looksLikeGeneratedLauncher(
      renderWindowsPs1Launcher(windowsSibling),
      windowsRoot,
      "occam.ps1",
      "win32",
    ),
    false,
    "a Windows PowerShell sibling install must not match by path prefix",
  );
}

function testReleaseUninstallDryPlanAndIdempotency() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-"));
  const home = join(fixture, "home");
  const install = join(home, ".local", "share", "ff-occam");
  const state = join(home, ".occam");
  const responseCache = join(fixture, "cache", "occam-cache");
  const playwrightCache = join(fixture, "shared", "ms-playwright");
  mkdirSync(state, { recursive: true });
  mkdirSync(responseCache, { recursive: true });
  mkdirSync(playwrightCache, { recursive: true });
  writeFileSync(join(state, "keep.txt"), "preserve", "utf8");
  writeFileSync(join(responseCache, `${"a".repeat(64)}.json`), "{}", "utf8");
  writeFileSync(join(playwrightCache, "shared-browser"), "keep", "utf8");
  writeReleaseTree(install);
  const launchers = writeGeneratedLaunchers(home, install);

  const plan = buildLocalUninstallPlan({
    occamHome: install,
    homeDir: home,
    platform: process.platform,
    removeCache: true,
    env: {
      OCCAM_CACHE_DIR: responseCache,
      PLAYWRIGHT_BROWSERS_PATH: playwrightCache,
    },
  });
  assert.equal(plan.blocked, false);
  assert.equal(plan.targets.find((row) => row.kind === "install").action, "remove");
  assert.equal(plan.targets.filter((row) => row.kind === "launcher").every((row) => row.action === "remove"), true);
  assert.equal(plan.targets.find((row) => row.kind === "state").action, "preserve");
  assert.equal(plan.targets.find((row) => row.kind === "response-cache").action, "remove");
  assert.equal(plan.targets.find((row) => row.kind === "playwright-cache").action, "preserve");
  assert.equal(existsSync(install), true, "planning/dry-run must keep install tree");
  assert.equal(launchers.every((launcher) => existsSync(launcher)), true, "planning/dry-run must keep launchers");

  const applied = executeLocalUninstallPlan(plan, {
    prepareInstall: () => ({ ok: true, stopped: [] }),
  });
  assert.equal(applied.ok, true);
  assert.equal(existsSync(install), false);
  assert.equal(launchers.every((launcher) => !existsSync(launcher)), true);
  assert.equal(existsSync(state), true, "state must survive default uninstall");
  assert.equal(existsSync(responseCache), false, "explicit response cache scope is removed");
  assert.equal(existsSync(playwrightCache), true, "shared Playwright cache must survive");

  const secondPlan = buildLocalUninstallPlan({
    occamHome: install,
    homeDir: home,
    platform: process.platform,
  });
  const second = executeLocalUninstallPlan(secondPlan, {
    prepareInstall: () => ({ ok: true, stopped: [] }),
  });
  assert.equal(second.ok, true);
  assert.equal(second.applied, false);
  assert.equal(existsSync(state), true);

  rmSync(fixture, { recursive: true, force: true });
}

function testSourceCheckoutAndExplicitStateRemoval() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-source-"));
  const home = join(fixture, "home");
  const source = join(fixture, "repo");
  mkdirSync(join(source, ".git"), { recursive: true });
  mkdirSync(join(home, ".occam", "sessions"), { recursive: true });
  writeFileSync(join(home, ".occam", "sessions", "secret.json"), "{}", "utf8");

  const target = inspectInstallTarget(source, { homeDir: home });
  assert.equal(target.action, "preserve");
  const plan = buildLocalUninstallPlan({
    occamHome: source,
    homeDir: home,
    platform: "linux",
    removeState: true,
  });
  const applied = executeLocalUninstallPlan(plan, {
    prepareInstall: () => ({ ok: true, stopped: [] }),
  });
  assert.equal(applied.ok, true);
  assert.equal(existsSync(source), true, "source checkout must never be removed");
  assert.equal(existsSync(join(home, ".occam")), false, "explicit state flag removes state");

  rmSync(fixture, { recursive: true, force: true });
}

function testUnknownTreeAndUnrelatedLauncherPreserved() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-refuse-"));
  const home = join(fixture, "home");
  const unknown = join(home, "unknown-install");
  mkdirSync(unknown, { recursive: true });
  writeFileSync(join(unknown, "VERSION"), "not-enough\n", "utf8");
  const launcher = writeUnixLauncher(home, unknown, "#!/usr/bin/env bash\necho custom\n");

  const plan = buildLocalUninstallPlan({
    occamHome: unknown,
    homeDir: home,
    platform: "linux",
  });
  assert.equal(plan.blocked, true);
  assert.equal(plan.targets.find((row) => row.kind === "install").action, "refuse");
  assert.equal(plan.targets.find((row) => row.kind === "launcher").action, "preserve");
  const applied = executeLocalUninstallPlan(plan, {
    prepareInstall: () => {
      throw new Error("blocked plans must not prepare or mutate");
    },
  });
  assert.equal(applied.ok, false);
  assert.equal(existsSync(unknown), true);
  assert.equal(existsSync(launcher), true);

  rmSync(fixture, { recursive: true, force: true });
}

function testReleaseMetadataAndCacheSymlinkRefusal() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-metadata-"));
  const home = join(fixture, "home");
  const install = join(fixture, "release");
  writeReleaseTree(install);
  writeFileSync(
    join(install, "release-manifest.json"),
    `${JSON.stringify({ version: "1.0.0-rc.2", rid: resolveRid(), layout: "source" })}\n`,
    "utf8",
  );
  const badManifest = inspectInstallTarget(install, { homeDir: home });
  assert.equal(badManifest.action, "refuse");
  assert.match(badManifest.reason, /layout/i);
  writeFileSync(join(install, "release-manifest.json"), "null\n", "utf8");
  const nullManifest = inspectInstallTarget(install, { homeDir: home });
  assert.equal(nullManifest.action, "refuse");
  assert.match(nullManifest.reason, /manifest root/i);

  const realCache = join(fixture, "real-cache");
  const linkedCache = join(fixture, "configured", "occam-cache");
  mkdirSync(realCache, { recursive: true });
  mkdirSync(join(fixture, "configured"), { recursive: true });
  symlinkSync(realCache, linkedCache, process.platform === "win32" ? "junction" : "dir");
  const cache = inspectResponseCacheTarget({
    env: { OCCAM_CACHE_DIR: linkedCache },
    homeDir: home,
    tempDir: fixture,
    removeCache: true,
  });
  assert.equal(cache.action, "refuse");
  assert.match(cache.reason, /real directory|preserved/i);
  assert.equal(existsSync(realCache), true);

  const broad = inspectResponseCacheTarget({
    env: { OCCAM_CACHE_DIR: home },
    homeDir: home,
    tempDir: fixture,
    removeCache: true,
  });
  assert.equal(broad.action, "refuse");

  const emptyCustom = join(fixture, "empty-custom-cache");
  mkdirSync(emptyCustom, { recursive: true });
  const emptyCustomResult = inspectResponseCacheTarget({
    env: { OCCAM_CACHE_DIR: emptyCustom },
    homeDir: home,
    tempDir: fixture,
    removeCache: true,
  });
  assert.equal(emptyCustomResult.action, "refuse");
  assert.match(emptyCustomResult.reason, /empty custom/i);

  const defaultEmpty = join(fixture, "occam-cache");
  mkdirSync(defaultEmpty, { recursive: true });
  const defaultEmptyResult = inspectResponseCacheTarget({
    env: {},
    homeDir: home,
    tempDir: fixture,
    removeCache: true,
  });
  assert.equal(defaultEmptyResult.action, "remove");

  rmSync(fixture, { recursive: true, force: true });
}

function testInstallDeleteFailureRestoresLaunchers() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-restore-"));
  const home = join(fixture, "home");
  const install = join(home, ".local", "share", "ff-occam");
  writeReleaseTree(install);
  const launchers = writeGeneratedLaunchers(home, install);
  const before = new Map(launchers.map((path) => [path, readFileSync(path, "utf8")]));
  const plan = buildLocalUninstallPlan({
    occamHome: install,
    homeDir: home,
    platform: process.platform,
    env: {},
    tempDir: fixture,
  });
  const result = executeLocalUninstallPlan(plan, {
    prepareInstall: () => ({ ok: true, stopped: [] }),
    removeInstall: () => {
      throw new Error("injected install delete failure");
    },
  });
  assert.equal(result.ok, false);
  assert.equal(existsSync(install), true, "install tree remains after injected delete failure");
  for (const launcher of launchers) {
    assert.equal(existsSync(launcher), true, "launcher must be restored when install survives");
    assert.equal(readFileSync(launcher, "utf8"), before.get(launcher));
  }
  assert.equal(
    result.targets.filter((row) => row.kind === "launcher").every((row) => row.outcome === "restored"),
    true,
  );

  rmSync(fixture, { recursive: true, force: true });
}

function testInstallMetadataRecheckRestoresLaunchers() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-recheck-"));
  const home = join(fixture, "home");
  const install = join(home, ".local", "share", "ff-occam");
  writeReleaseTree(install);
  const launchers = writeGeneratedLaunchers(home, install);
  const before = new Map(launchers.map((path) => [path, readFileSync(path, "utf8")]));
  const plan = buildLocalUninstallPlan({
    occamHome: install,
    homeDir: home,
    platform: process.platform,
    env: {},
    tempDir: fixture,
  });

  const result = executeLocalUninstallPlan(plan, {
    prepareInstall: () => {
      writeFileSync(
        join(install, "release-manifest.json"),
        `${JSON.stringify({ version: "1.0.0-rc.2", rid: resolveRid(), layout: "changed" })}\n`,
        "utf8",
      );
      return { ok: true, stopped: [] };
    },
  });

  assert.equal(result.ok, false);
  assert.equal(existsSync(install), true, "metadata refusal must preserve the install tree");
  assert.match(result.targets.find((row) => row.kind === "install").reason, /layout/i);
  for (const launcher of launchers) {
    assert.equal(existsSync(launcher), true, "launcher must be restored after metadata refusal");
    assert.equal(readFileSync(launcher, "utf8"), before.get(launcher));
  }
  assert.equal(
    result.targets.filter((row) => row.kind === "launcher").every((row) => row.outcome === "restored"),
    true,
  );

  rmSync(fixture, { recursive: true, force: true });
}

function testReadonlyReleaseTreeRemoval() {
  const fixture = mkdtempSync(join(tmpdir(), "occam-uninstall-readonly-"));
  const home = join(fixture, "home");
  const install = join(home, ".local", "share", "ff-occam");
  writeReleaseTree(install);
  const locked = join(install, "OccamMcp.Core.exe");
  writeFileSync(locked, "fake-binary");
  chmodSync(locked, fsConstants.S_IRUSR | fsConstants.S_IRGRP | fsConstants.S_IROTH);
  writeGeneratedLaunchers(home, install);
  const plan = buildLocalUninstallPlan({
    occamHome: install,
    homeDir: home,
    platform: process.platform,
    env: {},
    tempDir: fixture,
  });
  assert.equal(plan.ok, true);
  assert.equal(plan.blocked, false);
  const result = executeLocalUninstallPlan(plan, {
    prepareInstall: () => ({ ok: true, stopped: [] }),
    chdir: () => {},
  });
  assert.equal(result.ok, true, result.targets?.find((t) => t.kind === "install")?.reason || "uninstall ok");
  assert.equal(existsSync(install), false, "readonly release tree must be removable");
  rmSync(fixture, { recursive: true, force: true });
}

function main() {
  testDisconnectOwnershipDryRunAndIdempotency();
  testBroadAndUnresolvedPathsRefused();
  testLauncherOwnershipIsRootExact();
  testReleaseUninstallDryPlanAndIdempotency();
  testSourceCheckoutAndExplicitStateRemoval();
  testUnknownTreeAndUnrelatedLauncherPreserved();
  testReleaseMetadataAndCacheSymlinkRefusal();
  testInstallDeleteFailureRestoresLaunchers();
  testInstallMetadataRecheckRestoresLaunchers();
  testReadonlyReleaseTreeRemoval();
  console.log("uninstall.selftest.mjs OK");
}

main();
