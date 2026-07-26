import { JSDOM, VirtualConsole } from "jsdom";

/** Thrown when a schema field uses attr=nuxt — page JS must never execute in Node (EF-013). */
export class NuxtAttrDisabledError extends Error {
  constructor() {
    super("nuxt_attr_disabled");
    this.name = "NuxtAttrDisabledError";
    this.code = "nuxt_attr_disabled";
  }
}

/**
 * Flat field extract + optional row mode (shared with css-extract worker).
 * @param {string} html
 * @param {string} finalUrl
 * @param {Record<string, unknown>} spec
 */
export function extractStructuredData(html, finalUrl, spec) {
  const virtualConsole = new VirtualConsole();
  virtualConsole.on("jsdomError", () => {});
  const dom = new JSDOM(html, { url: finalUrl, virtualConsole });
  const doc = dom.window.document;

  const baseSelector =
    typeof spec.base_selector === "string"
      ? spec.base_selector
      : typeof spec.baseSelector === "string"
        ? spec.baseSelector
        : null;
  const fields = spec.fields ?? spec;

  if (baseSelector) {
    return extractRows(doc, html, finalUrl, baseSelector, fields);
  }

  return extractFlat(doc, html, finalUrl, fields);
}

/**
 * @param {Document} doc
 * @param {string} html
 * @param {string} finalUrl
 * @param {Record<string, unknown>} fields
 */
function extractFlat(doc, html, finalUrl, fields) {
  const data = {};
  for (const [name, fieldSpec] of Object.entries(fields)) {
    data[name] = extractField(doc, html, finalUrl, /** @type {Record<string, unknown>} */ (fieldSpec));
  }
  return data;
}

/**
 * @param {Document} doc
 * @param {string} html
 * @param {string} finalUrl
 * @param {string} baseSelector
 * @param {Record<string, unknown>} fields
 */
function extractRows(doc, html, finalUrl, baseSelector, fields) {
  const bases = [...doc.querySelectorAll(baseSelector)];
  const rows = bases
    .map((base) => {
      const row = {};
      for (const [name, fieldSpec] of Object.entries(fields)) {
        const spec = /** @type {Record<string, unknown>} */ (fieldSpec);
        const attr = spec.attr ?? "text";
        if (attr === "nuxt" || attr === "regex" || attr === "const") {
          row[name] = extractField(doc, html, finalUrl, spec);
          continue;
        }

        const selector = String(spec.selector ?? "");
        const nodes = selector ? [...base.querySelectorAll(selector)] : [];
        const values = nodes
          .map((node) => readValue(node, attr))
          .filter((value) => value !== null && value !== "");
        row[name] = spec.multiple ? values : (values[0] ?? null);
      }
      return row;
    })
    .filter((row) => Object.values(row).some((value) => value !== null && value !== ""));

  return { rows, row_count: rows.length };
}

/**
 * @param {Document} doc
 * @param {string} html
 * @param {string} finalUrl
 * @param {Record<string, unknown>} spec
 */
function extractField(doc, html, finalUrl, spec) {
  const attr = spec.attr ?? "text";
  if (attr === "nuxt") {
    // EF-013: never eval page-controlled __NUXT__ in Node. Typed failure for callers.
    throw new NuxtAttrDisabledError();
  }
  if (attr === "regex") {
    return applyDivide(readHtmlRegex(html, finalUrl, String(spec.selector ?? "")), spec.divide);
  }
  if (attr === "const") {
    return spec.selector ?? null;
  }

  const nodes = [...doc.querySelectorAll(String(spec.selector ?? ""))];
  const values = nodes
    .map((node) => readValue(node, attr))
    .filter((value) => value !== null && value !== "");
  return spec.multiple ? values : (values[0] ?? null);
}

function applyDivide(value, divide) {
  if (value == null || !divide || divide <= 1) {
    return value;
  }

  const num = Number(String(value).replace(",", "."));
  if (Number.isNaN(num)) {
    return value;
  }

  return formatDecimal(num / divide);
}

function formatDecimal(n) {
  if (Number.isInteger(n)) {
    return String(n);
  }

  return n.toFixed(2).replace(/\.?0+$/, "");
}

function readHtmlRegex(html, pageUrl, patternTemplate) {
  const id = pageUrl.match(/\/item\/(\d+)/)?.[1] ?? "";
  const pattern = patternTemplate.replaceAll("{id}", id);
  const match = html.match(new RegExp(pattern, "s"));
  return match?.[1]?.trim() ?? null;
}

/**
 * @param {Element} node
 * @param {string} attr
 */
function readValue(node, attr) {
  if (attr === "text") {
    return (node.textContent ?? "").replace(/\s+/g, " ").trim();
  }

  if (attr === "html") {
    return (node.innerHTML ?? "").trim();
  }

  if (attr === "href" || attr === "src") {
    return node.getAttribute(attr);
  }

  return node.getAttribute(attr);
}
