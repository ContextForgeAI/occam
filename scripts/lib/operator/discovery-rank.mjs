/**
 * A1 spike: cheap lexical + extractability ranking for discovered URLs.
 * Does not change occam_search default order. Provider identity stays on the hit.
 */

const DOC_PATH = /\/(docs?|guide|reference|manual|handbook|learn)\b/i;

/**
 * @param {string | null | undefined} text
 * @returns {string[]}
 */
export function tokenize(text) {
  return String(text ?? "")
    .toLowerCase()
    .split(/[^a-z0-9]+/i)
    .filter((token) => token.length >= 2);
}

/**
 * @param {string} query
 * @param {{ url?: string, title?: string, snippet?: string, extractability?: number | null }} hit
 */
export function scoreHit(query, hit) {
  const q = tokenize(query);
  if (q.length === 0) {
    return Number.isFinite(hit.extractability) ? Number(hit.extractability) : 0.5;
  }
  const hay = tokenize(`${hit.title ?? ""} ${hit.snippet ?? ""} ${hit.url ?? ""}`);
  const haySet = new Set(hay);
  let hits = 0;
  for (const token of q) {
    if (haySet.has(token)) hits += 1;
  }
  const lexical = hits / q.length;
  const extractability = Number.isFinite(hit.extractability) ? Number(hit.extractability) : 0.5;
  const pathBonus = DOC_PATH.test(String(hit.url ?? "")) ? 0.08 : 0;
  return Math.min(1, 0.55 * lexical + 0.37 * extractability + pathBonus);
}

/**
 * Stable rank: score desc, then original index.
 * @param {string} query
 * @param {Array<{ url?: string, title?: string, snippet?: string, extractability?: number | null }>} hits
 */
export function rankHits(query, hits) {
  return hits
    .map((hit, index) => ({ hit, index, score: scoreHit(query, hit) }))
    .sort((a, b) => b.score - a.score || a.index - b.index)
    .map((row) => ({ ...row.hit, relevance: Number(row.score.toFixed(4)) }));
}

/**
 * @param {Array<{ useful?: boolean }>} ranked
 * @param {number} k
 */
export function precisionAtK(ranked, k) {
  const slice = ranked.slice(0, k);
  if (slice.length === 0) return 0;
  return slice.filter((hit) => hit.useful === true).length / slice.length;
}

/**
 * @param {Array<{ useful?: boolean }>} ranked
 * @param {number} k
 */
export function recallAtK(ranked, k) {
  const useful = ranked.filter((hit) => hit.useful === true).length;
  if (useful === 0) return 0;
  return ranked.slice(0, k).filter((hit) => hit.useful === true).length / useful;
}

/**
 * 1-based rank of the first useful hit. Infinity when none.
 * @param {Array<{ useful?: boolean }>} ranked
 */
export function timeToUsefulSource(ranked) {
  const index = ranked.findIndex((hit) => hit.useful === true);
  return index === -1 ? Number.POSITIVE_INFINITY : index + 1;
}

/**
 * @param {{ query: string, hits: Array<Record<string, unknown>> }} task
 * @param {number} [k]
 */
export function evaluateTask(task, k = 3) {
  const baseline = task.hits.map((hit) => ({ ...hit }));
  const ranked = rankHits(task.query, task.hits).map((hit, index) => ({
    ...hit,
    useful: task.hits.find((raw) => raw.url === hit.url)?.useful === true,
    rank: index + 1,
  }));
  return {
    id: task.id,
    k,
    baseline: {
      precision: precisionAtK(baseline, k),
      recall: recallAtK(baseline, k),
      timeToUseful: timeToUsefulSource(baseline),
    },
    ranked: {
      precision: precisionAtK(ranked, k),
      recall: recallAtK(ranked, k),
      timeToUseful: timeToUsefulSource(ranked),
    },
    provider: task.provider ?? "fixture",
  };
}
