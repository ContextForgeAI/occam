/**
 * P10-E: virtual scroll — append (plateau) and replace (chunk merge + dedupe).
 * Disable with WT_VIRTUAL_SCROLL=0.
 */

const DEFAULT_STEP_PX = 600;
const DEFAULT_MAX_ROUNDS = 12;
const DEFAULT_WAIT_MS = 350;

/**
 * @typedef {"append" | "replace" | "auto" | "disabled"} VirtualScrollMode
 */

/**
 * @typedef {object} VirtualScrollConfig
 * @property {VirtualScrollMode} [mode]
 * @property {string} [container_selector]
 * @property {string} [item_selector]
 * @property {number} [scroll_count]
 * @property {number} [wait_ms]
 * @property {number} [step_px]
 * @property {number} [max_rounds]
 */

/**
 * @param {import('playwright').Page} page
 * @param {VirtualScrollConfig} [options]
 */
export async function runVirtualScroll(page, options = {}) {
  const disabled = isVirtualScrollDisabled();
  if (disabled) {
    await page.evaluate(() => window.scrollTo(0, Math.min(600, document.body?.scrollHeight ?? 600)));
    return {
      mode: "disabled",
      rounds: 0,
      finalHeight: 0,
      chunks_merged: 0,
      unique_items: 0,
      disabled: true,
    };
  }

  const config = normalizeVirtualScrollConfig(options);
  if (config.mode === "replace") {
    return virtualScrollReplaceMerge(page, config);
  }

  if (config.mode === "auto") {
    const replace = await detectReplaceScenario(page, config);
    if (replace) {
      return virtualScrollReplaceMerge(page, { ...config, mode: "replace" });
    }
  }

  const plateau = await virtualScrollUntilPlateau(page, config);
  return {
    mode: "append",
    rounds: plateau.rounds,
    finalHeight: plateau.finalHeight,
    chunks_merged: 0,
    unique_items: 0,
    disabled: false,
  };
}

/** @deprecated Use runVirtualScroll — kept for direct imports */
export async function virtualScrollUntilPlateau(page, options = {}) {
  const config = normalizeVirtualScrollConfig(options);
  const step = config.step_px ?? DEFAULT_STEP_PX;
  const maxRounds = config.max_rounds ?? config.scroll_count ?? DEFAULT_MAX_ROUNDS;
  const waitMs = config.wait_ms ?? DEFAULT_WAIT_MS;
  let lastHeight = 0;
  let stable = 0;
  let rounds = 0;

  for (let i = 0; i < maxRounds; i++) {
    rounds = i + 1;
    const height = await page.evaluate(() => document.body?.scrollHeight ?? 0);
    if (height > 0 && height <= lastHeight) {
      stable++;
      if (stable >= 2) {
        break;
      }
    } else {
      stable = 0;
      lastHeight = height;
    }

    const y = Math.min((i + 1) * step, Math.max(height, step));
    await page.evaluate((scrollY) => window.scrollTo(0, scrollY), y);
    await page.waitForTimeout(waitMs);
  }

  const finalHeight = await page.evaluate(() => document.body?.scrollHeight ?? 0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.waitForTimeout(200);
  return { rounds, finalHeight, disabled: false };
}

/**
 * @param {import('playwright').Page} page
 * @param {VirtualScrollConfig} config
 */
async function virtualScrollReplaceMerge(page, config) {
  const containerSelector =
    config.container_selector ?? (await findScrollableListContainer(page));
  if (!containerSelector) {
    const plateau = await virtualScrollUntilPlateau(page, config);
    return {
      mode: "append",
      rounds: plateau.rounds,
      finalHeight: plateau.finalHeight,
      chunks_merged: 0,
      unique_items: 0,
      disabled: false,
    };
  }

  const itemSelector = config.item_selector ?? ".item, [data-item], [data-id], li";
  const maxRounds = Math.max(1, config.scroll_count ?? config.max_rounds ?? 8);
  const waitMs = config.wait_ms ?? DEFAULT_WAIT_MS;
  const merged = new Map();
  let chunksMerged = 0;
  let lastFingerprint = "";

  for (let round = 0; round < maxRounds; round++) {
    const snapshot = await captureContainerItems(page, containerSelector, itemSelector);
    let added = 0;
    for (const item of snapshot.items) {
      const key = item.key || item.text;
      if (!merged.has(key)) {
        merged.set(key, item.text);
        added++;
      }
    }

    const fingerprint = snapshot.items.map((it) => it.key).join("|");
    if (fingerprint && fingerprint !== lastFingerprint) {
      chunksMerged++;
      lastFingerprint = fingerprint;
    }

    if (round === maxRounds - 1) {
      break;
    }

    await scrollContainer(page, containerSelector, config.step_px ?? DEFAULT_STEP_PX);
    await page.waitForTimeout(waitMs);
  }

  const uniqueItems = [...merged.values()];
  if (uniqueItems.length > 0) {
    await injectMergedItems(page, uniqueItems);
  }

  const finalHeight = await page.evaluate(() => document.body?.scrollHeight ?? 0);
  return {
    mode: "replace",
    rounds: maxRounds,
    finalHeight,
    chunks_merged: chunksMerged,
    unique_items: uniqueItems.length,
    disabled: false,
  };
}

/**
 * @param {import('playwright').Page} page
 * @param {VirtualScrollConfig} config
 */
async function detectReplaceScenario(page, config) {
  const containerSelector =
    config.container_selector ?? (await findScrollableListContainer(page));
  if (!containerSelector) {
    return false;
  }

  const itemSelector = config.item_selector ?? ".item, [data-item], [data-id], li";
  const waitMs = config.wait_ms ?? DEFAULT_WAIT_MS;
  const before = await captureContainerItems(page, containerSelector, itemSelector);
  if (before.items.length < 2) {
    return false;
  }

  await scrollContainer(page, containerSelector, config.step_px ?? DEFAULT_STEP_PX);
  await page.waitForTimeout(waitMs);
  const after = await captureContainerItems(page, containerSelector, itemSelector);
  if (after.items.length < 2) {
    return false;
  }

  const heightStable = Math.abs(after.height - before.height) <= 8;
  const sameSlotCount =
    before.items.length === after.items.length && before.items.length > 0;
  const keysChanged = after.items.some((it, idx) => before.items[idx]?.key !== it.key);

  return heightStable && sameSlotCount && keysChanged;
}

/**
 * @param {import('playwright').Page} page
 */
async function findScrollableListContainer(page) {
  return page.evaluate(() => {
    const candidates = Array.from(document.querySelectorAll("*")).filter((el) => {
      const style = window.getComputedStyle(el);
      const overflowY = style.overflowY;
      const scrollable = overflowY === "auto" || overflowY === "scroll";
      return scrollable && el.scrollHeight > el.clientHeight + 8 && el.children.length >= 2;
    });
    candidates.sort((a, b) => b.children.length - a.children.length);
    const best = candidates[0];
    if (!best) {
      return null;
    }
    if (best.id) {
      return `#${best.id}`;
    }
    if (best.classList.length > 0) {
      return `.${best.classList[0]}`;
    }
    return best.tagName.toLowerCase();
  });
}

/**
 * @param {import('playwright').Page} page
 * @param {string} containerSelector
 * @param {string} itemSelector
 */
async function captureContainerItems(page, containerSelector, itemSelector) {
  return page.evaluate(
    ({ containerSelector, itemSelector }) => {
      const root = document.querySelector(containerSelector);
      if (!root) {
        return { items: [], height: 0, childCount: 0 };
      }

      const nodes = itemSelector
        ? Array.from(root.querySelectorAll(itemSelector))
        : Array.from(root.children);

      const items = nodes
        .map((node) => {
          const text = (node.textContent ?? "").replace(/\s+/g, " ").trim();
          const key =
            node.getAttribute("data-id")
            ?? node.getAttribute("data-item")
            ?? node.id
            ?? text.slice(0, 120);
          return { key, text };
        })
        .filter((item) => item.text.length > 0);

      return {
        items,
        height: root.scrollHeight,
        childCount: nodes.length,
      };
    },
    { containerSelector, itemSelector },
  );
}

/**
 * @param {import('playwright').Page} page
 * @param {string} containerSelector
 * @param {number} stepPx
 */
async function scrollContainer(page, containerSelector, stepPx) {
  await page.evaluate(
    ({ containerSelector, stepPx }) => {
      const el = document.querySelector(containerSelector);
      if (!el) {
        window.scrollBy(0, stepPx);
        return;
      }
      const maxTop = el.scrollHeight - el.clientHeight;
      const next = Math.min(el.scrollTop + stepPx, maxTop);
      el.scrollTop = next >= maxTop - 2 ? maxTop : next;
      el.dispatchEvent(new Event("scroll"));
    },
    { containerSelector, stepPx },
  );
}

/**
 * @param {import('playwright').Page} page
 * @param {string[]} mergedItems
 */
async function injectMergedItems(page, mergedItems) {
  await page.evaluate((items) => {
    let mount = document.getElementById("wt-virtual-scroll-merge");
    if (!mount) {
      mount = document.createElement("section");
      mount.id = "wt-virtual-scroll-merge";
      mount.setAttribute("data-wt-virtual-scroll", "merged");
      document.body.prepend(mount);
    }

    mount.innerHTML = items
      .map((text, idx) => {
        const safe = text
          .replace(/&/g, "&amp;")
          .replace(/</g, "&lt;")
          .replace(/>/g, "&gt;");
        return `<article class="wt-virtual-scroll-item" data-idx="${idx}"><p>${safe}</p></article>`;
      })
      .join("\n");
  }, mergedItems);
}

function isVirtualScrollDisabled() {
  const disabled = String(process.env.WT_VIRTUAL_SCROLL ?? "1").toLowerCase();
  return disabled === "0" || disabled === "false" || disabled === "off";
}

/** @param {VirtualScrollConfig | Record<string, unknown> | null | undefined} raw */
export function normalizeVirtualScrollConfig(raw) {
  if (!raw || typeof raw !== "object") {
    return {};
  }

  const config = /** @type {Record<string, unknown>} */ (raw);
  const modeRaw = String(config.mode ?? "auto").toLowerCase();
  const mode = ["append", "replace", "auto"].includes(modeRaw) ? modeRaw : "auto";

  const scrollCount = clampInt(config.scroll_count ?? config.max_rounds, 1, 40, DEFAULT_MAX_ROUNDS);
  const waitMs = clampInt(config.wait_ms, 50, 5000, DEFAULT_WAIT_MS);
  const stepPx = clampInt(config.step_px, 100, 4000, DEFAULT_STEP_PX);

  return {
    mode,
    container_selector:
      typeof config.container_selector === "string"
        ? config.container_selector.trim()
        : typeof config.container === "string"
          ? config.container.trim()
          : undefined,
    item_selector:
      typeof config.item_selector === "string" ? config.item_selector.trim() : undefined,
    scroll_count: scrollCount,
    max_rounds: scrollCount,
    wait_ms: waitMs,
    step_px: stepPx,
  };
}

/** @param {unknown} value @param {number} min @param {number} max @param {number} fallback */
function clampInt(value, min, max, fallback) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    return fallback;
  }
  return Math.max(min, Math.min(max, Math.round(parsed)));
}
