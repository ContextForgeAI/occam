import { createInterface } from "node:readline";
import { join } from "node:path";
import { chromium } from "playwright";
import { getDefaultUserAgent } from "../../workers/shared/lib/default-fetch-headers.mjs";
import { resolveBrowserLaunchOptions } from "../../workers/browser-extract/lib/browser-launch-options.mjs";
import {
  ensureSessionsLayout,
  isValidSessionId,
  resolveSessionsRoot,
  writeSessionProfile,
} from "./occam-sessions-lib.mjs";

/**
 * @param {string} prompt
 */
function waitForEnter(prompt) {
  return new Promise((resolve) => {
    const rl = createInterface({ input: process.stdin, output: process.stderr });
    rl.question(prompt, () => {
      rl.close();
      resolve();
    });
  });
}

/**
 * @param {{
 *   url: string,
 *   id?: string,
 *   stateFile?: string,
 *   writeProfile?: boolean,
 *   userAgent?: string,
 *   force?: boolean,
 *   timeoutMs?: number,
 * }} options
 */
export async function exportPlaywrightStorageState(options) {
  const url = options.url?.trim();
  if (!url) {
    throw new Error("export-state requires --url <https://…>");
  }

  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`invalid --url: ${url}`);
  }

  if (parsed.protocol !== "https:" && parsed.protocol !== "http:") {
    throw new Error("--url must be http or https");
  }

  const host = parsed.hostname.replace(/^www\./, "");
  const id = options.id ?? host;
  if (!isValidSessionId(id)) {
    throw new Error(`invalid profile id "${id}" — use [a-zA-Z0-9._-] only`);
  }

  const sessionsRoot = resolveSessionsRoot();
  ensureSessionsLayout(sessionsRoot);
  const statesDir = join(sessionsRoot, "states");
  const stateFileName = options.stateFile ?? `${id}.json`;
  const stateAbsPath = join(statesDir, stateFileName);
  const storageStateRel = `states/${stateFileName}`;

  const userAgent = options.userAgent ?? getDefaultUserAgent();
  const launchBase = resolveBrowserLaunchOptions();
  const browser = await chromium.launch({ ...launchBase, headless: false });
  const context = await browser.newContext({ userAgent, viewport: { width: 1280, height: 720 } });
  const page = await context.newPage();

  try {
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: options.timeoutMs ?? 60_000 });
    process.stderr.write(
      "\nBrowser opened (headed). Log in, pass Cloudflare, accept cookies if needed.\n"
      + "When the page looks ready, press Enter in this terminal to save storageState…\n",
    );
    await waitForEnter("");

    await context.storageState({ path: stateAbsPath });
  } finally {
    await context.close();
    await browser.close();
  }

  let profilePath = null;
  if (options.writeProfile !== false) {
    profilePath = writeSessionProfile({
      sessionsRoot,
      id,
      headers: { "User-Agent": userAgent },
      storageState: storageStateRel,
      meta: {
        label: `Playwright storageState — ${host}`,
        hosts: [host],
        updated: new Date().toISOString().slice(0, 10),
        storageState: storageStateRel,
        notes: "Use backend_policy browser or http_then_browser with this session_profile",
      },
      force: Boolean(options.force),
    });
  }

  return {
    ok: true,
    id,
    session_profile: id,
    storageState: storageStateRel,
    statePath: stateAbsPath,
    profilePath,
    host,
    userAgent,
    hint: `occam_transcode(url, session_profile="${id}", backend_policy="browser")`,
  };
}
