/**
 * MCP V1 browser actions for occam_browser_interact (OCCAM_BROWSER_ACTIONS_MCP=1).
 * Public surface: declarative steps only — no js_before_wait / wait_for.js.
 */
import { createHash } from "node:crypto";

export const MAX_MCP_ACTIONS = 16;
export const DEFAULT_ACTION_TIMEOUT_MS = 8_000;
export const DEFAULT_DEADLINE_MS = 90_000;
export const ALLOWED_DOS = Object.freeze([
  "wait",
  "wait_selector",
  "wait_text",
  "click",
  "hover",
  "type",
  "press",
  "scroll",
]);

/**
 * @param {unknown} raw
 * @returns {{ ok: true, actions: object[] } | { ok: false, failure: string, message: string }}
 */
export function validateMcpActions(raw) {
  if (!Array.isArray(raw)) {
    return { ok: false, failure: "invalid_arguments", message: "actions must be a JSON array." };
  }
  if (raw.length === 0) {
    return { ok: false, failure: "invalid_arguments", message: "actions must not be empty." };
  }
  if (raw.length > MAX_MCP_ACTIONS) {
    return {
      ok: false,
      failure: "invalid_arguments",
      message: `actions length ${raw.length} exceeds max ${MAX_MCP_ACTIONS}.`,
    };
  }

  /** @type {object[]} */
  const actions = [];
  for (let i = 0; i < raw.length; i++) {
    const entry = raw[i];
    if (!entry || typeof entry !== "object" || Array.isArray(entry)) {
      return {
        ok: false,
        failure: "invalid_arguments",
        message: `actions[${i}] must be an object.`,
      };
    }
    const step = /** @type {Record<string, unknown>} */ (entry);
    const doName = String(step.do ?? "").toLowerCase().trim();
    if (!ALLOWED_DOS.includes(doName)) {
      return {
        ok: false,
        failure: "invalid_arguments",
        message: `actions[${i}].do="${doName}" is not allowed. Allowed: ${ALLOWED_DOS.join(", ")}.`,
      };
    }
    // Refuse raw JS surfaces even if smuggled under other keys.
    if ("js" in step || "js_before_wait" in step || "evaluate" in step) {
      return {
        ok: false,
        failure: "invalid_arguments",
        message: `actions[${i}] must not include js/evaluate fields.`,
      };
    }

    const normalized = normalizeStep(doName, step, i);
    if (!normalized.ok) {
      return normalized;
    }
    actions.push(normalized.action);
  }

  return { ok: true, actions };
}

/**
 * @param {string} doName
 * @param {Record<string, unknown>} step
 * @param {number} index
 */
function normalizeStep(doName, step, index) {
  const timeout_ms =
    typeof step.timeout_ms === "number" && Number.isFinite(step.timeout_ms)
      ? Math.max(50, Math.min(Math.floor(step.timeout_ms), 60_000))
      : DEFAULT_ACTION_TIMEOUT_MS;

  switch (doName) {
    case "wait": {
      const ms =
        typeof step.ms === "number" && Number.isFinite(step.ms)
          ? Math.max(50, Math.min(Math.floor(step.ms), 60_000))
          : 500;
      return { ok: true, action: { do: "wait", ms, timeout_ms } };
    }
    case "wait_selector": {
      const selector = typeof step.selector === "string" ? step.selector.trim() : "";
      if (!selector) {
        return {
          ok: false,
          failure: "invalid_arguments",
          message: `actions[${index}].selector is required for wait_selector.`,
        };
      }
      return { ok: true, action: { do: "wait_selector", selector, timeout_ms } };
    }
    case "wait_text": {
      const text = typeof step.text === "string" ? step.text : "";
      if (!text) {
        return {
          ok: false,
          failure: "invalid_arguments",
          message: `actions[${index}].text is required for wait_text.`,
        };
      }
      return { ok: true, action: { do: "wait_text", text, timeout_ms } };
    }
    case "click": {
      const selector = typeof step.selector === "string" ? step.selector.trim() : "";
      const text = typeof step.text === "string" ? step.text : "";
      if (!selector && !text) {
        return {
          ok: false,
          failure: "invalid_arguments",
          message: `actions[${index}] click requires selector or text.`,
        };
      }
      return {
        ok: true,
        action: {
          do: "click",
          ...(selector ? { selector } : {}),
          ...(text ? { text } : {}),
          timeout_ms,
        },
      };
    }
    case "hover": {
      const selector = typeof step.selector === "string" ? step.selector.trim() : "";
      if (!selector) {
        return {
          ok: false,
          failure: "invalid_arguments",
          message: `actions[${index}].selector is required for hover.`,
        };
      }
      return { ok: true, action: { do: "hover", selector, timeout_ms } };
    }
    case "type": {
      const selector = typeof step.selector === "string" ? step.selector.trim() : "";
      if (!selector) {
        return {
          ok: false,
          failure: "invalid_arguments",
          message: `actions[${index}].selector is required for type.`,
        };
      }
      const text = typeof step.text === "string" ? step.text : "";
      return { ok: true, action: { do: "type", selector, text, timeout_ms } };
    }
    case "press": {
      const key = typeof step.key === "string" ? step.key.trim() : "";
      if (!key) {
        return {
          ok: false,
          failure: "invalid_arguments",
          message: `actions[${index}].key is required for press.`,
        };
      }
      return { ok: true, action: { do: "press", key, timeout_ms } };
    }
    case "scroll": {
      const toRaw = typeof step.to === "string" ? step.to.toLowerCase().trim() : "down";
      const to = ["top", "bottom", "down"].includes(toRaw) ? toRaw : "down";
      const px =
        typeof step.px === "number" && Number.isFinite(step.px)
          ? Math.max(1, Math.min(Math.floor(step.px), 10_000))
          : 400;
      return { ok: true, action: { do: "scroll", to, px, timeout_ms } };
    }
    default:
      return {
        ok: false,
        failure: "invalid_arguments",
        message: `actions[${index}].do is not allowed.`,
      };
  }
}

/** Redact secrets from a single action for logs/results (typed text never returned). */
export function redactAction(action) {
  if (!action || typeof action !== "object") {
    return action;
  }
  const copy = { ...action };
  if (typeof copy.text === "string" && copy.do === "type") {
    copy.text = "***";
    copy.text_len = action.text.length;
  }
  return copy;
}

export function redactActions(actions) {
  return (actions ?? []).map(redactAction);
}

/** Stable SHA-256 hex of canonical redacted plan (order-preserving). */
export function actionPlanHash(actions) {
  const redacted = redactActions(actions);
  const canonical = JSON.stringify(redacted);
  return createHash("sha256").update(canonical, "utf8").digest("hex");
}

/**
 * @param {import('playwright').Page} page
 * @param {object[]} actions
 * @param {{ deadlineMs?: number }} [options]
 */
export async function runMcpActions(page, actions, options = {}) {
  const deadlineMs =
    typeof options.deadlineMs === "number" && options.deadlineMs > 0
      ? options.deadlineMs
      : DEFAULT_DEADLINE_MS;
  const started = Date.now();
  /** @type {object[]} */
  const outcomes = [];

  for (let i = 0; i < actions.length; i++) {
    const remaining = deadlineMs - (Date.now() - started);
    if (remaining <= 0) {
      outcomes.push({
        index: i,
        do: actions[i].do,
        ok: false,
        failure: "deadline_exceeded",
        message: "overall action deadline exceeded before step started",
      });
      return {
        ok: false,
        failure: "action_failed",
        failed_index: i,
        steps_run: i,
        outcomes,
        action_plan_hash: actionPlanHash(actions),
        action_trace: redactActions(actions).map((a, idx) => ({
          ...a,
          ...(outcomes[idx] ?? { index: idx, ok: false }),
        })),
      };
    }

    const action = actions[i];
    const stepTimeout = Math.min(action.timeout_ms ?? DEFAULT_ACTION_TIMEOUT_MS, remaining);
    try {
      await runOneAction(page, action, stepTimeout);
      outcomes.push({ index: i, do: action.do, ok: true });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      outcomes.push({
        index: i,
        do: action.do,
        ok: false,
        failure: "action_failed",
        message: sanitizeErrorMessage(message),
      });
      return {
        ok: false,
        failure: "action_failed",
        failed_index: i,
        steps_run: i,
        outcomes,
        action_plan_hash: actionPlanHash(actions),
        action_trace: outcomes.map((o) => ({
          ...redactAction(actions[o.index]),
          ...o,
        })),
      };
    }
  }

  return {
    ok: true,
    failure: null,
    failed_index: null,
    steps_run: actions.length,
    outcomes,
    action_plan_hash: actionPlanHash(actions),
    action_trace: outcomes.map((o) => ({
      ...redactAction(actions[o.index]),
      ...o,
    })),
  };
}

/**
 * @param {import('playwright').Page} page
 * @param {object} action
 * @param {number} timeoutMs
 */
async function runOneAction(page, action, timeoutMs) {
  switch (action.do) {
    case "wait":
      await page.waitForTimeout(Math.min(action.ms ?? 500, timeoutMs));
      return;
    case "wait_selector":
      await page.waitForSelector(action.selector, { timeout: timeoutMs });
      return;
    case "wait_text":
      await page.getByText(action.text, { exact: false }).first().waitFor({
        state: "visible",
        timeout: timeoutMs,
      });
      return;
    case "click": {
      if (action.selector) {
        await page.locator(action.selector).first().click({ timeout: timeoutMs });
      } else {
        await page.getByText(action.text, { exact: false }).first().click({ timeout: timeoutMs });
      }
      return;
    }
    case "hover":
      await page.locator(action.selector).first().hover({ timeout: timeoutMs });
      return;
    case "type":
      await page.locator(action.selector).first().fill(action.text ?? "", { timeout: timeoutMs });
      return;
    case "press":
      await page.keyboard.press(action.key, { delay: 0 });
      return;
    case "scroll": {
      const to = action.to ?? "down";
      const px = action.px ?? 400;
      await page.evaluate(
        ({ to, px }) => {
          if (to === "top") {
            window.scrollTo(0, 0);
          } else if (to === "bottom") {
            window.scrollTo(0, document.documentElement.scrollHeight);
          } else {
            window.scrollBy(0, px);
          }
        },
        { to, px },
      );
      return;
    }
    default:
      throw new Error(`unsupported action ${action.do}`);
  }
}

function sanitizeErrorMessage(message) {
  // Drop long typed-text payloads that Playwright may echo in locator errors.
  return String(message).replace(/\s+/g, " ").slice(0, 240);
}
