#!/usr/bin/env node
/**
 * Chromium runtime gate for doctor — an actual launch is the source of truth.
 *
 * Playwright selects the browser artifact for the current launch mode (headless shell vs
 * full chromium), so a resolvable executablePath does not prove the runtime it will launch
 * is present. Only a missing-runtime failure triggers an install plus one retry; every other
 * launch failure (sandbox, permissions, missing system libraries, crash, bad architecture)
 * is reported as-is.
 */
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  resolveBrowserLaunchOptions,
  usesSystemBrowser,
} from "./browser-launch-options.mjs";

const LAUNCH_TIMEOUT_MS = 30_000;

/** Missing system libraries are root/apt territory — never an install-chromium case. */
const SYSTEM_DEPS_PATTERN =
  /missing dependencies to run browsers|error while loading shared libraries|install-deps|libnspr4|libnss3|libatk|libgbm|libgtk/i;

/** Playwright's own missing-browser diagnostics, independent of cache path or revision. */
const MISSING_RUNTIME_PATTERNS = [
  /Executable doesn't exist at/i,
  /Please run the following command to download new browsers/i,
  /npx playwright install(?!-deps)/i,
];

export function isMissingBrowserRuntime(error) {
  const message = String(error?.message ?? error ?? "");
  if (SYSTEM_DEPS_PATTERN.test(message)) {
    return false;
  }
  return MISSING_RUNTIME_PATTERNS.some((pattern) => pattern.test(message));
}

function errorMessage(error) {
  return error instanceof Error ? error.message : String(error);
}

async function launchProbe() {
  const { chromium } = await import("playwright");
  const browser = await chromium.launch({
    ...resolveBrowserLaunchOptions(),
    timeout: LAUNCH_TIMEOUT_MS,
  });
  try {
    const page = await browser.newPage();
    await page.goto("about:blank", { timeout: LAUNCH_TIMEOUT_MS });
  } finally {
    await browser.close();
  }
}

function installChromium() {
  const workerRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
  let cli;
  try {
    const require = createRequire(path.join(workerRoot, "package.json"));
    cli = path.join(
      path.dirname(require.resolve("playwright/package.json")),
      "cli.js",
    );
  } catch (error) {
    console.error(
      `error: could not resolve the playwright CLI: ${errorMessage(error)}`,
    );
    return false;
  }

  const result = spawnSync(process.execPath, [cli, "install", "chromium"], {
    cwd: workerRoot,
    stdio: "inherit",
  });
  if (result.error) {
    console.error(
      `error: could not run playwright install: ${result.error.message}`,
    );
    return false;
  }
  return result.status === 0;
}

/**
 * @returns {Promise<{ ok: boolean, installed: boolean, reason?: string, error?: unknown }>}
 */
export async function ensurePlaywrightChromiumUsable({
  launch = launchProbe,
  install = installChromium,
  allowInstall = !usesSystemBrowser(),
  log = console.log,
  logError = console.error,
} = {}) {
  try {
    await launch();
    log("playwright chromium: launch OK (skip install)");
    return { ok: true, installed: false };
  } catch (error) {
    if (!isMissingBrowserRuntime(error)) {
      logError(`error: browser launch failed: ${errorMessage(error)}`);
      return { ok: false, installed: false, reason: "launch_failed", error };
    }

    if (!allowInstall) {
      logError(
        `error: configured system browser is unavailable: ${errorMessage(error)}`,
      );
      return {
        ok: false,
        installed: false,
        reason: "system_browser_missing",
        error,
      };
    }

    log("playwright chromium: runtime missing; installing");
    if (!install()) {
      logError("error: playwright install chromium failed");
      return { ok: false, installed: false, reason: "install_failed", error };
    }

    try {
      await launch();
      log("playwright chromium: launch OK after install");
      return { ok: true, installed: true };
    } catch (retryError) {
      logError(
        `error: browser launch failed after install: ${errorMessage(retryError)}`,
      );
      return {
        ok: false,
        installed: true,
        reason: isMissingBrowserRuntime(retryError)
          ? "missing_after_install"
          : "launch_failed",
        error: retryError,
      };
    }
  }
}

async function main() {
  try {
    const { chromium } = await import("playwright");
    console.log(`playwright chromium executable: ${chromium.executablePath()}`);
  } catch {
    // Diagnostics only — the launch probe decides usability.
  }

  const result = await ensurePlaywrightChromiumUsable();
  if (!result.ok) {
    console.error(
      "hint: run playwright install chromium, or set OCCAM_BROWSER_CHANNEL=chrome|msedge with Chrome/Edge installed",
    );
    process.exit(1);
  }
  console.log(
    `browser-launch: OK (${usesSystemBrowser() ? "system" : "bundled"})`,
  );
}

const isMain =
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isMain) {
  main();
}
