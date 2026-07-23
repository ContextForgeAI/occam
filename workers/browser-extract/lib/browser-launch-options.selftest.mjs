import assert from "node:assert/strict";
import {
  resolveBrowserLaunchOptions,
  usesSystemBrowser,
  classifyBrowserLaunchError,
  STEALTH_ARGS,
  STEALTH_INIT_SCRIPT,
} from "./browser-launch-options.mjs";
import { willAutoProvision } from "./provision-gate.mjs";

for (const key of [
  "OCCAM_BROWSER_CHANNEL",
  "OCCAM_BROWSER_EXECUTABLE_PATH",
  "OCCAM_CHROME_PATH",
]) {
  delete process.env[key];
}

// ---- lean-A stealth exports ----
assert.ok(Array.isArray(STEALTH_ARGS), "STEALTH_ARGS must be an array");
assert.ok(STEALTH_ARGS.includes("--disable-blink-features=AutomationControlled"));
assert.ok(typeof STEALTH_INIT_SCRIPT === "string");
assert.ok(STEALTH_INIT_SCRIPT.includes("webdriver"));

// ---- base (no env) ----
const base = resolveBrowserLaunchOptions();
assert.equal(base.headless, true);
assert.ok(Array.isArray(base.args));
assert.ok(base.args.includes("--disable-blink-features=AutomationControlled"));
assert.equal(base.channel, undefined);
assert.equal(base.executablePath, undefined);
assert.equal(usesSystemBrowser(), false);

// ---- channel=chrome ----
process.env.OCCAM_BROWSER_CHANNEL = "chrome";
const chrome = resolveBrowserLaunchOptions();
assert.equal(chrome.headless, true);
assert.equal(chrome.channel, "chrome");
assert.ok(chrome.args.includes("--disable-blink-features=AutomationControlled"));
assert.equal(usesSystemBrowser(), true);

// ---- explicit executable ----
delete process.env.OCCAM_BROWSER_CHANNEL;
process.env.OCCAM_BROWSER_EXECUTABLE_PATH = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const exe = resolveBrowserLaunchOptions();
assert.equal(exe.headless, true);
assert.equal(exe.executablePath, "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe");
assert.ok(exe.args.includes("--disable-blink-features=AutomationControlled"));
assert.equal(usesSystemBrowser(), true);

// ---- legacy OCCAM_CHROME_PATH ----
delete process.env.OCCAM_BROWSER_EXECUTABLE_PATH;
process.env.OCCAM_CHROME_PATH = "/usr/bin/google-chrome";
const legacy = resolveBrowserLaunchOptions();
assert.equal(legacy.headless, true);
assert.equal(legacy.executablePath, "/usr/bin/google-chrome");
assert.ok(legacy.args.includes("--disable-blink-features=AutomationControlled"));

// ---- channel=chromium treated as default (no system browser) ----
delete process.env.OCCAM_CHROME_PATH;
process.env.OCCAM_BROWSER_CHANNEL = "chromium";
const chromium = resolveBrowserLaunchOptions();
assert.equal(chromium.headless, true);
assert.equal(chromium.channel, undefined); // chromium is the default, not a "channel"
assert.equal(usesSystemBrowser(), false);

// ---- B6: provision-gate probe — the single source of truth the C# host asks ----
// This is the contract FeatureDiscoveryService.WillAutoProvisionBrowser() reads instead of mirroring the
// rule in C#. Each case below is one the old C# mirror also had to get right; they must stay in lockstep
// with browser-session.mjs's real gate (autoInstallEnabled() && !usesSystemBrowser()).
for (const key of ["OCCAM_BROWSER_CHANNEL", "OCCAM_BROWSER_EXECUTABLE_PATH", "OCCAM_CHROME_PATH", "OCCAM_BROWSER_AUTOINSTALL"]) {
  delete process.env[key];
}
assert.equal(willAutoProvision(), true, "default: bundled chromium + autoinstall on → we provision");

process.env.OCCAM_BROWSER_AUTOINSTALL = "0";
assert.equal(willAutoProvision(), false, "OCCAM_BROWSER_AUTOINSTALL=0 → branch-3 ask, no provision");
process.env.OCCAM_BROWSER_AUTOINSTALL = "false";
assert.equal(willAutoProvision(), true, "only the literal '0' disables autoinstall");
delete process.env.OCCAM_BROWSER_AUTOINSTALL;

for (const channel of ["chrome", "msedge", "chrome-beta", "msedge-beta", "  CHROME  ", "MsEdge"]) {
  process.env.OCCAM_BROWSER_CHANNEL = channel;
  assert.equal(willAutoProvision(), false, `system channel ${JSON.stringify(channel)} → playwright can't install it`);
}
process.env.OCCAM_BROWSER_CHANNEL = "chromium";
assert.equal(willAutoProvision(), true, "chromium IS the bundled browser we install");
process.env.OCCAM_BROWSER_CHANNEL = "firefox";
assert.equal(willAutoProvision(), true, "unknown channel is not a system browser → still ours to provision");
process.env.OCCAM_BROWSER_CHANNEL = "";
assert.equal(willAutoProvision(), true, "empty channel → default bundled");
delete process.env.OCCAM_BROWSER_CHANNEL;

process.env.OCCAM_BROWSER_EXECUTABLE_PATH = "/x/chrome";
assert.equal(willAutoProvision(), false, "explicit executable → not ours to install");
delete process.env.OCCAM_BROWSER_EXECUTABLE_PATH;
process.env.OCCAM_CHROME_PATH = "/x/chrome";
assert.equal(willAutoProvision(), false, "legacy explicit executable → not ours to install");
delete process.env.OCCAM_CHROME_PATH;

// ---- args are fresh copies (not shared reference) ----
delete process.env.OCCAM_BROWSER_CHANNEL;
const a = resolveBrowserLaunchOptions();
const b = resolveBrowserLaunchOptions();
assert.notStrictEqual(a.args, b.args, "args arrays must be distinct copies");
a.args.push("--test");
assert.ok(!b.args.includes("--test"), "mutating one args must not affect another");

// ---- classifyBrowserLaunchError: browser binary missing (user-level, no root) ----
const noBinary = classifyBrowserLaunchError(
  new Error("browserType.launch: Executable doesn't exist at /home/u/.cache/ms-playwright/chromium-1223/chrome-linux/chrome\nPlease run the following command to download new browsers:\n  npx playwright install"),
);
assert.ok(noBinary, "missing-binary error must classify");
assert.equal(noBinary.fix.kind, "manual_install");
assert.equal(noBinary.fix.root_required, false);
assert.match(noBinary.fix.command, /install-browser/);

// ---- classifyBrowserLaunchError: system libraries missing (root/apt) ----
const noLibs = classifyBrowserLaunchError(
  new Error("browserType.launch: Host system is missing dependencies to run browsers. Please install them with: sudo npx playwright install-deps\nMissing libraries: libnspr4, libnss3"),
);
assert.ok(noLibs, "missing-libs error must classify");
assert.equal(noLibs.fix.kind, "system_deps");
assert.equal(noLibs.fix.root_required, true, "system libs need root");
assert.match(noLibs.fix.command, /install-deps/);

// ---- system-libs is checked BEFORE binary (its message contains 'install') ----
const ambiguous = classifyBrowserLaunchError(new Error("missing dependencies to run browsers; run playwright install-deps"));
assert.equal(ambiguous.fix.kind, "system_deps", "must not misclassify a libs error as a binary error");

// ---- a genuine runtime failure is NOT a browser-availability signal ----
assert.equal(classifyBrowserLaunchError(new Error("Navigation timeout of 30000 ms exceeded")), null);
assert.equal(classifyBrowserLaunchError(new Error("net::ERR_CONNECTION_REFUSED")), null);

console.log("browser-launch-options.selftest: OK");
