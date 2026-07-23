import { createBrowserSession, renderAndExtract, parseExtractVariant } from "./browser-session.mjs";
import { runWithOverlay, wasOverlayApplied } from "../../shared/lib/playbook-seed.mjs";
import { hasRecipe } from "./recipes/registry.mjs";
import { readBrowserPlanFile } from "./interaction-steps.mjs";
import { buildDomSkeleton } from "./dom-skeleton.mjs";
import { flattenOpenShadowRoots } from "./shadow-dom-flatten.mjs";
import { tryDismissConsent } from "./consent.mjs";

const RECYCLE_AFTER_RUNS = 10;
const RECYCLE_MEMORY_THRESHOLD_BYTES = 400 * 1024 * 1024; // 400 MB

/** Persistent Playwright browser — amortizes chromium.launch() across requests. */
export class BrowserPool {
  /** @type {import("./browser-session.mjs").ReturnType<createBrowserSession> extends Promise<infer T> ? T : never} | null */
  #session = null;
  #runs = 0;
  #blockStylesheets = true;
  #headersFile = null;
  #storageStateFile = null;
  /** @type {Promise<unknown>} */
  #extractQueue = Promise.resolve();

  /**
   * @param {{ blockStylesheets?: boolean, headersFile?: string | null, storageStateFile?: string | null }} options
   */
  async ensureSession(options = {}) {
    if (options.blockStylesheets !== undefined) {
      this.#blockStylesheets = options.blockStylesheets === true;
    }

    if (this.#session) {
      if (options.headersFile !== undefined && options.headersFile !== this.#headersFile) {
        await this.recycle();
      }
      if (options.storageStateFile !== undefined && options.storageStateFile !== this.#storageStateFile) {
        await this.recycle();
      }
    }

    if (options.headersFile !== undefined) {
      this.#headersFile = options.headersFile;
    }
    if (options.storageStateFile !== undefined) {
      this.#storageStateFile = options.storageStateFile;
    }

    if (this.#session) {
      return this.#session;
    }

    this.#session = await createBrowserSession({
      blockStylesheets: this.#blockStylesheets,
      headersFile: this.#headersFile ?? undefined,
      storageStateFile: this.#storageStateFile ?? undefined,
    });
    this.#runs = 0;
    return this.#session;
  }

  async recycle() {
    if (this.#session) {
      await this.#session.close();
      this.#session = null;
    }
    this.#runs = 0;
  }

  /**
   * @param {string} url
   * @param {{
   *   leanAssets?: boolean,
   *   consentAggressive?: boolean,
   *   headersFile?: string | null,
   *   storageStateFile?: string | null,
   *   browserPlanFile?: string | null,
   *   extractVariant?: string,
   *   forceRecycle?: boolean,
   * }} [options]
   */
  async extract(url, options = {}) {
    // A3: a per-request overlay seed (inline genome from the daemon /extract body) is applied via
    // AsyncLocalStorage so the warm pool honours a playbook host without a cold one-shot. No overlay →
    // plain run (argv/global fallback preserved).
    const bare = async () => this.#extractOnce(url, options);
    const run = options.overlaySeed
      ? async () => runWithOverlay(options.overlaySeed, options.overlayStrict, bare)
      : bare;
    const next = this.#extractQueue.then(run, run);
    this.#extractQueue = next.catch(() => {});
    return next;
  }

  /**
   * @param {string} url
   * @param {{
   *   leanAssets?: boolean,
   *   consentAggressive?: boolean,
   *   headersFile?: string | null,
   *   storageStateFile?: string | null,
   *   browserPlanFile?: string | null,
   *   extractVariant?: string,
   *   forceRecycle?: boolean,
   *   timeoutMs?: number,
   * }} [options]
   */
  async #extractOnce(url, options = {}) {
    // Pre-warm the session BEFORE arming the per-extract timer. Session creation may auto-provision
    // chromium on first browser need (branch 2, SPEC-browser-autoprovision.md) — a one-time ~175MB
    // download that must NOT be charged against the page budget, or the very first browser call would
    // return `timeout` instead of the provisioned render (the exact first-call promise branch 2 exists
    // to keep; the one-shot worker path already provisions outside any inner race). Steady-state, once
    // the session is live, this is an instant no-op.
    if (options.forceRecycle) {
      await this.recycle();
    }
    const leanAssets = options.leanAssets !== false;
    const blockStylesheets = leanAssets || hasRecipe(url);
    const session = await this.ensureSession({
      blockStylesheets,
      headersFile: options.headersFile ?? null,
      storageStateFile: options.storageStateFile ?? null,
    });

    const hostTimeoutMs = typeof options.timeoutMs === "number" && options.timeoutMs > 0 ? options.timeoutMs : 115000;
    const daemonTimeoutMs = Math.max(1000, hostTimeoutMs - 2000); // Trigger slightly before host gives up

    let timeoutId;
    const timeoutPromise = new Promise((_, reject) => {
      timeoutId = setTimeout(() => reject(new Error("DaemonExtractTimeout")), daemonTimeoutMs);
    });

    try {
      const result = await Promise.race([
        this.#doExtractOnce(url, session, options),
        timeoutPromise
      ]);
      return result;
    } catch (error) {
      await this.recycle(); // Hard kill hung playwright processes
      if (error.message === "DaemonExtractTimeout") {
        return {
          ok: false,
          backend: "browser_playwright",
          failure: "timeout",
          message: "daemon_enforced_timeout"
        };
      }
      throw error;
    } finally {
      clearTimeout(timeoutId);
    }
  }

  /**
   * Render + extract on an already-created (pre-warmed) session. The caller (#extractOnce) creates
   * the session outside the per-extract timer so a cold branch-2 provision isn't charged to the page
   * budget; this method is what the timer actually races.
   * @param {string} url
   * @param {Awaited<ReturnType<createBrowserSession>>} session
   * @param {{ consentAggressive?: boolean, browserPlanFile?: string | null, extractVariant?: string, features?: string | null }} [options]
   */
  async #doExtractOnce(url, session, options = {}) {
    const browserPlan = readBrowserPlanFile(options.browserPlanFile ?? null);
    const extractVariant = parseExtractVariant(options.extractVariant);

    try {
      const result = await renderAndExtract(session.context, url, {
        consentAggressive: options.consentAggressive === true,
        extractVariant,
        browserPlan,
        sessionHeaders: session.sessionHeaders,
        features: options.features ?? null,
      });

      // Branch-2 telemetry: report the auto-provision once (on the first extract of a provisioned session).
      if (this.#session?.browserProvisioned && result && typeof result === "object") {
        result.browser_provisioned = this.#session.browserProvisioned;
        this.#session.browserProvisioned = null;
      }

      // A3: stamp honest provenance — whether the active per-request overlay actually matched this host
      // (computed inside the runWithOverlay scope). C# stamps PlaybookId only when this is true.
      if (result && typeof result === "object") {
        result.overlay_applied = wasOverlayApplied(url);
      }

      if (!result.ok) {
        await this.recycle();
        return result;
      }

      this.#runs++;
      
      // P1-4: Memory-aware recycle - check both run count and heap memory
      const memUsage = process.memoryUsage();
      if (this.#runs >= RECYCLE_AFTER_RUNS || memUsage.heapUsed > RECYCLE_MEMORY_THRESHOLD_BYTES) {
        await this.recycle();
      }

      return result;
    } catch (error) {
      await this.recycle();
      throw error;
    }
  }

  async close() {
    await this.recycle();
  }

  /**
   * @param {string} url
   * @param {{ maxNodes?: number, consentAggressive?: boolean, headersFile?: string | null }} [options]
   */
  async captureSkeleton(url, options = {}) {
    const run = async () => this.#captureSkeletonOnce(url, options);
    const next = this.#extractQueue.then(run, run);
    this.#extractQueue = next.catch(() => {});
    return next;
  }

  async #captureSkeletonOnce(url, options = {}) {
    const started = performance.now();
    const session = await this.ensureSession({
      blockStylesheets: true,
      headersFile: options.headersFile ?? null,
    });

    const page = await session.context.newPage();
    try {
      await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45_000 });
      if (options.consentAggressive === true) {
        await tryDismissConsent(page);
      }
      // Wait for the primary content landmark before capturing — the node-capped skeleton otherwise
      // races async render on content-heavy pages (e.g. MDN) and can miss main content, yielding
      // mainCandidates=0 (flaky L3 K1). Mirrors dom-skeleton-capture.mjs; best-effort on pages with
      // no explicit main landmark.
      try {
        await page.waitForSelector("main, [role=main], article", { timeout: 8000, state: "attached" });
      } catch {
        // no explicit main landmark — proceed with the generic settle below
      }
      await page.waitForTimeout(800);
      await flattenOpenShadowRoots(page);
      const built = await buildDomSkeleton(page, { maxNodes: options.maxNodes ?? 600 });
      return {
        Ok: true,
        Backend: "browser_daemon",
        Skeleton: {
          root: built.root,
          stats: built.stats,
        },
        Anchors: built.anchors,
        latency_ms: Math.round(performance.now() - started),
      };
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      return {
        Ok: false,
        FailureCode: "skeleton_capture_failed",
        Message: message,
        latency_ms: Math.round(performance.now() - started),
      };
    } finally {
      await page.close();
    }
  }
}
