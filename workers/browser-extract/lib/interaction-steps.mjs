/**
 * P10-D2: playbook browser plan — js_before_wait → wait_for → interaction_steps.
 */
import { readFileSync } from "node:fs";
import { normalizeVirtualScrollConfig } from "./virtual-scroll.mjs";
export async function runBrowserPlan(page, plan) {
  if (!plan) {
    return { steps_run: 0 };
  }

  let stepsRun = 0;

  if (plan.js_before_wait) {
    await page.evaluate(plan.js_before_wait);
  }

  if (plan.wait_for?.selector) {
    await page.waitForSelector(plan.wait_for.selector, {
      timeout: plan.wait_for.timeout_ms ?? 12_000,
    });
  } else if (plan.wait_for?.js) {
    await page.waitForFunction(plan.wait_for.js, undefined, {
      timeout: plan.wait_for.timeout_ms ?? 12_000,
    });
  }

  for (const step of plan.interaction_steps ?? []) {
    await runInteractionStep(page, step);
    stepsRun += 1;
  }

  return { steps_run: stepsRun };
}

/**
 * @param {import('playwright').Page} page
 * @param {{ action: string, selector?: string, ms?: number, text?: string }} step
 */
async function runInteractionStep(page, step) {
  const action = String(step.action ?? "").toLowerCase();
  switch (action) {
    case "scroll":
      await page.evaluate(() => window.scrollBy(0, Math.max(400, window.innerHeight * 0.85)));
      return;
    case "click": {
      const selector = step.selector?.trim();
      if (!selector) return;
      await page.locator(selector).first().click({ timeout: 8000 });
      return;
    }
    case "wait":
      await page.waitForTimeout(Math.max(50, Math.min(step.ms ?? 500, 60_000)));
      return;
    case "type": {
      const selector = step.selector?.trim();
      if (!selector) return;
      await page.locator(selector).first().fill(step.text ?? "", { timeout: 8000 });
      return;
    }
    default:
      return;
  }
}

/**
 * @param {string | undefined} filePath
 */
export function readBrowserPlanFile(filePath) {
  if (!filePath) return null;
  try {
    const raw = readFileSync(filePath, "utf8");
    const parsed = JSON.parse(raw);
    return normalizeBrowserPlan(parsed);
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "read_failed";
    console.error(`[occam.worker] browser_plan_read_failed code=${code}`);
    return null;
  }
}

/** @param {unknown} raw */
function normalizeBrowserPlan(raw) {
  if (!raw || typeof raw !== "object") return null;
  const plan = /** @type {Record<string, unknown>} */ (raw);
  const steps = Array.isArray(plan.interaction_steps)
    ? plan.interaction_steps
        .map((entry) => {
          if (!entry || typeof entry !== "object") return null;
          const step = /** @type {Record<string, unknown>} */ (entry);
          const action = String(step.action ?? "").toLowerCase();
          if (!["scroll", "click", "wait", "type"].includes(action)) return null;
          return {
            action,
            selector: typeof step.selector === "string" ? step.selector : undefined,
            ms: typeof step.ms === "number" ? step.ms : undefined,
            text: typeof step.text === "string" ? step.text : undefined,
          };
        })
        .filter(Boolean)
    : [];

  const waitForRaw = plan.wait_for;
  let wait_for = null;
  if (typeof waitForRaw === "string" && waitForRaw.trim()) {
    wait_for = { selector: waitForRaw.trim(), timeout_ms: 12_000 };
  } else if (waitForRaw && typeof waitForRaw === "object") {
    const wf = /** @type {Record<string, unknown>} */ (waitForRaw);
    wait_for = {
      selector: typeof wf.selector === "string" ? wf.selector : undefined,
      js: typeof wf.js === "string" ? wf.js : undefined,
      timeout_ms: typeof wf.timeout_ms === "number" ? wf.timeout_ms : 12_000,
    };
  }

  const js_before_wait =
    typeof plan.js_before_wait === "string" && plan.js_before_wait.trim()
      ? plan.js_before_wait
      : undefined;

  const virtual_scroll =
    plan.virtual_scroll && typeof plan.virtual_scroll === "object"
      ? normalizeVirtualScrollConfig(plan.virtual_scroll)
      : undefined;

  if (!js_before_wait && !wait_for && steps.length === 0 && !virtual_scroll) {
    return null;
  }

  return { js_before_wait, wait_for, interaction_steps: steps, virtual_scroll };
}
