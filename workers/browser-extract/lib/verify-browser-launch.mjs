#!/usr/bin/env node
/**
 * Launch smoke for Playwright browser backend (bundled chromium or system channel).
 * Used by doctor and install verify — fails fast before MCP runtime.
 */
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import {
  resolveBrowserLaunchOptions,
  usesSystemBrowser,
} from "./browser-launch-options.mjs";

const TIMEOUT_MS = 30_000;

/**
 * @returns {Promise<{ ok: true, mode: "system" | "bundled" }>}
 */
export async function verifyBrowserLaunch() {
  const options = resolveBrowserLaunchOptions();
  const mode = usesSystemBrowser() ? "system" : "bundled";
  const browser = await chromium.launch({ ...options, timeout: TIMEOUT_MS });
  try {
    const page = await browser.newPage();
    await page.goto("about:blank", { timeout: TIMEOUT_MS });
  } finally {
    await browser.close();
  }
  return { ok: true, mode };
}

async function main() {
  try {
    const result = await verifyBrowserLaunch();
    console.log(`browser-launch: OK (${result.mode})`);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    console.error(`error: browser launch failed: ${message}`);
    console.error(
      "hint: run playwright install chromium, or set OCCAM_BROWSER_CHANNEL=chrome|msedge with Chrome/Edge installed",
    );
    process.exit(1);
  }
}

const isMain =
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isMain) {
  main();
}
