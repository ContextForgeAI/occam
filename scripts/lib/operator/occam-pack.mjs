/**
 * Context Pack assembler — no second extractor.
 * Turns occam_search / occam_transcode / occam_digest payloads into a folder:
 * manifest, sources, excerpts, omissions. Failures are preserved.
 */

export const PACK_SCHEMA = "occam.context-pack.v1";
export const TOKEN_ESTIMATOR = "heuristic-unicode-v1";

/** @param {string | null | undefined} text */
export function estimateTokens(text) {
  if (!text) return 0;
  return Math.ceil([...String(text)].length / 4);
}

/**
 * Model-consumable pack serialization:
 * - content = excerpts.txt (the text a model reads)
 * - wrapper = pretty-printed task/settings/sources/omissions JSON
 *   (the sidecar files minus computed budget fields)
 * @param {{
 *   task: string,
 *   settings?: Record<string, unknown>,
 *   sources?: unknown[],
 *   omissions?: unknown[],
 * }} input
 */
export function estimatePackWrapperTokens(input) {
  return estimateTokens(JSON.stringify({
    schema: PACK_SCHEMA,
    task: input.task,
    settings: input.settings ?? {},
    sources: input.sources ?? [],
    omissions: input.omissions ?? [],
  }, null, 2));
}

/**
 * Reserve wrapper cost, then split the remaining declared budget across sources.
 * Does not truncate excerpts. Impossible when wrapper alone exceeds --budget.
 * @param {{
 *   declared: number,
 *   task: string,
 *   settings?: Record<string, unknown>,
 *   urls?: string[],
 *   sourceCount?: number,
 * }} input
 */
export function planPackBudget(input) {
  const urls = Array.isArray(input.urls) ? input.urls.filter(Boolean) : [];
  const n = Math.max(urls.length, Number(input.sourceCount) || 0, 1);
  const stubSources = (urls.length ? urls : Array.from({ length: n }, () => "")).map((url, i) => ({
    kind: "page",
    url: url || `pending://${i + 1}`,
    ok: true,
  }));
  const wrapper = estimatePackWrapperTokens({
    task: input.task,
    settings: input.settings ?? {},
    sources: stubSources,
    omissions: [],
  });
  const remaining = input.declared - wrapper;
  if (remaining <= 0) {
    return {
      possible: false,
      declared: input.declared,
      wrapper,
      content: 0,
      perSource: 0,
      reason: "wrapper_exceeds_budget",
    };
  }
  return {
    possible: true,
    declared: input.declared,
    wrapper,
    content: remaining,
    perSource: Math.max(1, Math.floor(remaining / n)),
    reason: null,
  };
}

/**
 * @param {string[]} argv
 */
export function parsePackArgs(argv) {
  const flags = {
    help: false,
    json: false,
    task: null,
    urls: [],
    search: null,
    maxSources: 4,
    budget: null,
    focus: null,
    backend: null,
    out: null,
    fromJson: null,
    error: null,
  };
  const args = [...argv];
  while (args.length) {
    const token = args.shift();
    if (token === "--") {
      flags.urls.push(...args.filter(Boolean));
      break;
    }
    if (token === "-h" || token === "--help") {
      flags.help = true;
      continue;
    }
    if (token === "--json") {
      flags.json = true;
      continue;
    }
    if (token === "--task" || token.startsWith("--task=")) {
      flags.task = token.includes("=") ? token.slice("--task=".length) : args.shift();
      if (!flags.task) flags.error = "missing --task value";
      continue;
    }
    if (token === "--url" || token.startsWith("--url=")) {
      const url = token.includes("=") ? token.slice("--url=".length) : args.shift();
      if (!url) flags.error = "missing --url value";
      else flags.urls.push(url);
      continue;
    }
    if (token === "--search" || token.startsWith("--search=")) {
      flags.search = token.includes("=") ? token.slice("--search=".length) : args.shift();
      if (!flags.search) flags.error = "missing --search value";
      continue;
    }
    if (token === "--max-sources" || token.startsWith("--max-sources=")) {
      const raw = token.includes("=") ? token.slice("--max-sources=".length) : args.shift();
      const n = Number(raw);
      if (!Number.isInteger(n) || n < 1 || n > 8) flags.error = "--max-sources must be an integer 1–8";
      else flags.maxSources = n;
      continue;
    }
    if (token === "--budget" || token.startsWith("--budget=")) {
      const raw = token.includes("=") ? token.slice("--budget=".length) : args.shift();
      const n = Number(raw);
      if (!Number.isInteger(n) || n < 128) flags.error = "--budget must be an integer >= 128";
      else flags.budget = n;
      continue;
    }
    if (token === "--focus" || token.startsWith("--focus=")) {
      flags.focus = token.includes("=") ? token.slice("--focus=".length) : args.shift();
      if (!flags.focus) flags.error = "missing --focus value";
      continue;
    }
    if (token === "--backend" || token.startsWith("--backend=")) {
      flags.backend = token.includes("=") ? token.slice("--backend=".length) : args.shift();
      if (!["http", "browser", "http_then_browser", "http-then-browser"].includes(flags.backend ?? "")) {
        flags.error = "--backend must be http, browser, or http_then_browser";
      }
      continue;
    }
    if (token === "--out" || token.startsWith("--out=")) {
      flags.out = token.includes("=") ? token.slice("--out=".length) : args.shift();
      if (!flags.out) flags.error = "missing --out value";
      continue;
    }
    if (token === "--from-json" || token.startsWith("--from-json=")) {
      flags.fromJson = token.includes("=") ? token.slice("--from-json=".length) : args.shift();
      if (!flags.fromJson) flags.error = "missing --from-json value";
      continue;
    }
    if (token.startsWith("-")) {
      flags.error = `unknown flag ${token}`;
      continue;
    }
    flags.urls.push(token);
  }

  if (!flags.help && !flags.error) {
    if (!flags.task) flags.error = "missing --task";
    else if (!flags.fromJson && !flags.search && flags.urls.length === 0) {
      flags.error = "need --url, --search, or --from-json";
    }
  }
  return flags;
}

/**
 * @param {{
 *   task: string,
 *   settings?: Record<string, unknown>,
 *   toolchain?: string,
 *   createdAt?: string,
 *   responses: Array<{ tool: string, payload: unknown }>,
 *   inability?: { reason?: string } | null,
 *   allocatedPerSource?: number | null,
 * }} input
 */
export function assemblePack(input) {
  const sources = [];
  const omissions = [];
  const excerptParts = [`# Context pack\n\nTask: ${input.task}\n`];
  const toolsUsed = [];

  for (const step of input.responses ?? []) {
    if (!step || typeof step !== "object") continue;
    const tool = String(step.tool ?? "");
    if (tool && !toolsUsed.includes(tool)) toolsUsed.push(tool);
    const payload = step.payload && typeof step.payload === "object"
      ? /** @type {Record<string, unknown>} */ (step.payload)
      : {};
    if (tool === "occam_search") {
      collectSearch(payload, sources, excerptParts);
      continue;
    }
    if (tool === "occam_digest") {
      collectDigest(payload, sources, omissions, excerptParts);
      continue;
    }
    collectTranscode(payload, sources, omissions, excerptParts);
  }

  const excerpts = excerptParts.join("\n").trim() + "\n";
  const settings = input.settings && typeof input.settings === "object" ? input.settings : {};
  const declared = Number(settings.budget) > 0 ? Number(settings.budget) : null;
  const contentTokens = estimateTokens(excerpts);
  const wrapperTokens = estimatePackWrapperTokens({
    task: input.task,
    settings,
    sources,
    omissions,
  });
  const total = contentTokens + wrapperTokens;
  const failed = sources.filter((s) => s.ok === false).length;
  const succeeded = sources.filter((s) => s.ok === true).length;
  const inability = input.inability && typeof input.inability === "object" ? input.inability : null;
  const overBudget = declared != null ? total > declared : false;
  const reason = inability?.reason
    ?? (overBudget ? "over_budget" : null);

  return {
    manifest: {
      schema: PACK_SCHEMA,
      task: input.task,
      createdAt: input.createdAt ?? new Date().toISOString(),
      toolchain: input.toolchain ?? "ff-occam",
      settings,
      toolsUsed,
      sourceCount: sources.length,
      succeeded,
      failed,
      budget: {
        declared,
        content: contentTokens,
        wrapper: wrapperTokens,
        total,
        estimator: TOKEN_ESTIMATOR,
        overBudget,
        allocatedPerSource: input.allocatedPerSource ?? null,
        reason,
      },
      ok: failed === 0 && succeeded > 0 && !overBudget && !inability,
    },
    sources,
    omissions,
    excerpts,
  };
}

/**
 * @param {ReturnType<typeof assemblePack>} pack
 */
export function packFiles(pack) {
  return {
    "manifest.json": `${JSON.stringify(pack.manifest, null, 2)}\n`,
    "sources.json": `${JSON.stringify(pack.sources, null, 2)}\n`,
    "omissions.json": `${JSON.stringify(pack.omissions, null, 2)}\n`,
    "excerpts.txt": pack.excerpts,
  };
}

function asRecord(value) {
  return value && typeof value === "object" ? /** @type {Record<string, unknown>} */ (value) : {};
}

function urlOf(payload) {
  const url = payload.url;
  if (typeof url === "string") return url;
  if (url && typeof url === "object") {
    const row = /** @type {Record<string, unknown>} */ (url);
    if (typeof row.url === "string") return row.url;
    if (typeof row.finalUrl === "string") return row.finalUrl;
  }
  return "";
}

function collectSearch(payload, sources, excerptParts) {
  const results = Array.isArray(payload.results) ? payload.results : [];
  excerptParts.push("## Discovery\n");
  if (payload.ok === false) {
    const failure = asRecord(payload.failure);
    sources.push({
      kind: "search",
      ok: false,
      query: payload.query ?? null,
      failureCode: failure.code ?? payload.failureCode ?? "search_failed",
      message: failure.message ?? payload.message ?? "",
    });
    excerptParts.push(`Search failed: ${failure.code ?? "search_failed"}\n`);
    return;
  }
  for (const item of results) {
    const hit = asRecord(item);
    sources.push({
      kind: "search",
      ok: true,
      id: hit.id ?? null,
      title: hit.title ?? null,
      url: hit.url ?? "",
      provider: hit.provider ?? null,
    });
    excerptParts.push(`- ${hit.id ?? "?"}\t${hit.title ?? ""}\t${hit.url ?? ""}\n`);
  }
}

function collectTranscode(payload, sources, omissions, excerptParts) {
  const url = urlOf(payload);
  const failure = asRecord(payload.failure);
  const compile = asRecord(payload.compile);
  const omitted = asRecord(compile.omitted);
  const completeness = asRecord(payload.completeness);
  const source = {
    kind: "page",
    url,
    ok: payload.ok === true,
    unchanged: payload.unchanged === true ? true : undefined,
    backend: payload.backend ?? null,
    contentHash: payload.contentHash ?? null,
    materializationKey: payload.materializationKey ?? null,
    tokensEstimated: compile.tokensEstimated ?? null,
    focus: typeof payload.focus === "string"
      ? payload.focus
      : asRecord(payload.focus).status ?? null,
    completeness: completeness.status ?? null,
    failureCode: payload.ok === true ? undefined : (failure.code ?? payload.failureCode ?? "unknown"),
    message: payload.ok === true ? undefined : (failure.message ?? payload.message ?? ""),
  };
  sources.push(source);

  if (omitted.reason || omitted.tokensDropped) {
    omissions.push({
      url,
      reason: omitted.reason ?? completeness.incompleteReason ?? "omitted",
      tokensDropped: omitted.tokensDropped ?? null,
      regions: omitted.regions ?? null,
      sections: omitted.sections ?? null,
      suggestedMinTokens: completeness.suggestedMinTokens ?? null,
    });
  } else if (completeness.status && completeness.status !== "complete") {
    omissions.push({
      url,
      reason: completeness.incompleteReason ?? completeness.status,
      tokensDropped: null,
      suggestedMinTokens: completeness.suggestedMinTokens ?? null,
    });
  }

  excerptParts.push(`## Source: ${url || "(unknown)"}\n`);
  if (payload.ok !== true) {
    excerptParts.push(`FAILURE ${source.failureCode}: ${source.message}\n`);
    return;
  }
  if (payload.unchanged === true) {
    excerptParts.push("Unchanged since the stored contentHash. Empty body is intentional.\n");
    return;
  }
  const markdown = typeof payload.markdown === "string" ? payload.markdown.trim() : "";
  excerptParts.push(markdown ? `${markdown}\n` : "(empty markdown)\n");
}

function collectDigest(payload, sources, omissions, excerptParts) {
  const items = Array.isArray(payload.items) ? payload.items : [];
  if (payload.ok === false && items.length === 0) {
    const failure = asRecord(payload.failure);
    sources.push({
      kind: "digest",
      url: payload.sourceUrl ?? null,
      ok: false,
      failureCode: failure.code ?? payload.failureCode ?? "digest_failed",
      message: failure.message ?? payload.message ?? "",
    });
    excerptParts.push(`## Digest\n\nFAILURE ${failure.code ?? "digest_failed"}\n`);
    return;
  }

  for (const item of items) {
    const page = asRecord(item);
    const completeness = asRecord(page.completeness);
    const url = typeof page.url === "string" ? page.url : "";
    sources.push({
      kind: "page",
      url,
      ok: page.ok !== false,
      tokensEstimated: page.tokensEstimated ?? null,
      focus: typeof page.focus === "string"
        ? page.focus
        : asRecord(page.focus).status ?? (page.focusMatched === false ? "miss" : null),
      completeness: completeness.status ?? null,
      failureCode: page.ok === false ? (page.failureCode ?? "unknown") : undefined,
      message: page.ok === false ? (page.message ?? "") : undefined,
    });
    if (completeness.status && completeness.status !== "complete") {
      omissions.push({
        url,
        reason: completeness.incompleteReason ?? completeness.status,
        tokensDropped: null,
        suggestedMinTokens: completeness.suggestedMinTokens ?? null,
      });
    }
    excerptParts.push(`## Source: ${url}\n`);
    if (page.ok === false) {
      excerptParts.push(`FAILURE ${page.failureCode ?? "unknown"}\n`);
    } else {
      const excerpt = typeof page.excerpt === "string" && page.excerpt.trim()
        ? page.excerpt.trim()
        : (typeof payload.combined === "string" ? "" : "");
      if (excerpt) excerptParts.push(`${excerpt}\n`);
    }
  }

  if (typeof payload.combined === "string" && payload.combined.trim() && items.every((i) => !asRecord(i).excerpt)) {
    excerptParts.push(`## Combined\n\n${payload.combined.trim()}\n`);
  }
}

/**
 * @param {unknown} payload
 * @returns {string[]}
 */
export function searchHitUrls(payload) {
  if (!payload || typeof payload !== "object") return [];
  const results = /** @type {Record<string, unknown>} */ (payload).results;
  if (!Array.isArray(results)) return [];
  return results
    .map((item) => {
      const hit = asRecord(item);
      return typeof hit.url === "string" ? hit.url : "";
    })
    .filter(Boolean);
}
