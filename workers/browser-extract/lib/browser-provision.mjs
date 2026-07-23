// Branch 2 of the self-managing browser layer (SPEC-browser-autoprovision.md): when a page genuinely
// needs the browser and no browser binary is installed, occam provisions the USER-LEVEL Playwright
// chromium itself (no root, no apt) and reports that it did — instead of a bare ok:false. System
// libraries (root) remain the human's step (branch 3, the typed browser_required failure).
import { spawn } from "node:child_process";
import { closeSync, openSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

// workers/browser-extract — where Playwright is a dependency, so `npx playwright` resolves it.
const WORKER_DIR = dirname(fileURLToPath(import.meta.url)) + "/..";

/** Auto-provision is on by default; OCCAM_BROWSER_AUTOINSTALL=0 turns it into the branch-3 ask. */
export function autoInstallEnabled() {
  return process.env.OCCAM_BROWSER_AUTOINSTALL !== "0";
}

let inFlight = null; // coalesce concurrent provisions within this worker process

/**
 * Provision the user-level Playwright chromium. Idempotent (playwright install skips if present) and
 * coalesced (concurrent callers share one install). Returns telemetry; throws if the install fails.
 * @returns {Promise<{ installed: true, channel: string, path: string|undefined, tookMs: number }>}
 */
export function provisionChromium() {
  if (inFlight) return inFlight;
  inFlight = (async () => {
    const t0 = Date.now();
    const isWin = process.platform === "win32";
    const cmd = isWin ? "cmd" : "npx";
    const args = isWin ? ["/c", "npx", "playwright", "install", "chromium"] : ["playwright", "install", "chromium"];
    // Route the installer's chatty progress to a LOG FILE — not the worker's stdout (reserved for the
    // single JSON result line; a progress bar there breaks the JSON contract) and NOT its inherited
    // stderr (fd 2). On the shared-daemon path the daemon's stderr is an OS pipe the C# host never
    // drains, so ~60s of playwright download progress fills the pipe buffer and blocks the installer's
    // write() — the provision deadlocks and the first browser call hangs to a timeout. A file sink has
    // neither problem and keeps the install log diagnosable.
    const logPath = join(tmpdir(), "occam-browser-provision.log");
    let logFd = "ignore";
    try { logFd = openSync(logPath, "a"); } catch { /* no writable tmp — discard installer output */ }
    try {
      await new Promise((resolve, reject) => {
        const p = spawn(cmd, args, { cwd: WORKER_DIR, stdio: ["ignore", logFd, logFd] });
        p.on("error", reject);
        p.on("exit", (code) => (code === 0 ? resolve() : reject(new Error(`playwright install chromium exited ${code}`))));
      });
    } finally {
      if (typeof logFd === "number") { try { closeSync(logFd); } catch { /* already closed */ } }
    }
    let path;
    try {
      const { chromium } = await import("playwright");
      path = chromium.executablePath();
    } catch { /* telemetry only */ }
    return { installed: true, channel: "chromium", path, tookMs: Date.now() - t0 };
  })();
  // Reset so a later genuine need can retry, but concurrent callers still share this run.
  inFlight.finally?.(() => { inFlight = null; });
  return inFlight;
}
