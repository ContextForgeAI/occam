/**
 * Resolve Playwright chromium.launch() options from environment.
 * OCCAM_BROWSER_CHANNEL=chrome|msedge uses system browser (no ms-playwright download).
 * OCCAM_BROWSER_EXECUTABLE_PATH or OCCAM_CHROME_PATH — explicit binary path.
 *
 * Anti-detection baseline ("lean-A"): stock browser fingerprint that doesn't
 * announce "I am automated" to every site. This is table-stakes for a real
 * browser — NOT impersonation or CAPTCHA-solving. The line:
 *   ✓ hide the `navigator.webdriver` flag + disable AutomationControlled blink feature
 *   ✗ no CAPTCHA solving, no identity rotation, no proxy chaining (opt-in escalation)
 */

const ALLOWED_CHANNELS = new Set([
  "chrome",
  "msedge",
  "chrome-beta",
  "msedge-beta",
  "chromium",
]);

/**
 * Chromium args that prevent trivial bot detection without impersonation.
 * --disable-blink-features=AutomationControlled: removes the
 *   `navigator.webdriver`-related automation indicators at the engine level.
 * --headless=new is set via the `headless` option below (Playwright maps it).
 */
export const STEALTH_ARGS = [
  "--disable-blink-features=AutomationControlled",
];

/**
 * Init script injected into every browser context to mask navigator.webdriver.
 * Playwright sets it to `true`; real browsers have it `false` or absent.
 * This must run before any page script (addInitScript guarantees that).
 */
export const STEALTH_INIT_SCRIPT = `
  Object.defineProperty(navigator, 'webdriver', {
    get: () => false,
    configurable: true,
  });
`;

/**
 * @returns {import('playwright').LaunchOptions}
 */
export function resolveBrowserLaunchOptions() {
  // Playwright ≥1.33 uses the new headless mode by default (full rendering pipeline,
  // less detectable than the legacy headless shell). STEALTH_ARGS remove the remaining
  // automation indicators that anti-bot scripts check.
  const base = { headless: true, args: [...STEALTH_ARGS] };

  const executablePath =
    process.env.OCCAM_BROWSER_EXECUTABLE_PATH?.trim() ||
    process.env.OCCAM_CHROME_PATH?.trim();
  if (executablePath) {
    return { ...base, executablePath };
  }

  const channel = process.env.OCCAM_BROWSER_CHANNEL?.trim()?.toLowerCase();
  if (channel && channel !== "chromium" && ALLOWED_CHANNELS.has(channel)) {
    return { ...base, channel };
  }

  return base;
}

/**
 * Classify a browser-launch failure into an actionable "the page needs a browser" signal.
 * Distinguishes the two layers occam can/can't cross itself:
 *   - no browser BINARY (user-level, occam could provision) → root_required:false
 *   - missing system LIBRARIES (root/apt, occam cannot) → root_required:true, hand the human the command
 * Returns null for a genuine runtime/launch failure that isn't about browser availability.
 * @param {unknown} error
 * @returns {{ reason: string, fix: { kind: string, command: string, root_required: boolean } } | null}
 */
export function classifyBrowserLaunchError(error) {
  const message = String(error?.message ?? error ?? "");
  // System libraries missing (root/apt territory). Playwright says "missing dependencies to run
  // browsers" or the OS loader names the lib. Check FIRST — its message contains "install-deps",
  // which would otherwise match the binary-missing "install" pattern below.
  if (/missing dependencies to run browsers|error while loading shared libraries|install-deps|libnspr4|libnss3|libatk|libgbm|libgtk/i.test(message)) {
    return {
      reason: "a browser is present but the host is missing system libraries to launch it",
      fix: { kind: "system_deps", command: "npx playwright install-deps chromium", root_required: true },
    };
  }
  // Browser binary not installed — user-level, no root; occam can provision this (branch 2, later).
  if (/Executable doesn't exist|please run the following command|playwright install\b|browserType\.launch/i.test(message)) {
    return {
      reason: "this page needs a browser to read (JS-rendered); no browser is installed",
      fix: { kind: "manual_install", command: "occam install-browser", root_required: false },
    };
  }
  return null;
}

/** True when Playwright bundled chromium under ms-playwright is not required. */
export function usesSystemBrowser() {
  const executablePath =
    process.env.OCCAM_BROWSER_EXECUTABLE_PATH?.trim() ||
    process.env.OCCAM_CHROME_PATH?.trim();
  if (executablePath) {
    return true;
  }

  const channel = process.env.OCCAM_BROWSER_CHANNEL?.trim()?.toLowerCase();
  return Boolean(channel && channel !== "chromium" && ALLOWED_CHANNELS.has(channel));
}

