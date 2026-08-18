/**
 * Preserve semantic identifiers inside pre/code before Readability.
 * Readability drops interactive wrappers (button/span/a), which can delete
 * the only copy of a code identifier. Operate only inside pre/code.
 *
 * Complexity: one query for pre/code roots, then descendant queries per root.
 * Nested code inside pre is skipped so wrappers are visited once.
 *
 * Tooltip invariant: remove PAYLOAD / POPUP nodes, never the HOST / TRIGGER.
 * - Payload: role=tooltip, or a content-ish class token (tooltiptext,
 *   tooltip-text, tooltip-content, tooltip-popup, tooltip-bubble, tooltip-body,
 *   twoslash-popup…, hover-popup content).
 * - Host: tooltip-trigger / tooltip-host / has-tooltip / tooltip-wrapper, or
 *   an ambiguous token `tooltip` that wraps a nested payload or an
 *   identifier-shaped label.
 * - Ambiguous `.tooltip` leaf with prose (typical child of a button) is payload.
 * Do not treat class*="tooltip" as delete — that erases tooltip-trigger hosts.
 */

const IDENTIFIER_RE = /^[A-Za-z_][\w$]*(?:[.:][\w$]+)*$/;
const PRESENTATION_CLASS_RE = /twoslash|shiki|hover|tooltip/i;

/** Content / popup class tokens — always payload. */
const PAYLOAD_TOKEN_RE =
  /^(?:tooltiptext|tooltip-text|tooltip-content|tooltip-popup|tooltip-bubble|tooltip-body|twoslash-popup(?:-.*)?|hover-popup(?:-(?:content|text|body|bubble|panel))?)$/i;

/** Trigger / container class tokens — never payload. */
const HOST_TOKEN_RE = /^(?:tooltip-trigger|tooltip-host|has-tooltip|tooltip-wrapper)$/i;

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
    if (isTooltipPayload(el)) {
      el.remove();
      continue;
    }
    if (shouldUnwrapSpanOrAnchor(el)) {
      const collapse = PRESENTATION_CLASS_RE.test(classAttribute(el));
      replaceWithVisibleLabel(el, { collapse });
    }
  }
}

function stripTooltipDescendants(el) {
  const nodes = [...el.querySelectorAll("*")].reverse();
  for (const node of nodes) {
    if (!el.contains(node) || node === el) {
      continue;
    }
    if (isTooltipPayload(node)) {
      node.remove();
    }
  }
}

/**
 * Payload vs host: role=tooltip and content-ish tokens are payload.
 * Host tokens are kept. Exact class `tooltip` is a host when it contains a
 * nested payload or identifier-shaped label; otherwise a prose leaf is payload.
 */
function isTooltipPayload(el) {
  if (!el?.getAttribute) {
    return false;
  }
  if (el.getAttribute("role") === "tooltip") {
    return true;
  }
  const tokens = classTokens(el);
  if (tokens.some((t) => PAYLOAD_TOKEN_RE.test(t))) {
    return true;
  }
  if (tokens.some((t) => HOST_TOKEN_RE.test(t))) {
    return false;
  }
  if (!tokens.some((t) => t.toLowerCase() === "tooltip")) {
    return false;
  }
  if (hasPayloadDescendant(el)) {
    return false;
  }
  const label = (el.textContent ?? "").replace(/\s+/g, " ").trim();
  return !IDENTIFIER_RE.test(label);
}

function hasPayloadDescendant(el) {
  for (const child of el.querySelectorAll("*")) {
    if (child.getAttribute("role") === "tooltip") {
      return true;
    }
    if (classTokens(child).some((t) => PAYLOAD_TOKEN_RE.test(t))) {
      return true;
    }
  }
  return false;
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
  if (isTooltipPayload(el)) {
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

function classTokens(el) {
  return classAttribute(el).split(/\s+/).filter(Boolean);
}

function classAttribute(el) {
  if (typeof el.className === "string") {
    return el.className;
  }
  return el.getAttribute("class") ?? "";
}
