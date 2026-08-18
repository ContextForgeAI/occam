/**
 * Preserve semantic identifiers inside pre/code before Readability.
 * Readability drops interactive wrappers (button/span/a), which can delete
 * the only copy of a code identifier. Operate only inside pre/code.
 *
 * Complexity: one query for pre/code roots, then descendant queries per root.
 * Nested code inside pre is skipped so wrappers are visited once.
 */

const TOOLTIP_SELECTOR = [
  '[role="tooltip"]',
  ".tooltip",
  ".twoslash-popup",
  '[class*="twoslash-popup"]',
  '[class*="tooltip"]',
  '[class*="hover-popup"]',
].join(",");

const PRESENTATION_CLASS_RE = /twoslash|shiki|hover|tooltip/i;
const IDENTIFIER_RE = /^[A-Za-z_][\w$]*(?:[.:][\w$]+)*$/;

export function preserveCodeWrappers(document) {
  if (!document?.querySelectorAll) {
    return document;
  }

  for (const root of document.querySelectorAll("pre, code")) {
    if (root.tagName === "CODE" && root.closest("pre") && root.closest("pre") !== root) {
      continue;
    }
    stripTooltipDescendants(root);
    unwrapButtons(root);
    unwrapSpansAndAnchors(root);
  }
  return document;
}

function unwrapButtons(root) {
  for (const button of [...root.querySelectorAll("button")]) {
    if (!root.contains(button)) {
      continue;
    }
    replaceWithVisibleLabel(button);
  }
}

function unwrapSpansAndAnchors(root) {
  const nodes = [...root.querySelectorAll("span, a")].reverse();
  for (const el of nodes) {
    if (!root.contains(el)) {
      continue;
    }
    if (shouldUnwrapSpanOrAnchor(el)) {
      const collapse = PRESENTATION_CLASS_RE.test(classAttribute(el));
      replaceWithVisibleLabel(el, { collapse });
    }
  }
}

function stripTooltipDescendants(el) {
  for (const tip of [...el.querySelectorAll(TOOLTIP_SELECTOR)]) {
    if (el.contains(tip)) {
      tip.remove();
    }
  }
}

function visibleLabel(el, { collapse = true } = {}) {
  stripTooltipDescendants(el);
  const raw = el.textContent ?? "";
  return collapse ? raw.replace(/\s+/g, " ").trim() : raw;
}

function replaceWithVisibleLabel(el, options) {
  const label = visibleLabel(el, options);
  el.replaceWith(el.ownerDocument.createTextNode(label));
}

function shouldUnwrapSpanOrAnchor(el) {
  if (el.matches(TOOLTIP_SELECTOR)) {
    return false;
  }
  const className = classAttribute(el);
  if (PRESENTATION_CLASS_RE.test(className)) {
    return true;
  }

  const clone = el.cloneNode(true);
  stripTooltipDescendants(clone);
  if (clone.querySelector("*")) {
    return false;
  }
  const text = (clone.textContent ?? "").trim();
  return IDENTIFIER_RE.test(text);
}

function classAttribute(el) {
  if (typeof el.className === "string") {
    return el.className;
  }
  return el.getAttribute("class") ?? "";
}
