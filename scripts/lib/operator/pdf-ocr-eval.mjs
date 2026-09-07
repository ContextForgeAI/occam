/**
 * A2 evaluation only: score current PDF / OCR / difficult-acquisition behavior.
 * Does not ship an OCR engine and does not change extract workers.
 */
export const EVAL_SCHEMA = "occam.capability-eval.pdf-ocr.v1";

/**
 * @param {string} haystack
 * @param {string[]} sequence
 * @returns {number} 0–1
 */
export function scoreReadingOrder(haystack, sequence) {
  const text = String(haystack ?? "");
  let from = 0;
  let found = 0;
  for (const token of sequence ?? []) {
    const index = text.indexOf(token, from);
    if (index === -1) continue;
    found += 1;
    from = index + token.length;
  }
  const total = (sequence ?? []).length;
  return total === 0 ? 0 : found / total;
}

/**
 * @param {string} haystack
 * @param {{ headers?: string[], rows?: string[][], requireMarkdownTable?: boolean }} expected
 */
export function scoreTables(haystack, expected) {
  const text = String(haystack ?? "");
  const cells = [
    ...(expected.headers ?? []),
    ...(expected.rows ?? []).flat(),
  ].filter(Boolean);
  if (cells.length === 0) return 0;
  const present = cells.filter((cell) => text.includes(cell)).length / cells.length;
  if (expected.requireMarkdownTable) {
    const hasTable = /\|.+\|/.test(text) && /-+:?\|/.test(text);
    return hasTable ? present : present * 0.35;
  }
  return present;
}

/**
 * @param {string} haystack
 * @param {Array<{ value: string, unit: string }>} pairs
 */
export function scoreUnits(haystack, pairs) {
  const text = String(haystack ?? "");
  const list = pairs ?? [];
  if (list.length === 0) return 0;
  let hits = 0;
  for (const pair of list) {
    const glued = `${pair.value}${pair.unit}`;
    const spaced = `${pair.value} ${pair.unit}`;
    if (text.includes(spaced) || text.includes(glued)) hits += 1;
  }
  return hits / list.length;
}

/**
 * @param {string} haystack
 * @param {{ expectSeparator?: boolean, mentions?: string[] }} expected
 */
export function scorePageRefs(haystack, expected) {
  const text = String(haystack ?? "");
  const parts = [];
  if (expected.expectSeparator !== false) {
    parts.push(/\n---\n/.test(text) ? 1 : 0);
  }
  for (const mention of expected.mentions ?? []) {
    parts.push(text.includes(mention) ? 1 : 0);
  }
  if (parts.length === 0) return 0;
  return parts.reduce((sum, n) => sum + n, 0) / parts.length;
}

/**
 * @param {Record<string, unknown>} observed
 * @param {Record<string, unknown>} expected
 */
export function scoreAcquisition(observed, expected) {
  const keys = Object.keys(expected ?? {});
  if (keys.length === 0) return 0;
  let hits = 0;
  for (const key of keys) {
    if (JSON.stringify(observed[key]) === JSON.stringify(expected[key])) hits += 1;
  }
  return hits / keys.length;
}

/**
 * @param {{
 *   id: string,
 *   dimension: string,
 *   actual?: string,
 *   observed?: Record<string, unknown>,
 *   expected: Record<string, unknown>,
 * }} item
 */
export function scoreCase(item) {
  const actual = item.actual ?? "";
  let score = 0;
  switch (item.dimension) {
    case "reading_order":
      score = scoreReadingOrder(actual, item.expected.sequence ?? []);
      break;
    case "tables":
      score = scoreTables(actual, item.expected);
      break;
    case "units":
      score = scoreUnits(actual, item.expected.pairs ?? []);
      break;
    case "page_refs":
      score = scorePageRefs(actual, item.expected);
      break;
    case "acquisition":
      score = scoreAcquisition(item.observed ?? {}, item.expected);
      break;
    default:
      score = 0;
  }
  return {
    id: item.id,
    dimension: item.dimension,
    score: Number(score.toFixed(3)),
  };
}

/**
 * @param {Array<Parameters<typeof scoreCase>[0]>} cases
 */
export function evaluateCases(cases) {
  const results = cases.map(scoreCase);
  const byDimension = {};
  for (const row of results) {
    if (!byDimension[row.dimension]) byDimension[row.dimension] = [];
    byDimension[row.dimension].push(row.score);
  }
  const dimensions = {};
  for (const [name, values] of Object.entries(byDimension)) {
    dimensions[name] = Number(
      (values.reduce((sum, n) => sum + n, 0) / values.length).toFixed(3),
    );
  }
  return {
    schema: EVAL_SCHEMA,
    results,
    dimensions,
  };
}

export const IMPLEMENTATION_DECISION = {
  shipBundledOcr: false,
  keepTextLayerUnpdf: true,
  keepOptInOcrHelper: true,
  estimatesDays: {
    tableAwarePdf: { lo: 8, hi: 12, defer: true },
    bundledOcrEngine: { lo: 18, hi: 25, defer: true },
    mapIncludePdfLinks: { lo: 2, hi: 3, defer: true },
  },
  rationale: [
    "Text-layer PDFs already extract via unpdf with page separators.",
    "Tables and drawing-only units are not reconstructed.",
    "Empty text layers fail honestly; OCR stays operator-supplied.",
    "Map drops .pdf as an asset; fetch a PDF URL with transcode instead.",
  ],
};
