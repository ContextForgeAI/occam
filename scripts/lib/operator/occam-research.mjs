/**
 * Bounded site research planner — no second crawler.
 * Discovery (map/search links) and extraction (transcode/digest) stay separate.
 *
 * `--max-bytes` is kept extracted markdown (UTF-8 output bytes), not HTTP
 * download size. Time remaining is applied to each MCP call.
 */
import { existsSync, mkdirSync, readFileSync, renameSync, unlinkSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { rankHits } from "./discovery-rank.mjs";

export const RESEARCH_SCHEMA = "occam.site-research.v1";
export const PAGES_SCHEMA = "occam.site-research.pages.v1";

export const DEFAULT_BUDGETS = {
  maxUrls: 16,
  maxPages: 4,
  deadlineMs: 60_000,
  maxBytes: 1_000_000,
};

/**
 * @param {string[]} argv
 */
export function parseResearchArgs(argv) {
  const flags = {
    help: false,
    json: false,
    seed: null,
    focus: null,
    sameDomain: true,
    maxUrls: DEFAULT_BUDGETS.maxUrls,
    maxPages: DEFAULT_BUDGETS.maxPages,
    deadlineMs: DEFAULT_BUDGETS.deadlineMs,
    maxBytes: DEFAULT_BUDGETS.maxBytes,
    out: null,
    resume: false,
    fromJson: null,
    error: null,
  };
  const args = [...argv];
  while (args.length) {
    const token = args.shift();
    if (token === "-h" || token === "--help") {
      flags.help = true;
      continue;
    }
    if (token === "--json") {
      flags.json = true;
      continue;
    }
    if (token === "--resume") {
      flags.resume = true;
      continue;
    }
    if (token === "--same-domain") {
      flags.sameDomain = true;
      continue;
    }
    if (token === "--any-domain") {
      flags.sameDomain = false;
      continue;
    }
    const take = (name) => (token.includes("=") ? token.slice(name.length + 1) : args.shift());
    if (token === "--seed" || token.startsWith("--seed=")) {
      flags.seed = take("--seed");
      if (!flags.seed) flags.error = "missing --seed value";
      continue;
    }
    if (token === "--focus" || token.startsWith("--focus=")) {
      flags.focus = take("--focus");
      if (!flags.focus) flags.error = "missing --focus value";
      continue;
    }
    if (token === "--out" || token.startsWith("--out=")) {
      flags.out = take("--out");
      if (!flags.out) flags.error = "missing --out value";
      continue;
    }
    if (token === "--from-json" || token.startsWith("--from-json=")) {
      flags.fromJson = take("--from-json");
      if (!flags.fromJson) flags.error = "missing --from-json value";
      continue;
    }
    if (token === "--max-urls" || token.startsWith("--max-urls=")) {
      const n = Number(take("--max-urls"));
      if (!Number.isInteger(n) || n < 1 || n > 64) flags.error = "--max-urls must be 1–64";
      else flags.maxUrls = n;
      continue;
    }
    if (token === "--max-pages" || token.startsWith("--max-pages=")) {
      const n = Number(take("--max-pages"));
      if (!Number.isInteger(n) || n < 1 || n > 16) flags.error = "--max-pages must be 1–16";
      else flags.maxPages = n;
      continue;
    }
    if (token === "--deadline-ms" || token.startsWith("--deadline-ms=")) {
      const n = Number(take("--deadline-ms"));
      if (!Number.isInteger(n) || n < 1000) flags.error = "--deadline-ms must be >= 1000";
      else flags.deadlineMs = n;
      continue;
    }
    if (token === "--max-bytes" || token.startsWith("--max-bytes=")) {
      const n = Number(take("--max-bytes"));
      if (!Number.isInteger(n) || n < 1024) flags.error = "--max-bytes must be >= 1024";
      else flags.maxBytes = n;
      continue;
    }
    if (token.startsWith("-")) {
      flags.error = `unknown flag ${token}`;
      continue;
    }
    if (!flags.seed) flags.seed = token;
    else flags.error = `unexpected argument ${token}`;
  }
  if (!flags.help && !flags.error) {
    if (!flags.fromJson && !flags.seed) flags.error = "need --seed or --from-json";
    else if (!flags.fromJson && !flags.out) flags.error = "need --out";
  }
  return flags;
}

export function canonicalizeUrl(raw) {
  try {
    const uri = new URL(String(raw ?? "").trim());
    if (uri.protocol !== "http:" && uri.protocol !== "https:") return "";
    uri.hash = "";
    if ((uri.protocol === "http:" && uri.port === "80") || (uri.protocol === "https:" && uri.port === "443")) {
      uri.port = "";
    }
    return uri.toString();
  } catch {
    return "";
  }
}

export function hostOf(raw) {
  try {
    return new URL(canonicalizeUrl(raw) || raw).hostname.replace(/^www\./i, "").toLowerCase();
  } catch {
    return "";
  }
}

export function inScope(url, seed, sameDomain) {
  if (!sameDomain) return canonicalizeUrl(url) !== "";
  return hostOf(url) !== "" && hostOf(url) === hostOf(seed);
}

export function dedupeUrls(urls) {
  const seen = new Set();
  const out = [];
  for (const raw of urls) {
    const key = canonicalizeUrl(raw);
    if (!key || seen.has(key)) continue;
    seen.add(key);
    out.push(key);
  }
  return out;
}

/**
 * @param {{
 *   seed: string,
 *   focus?: string | null,
 *   sameDomain?: boolean,
 *   discovered: Array<{ url: string, title?: string }>,
 *   extracted?: Array<{ url: string, ok?: boolean, bytes?: number }>,
 *   budgets?: Partial<typeof DEFAULT_BUDGETS>,
 *   startedAt?: number,
 *   now?: number,
 *   cancelled?: boolean,
 *   halt?: string | null,
 * }} input
 */
export function planResearch(input) {
  const budgets = { ...DEFAULT_BUDGETS, ...input.budgets };
  const seed = canonicalizeUrl(input.seed) || String(input.seed ?? "");
  const sameDomain = input.sameDomain !== false;
  const extracted = input.extracted ?? [];
  const extractedKeys = new Set(extracted.map((row) => canonicalizeUrl(row.url)).filter(Boolean));
  const bytesUsed = extracted.reduce((sum, row) => {
    if (row.ok === false) return sum;
    return sum + (Number(row.bytes) || 0);
  }, 0);
  const now = input.now ?? Date.now();
  const startedAt = input.startedAt ?? now;

  const scoped = [];
  const outOfScope = [];
  for (const hit of input.discovered ?? []) {
    const url = canonicalizeUrl(hit.url);
    if (!url) continue;
    if (!inScope(url, seed, sameDomain)) {
      outOfScope.push(url);
      continue;
    }
    scoped.push({ ...hit, url });
  }
  const unique = [];
  const seen = new Set();
  for (const hit of scoped) {
    if (seen.has(hit.url)) continue;
    seen.add(hit.url);
    unique.push(hit);
  }
  const ranked = rankHits(input.focus || hostOf(seed), unique).slice(0, budgets.maxUrls);
  const pending = ranked.filter((hit) => !extractedKeys.has(hit.url)).map((hit) => hit.url);

  let stop = null;
  if (input.halt) stop = input.halt;
  else if (input.cancelled) stop = "cancelled";
  else if (now - startedAt >= budgets.deadlineMs) stop = "budget_time";
  else if (bytesUsed >= budgets.maxBytes) stop = "budget_bytes";
  else if (extracted.length >= budgets.maxPages) stop = "budget_pages";
  else if (pending.length === 0) stop = extracted.length > 0 ? "complete" : "scope_exhausted";

  return {
    seed,
    scope: { sameDomain, host: hostOf(seed) },
    budgets,
    discovery: {
      input: unique.length,
      kept: ranked.length,
      pending: stop ? [] : pending,
      outOfScope,
      omitted: unique.slice(budgets.maxUrls).map((hit) => hit.url),
    },
    nextUrl: stop ? null : pending[0] ?? null,
    stop,
    bytesUsed,
  };
}

/**
 * @param {object} input
 */
export function assembleResearch(input) {
  const plan = planResearch(input);
  const extracted = input.extracted ?? [];
  const failed = extracted.filter((row) => row.ok === false);
  const succeeded = extracted.filter((row) => row.ok !== false);
  return {
    schema: RESEARCH_SCHEMA,
    seed: plan.seed,
    focus: input.focus ?? null,
    createdAt: input.createdAt ?? new Date().toISOString(),
    toolchain: input.toolchain ?? "ff-occam",
    scope: plan.scope,
    budgets: plan.budgets,
    discovery: {
      tool: input.discoveryTool ?? "occam_map",
      provider: input.provider ?? null,
      urls: (input.discovered ?? []).map((hit) => hit.url),
      ranked: plan.discovery.kept,
      outOfScope: plan.discovery.outOfScope,
      omitted: plan.discovery.omitted,
    },
    extraction: {
      tool: input.extractionTool ?? "occam_transcode",
      pages: extracted.length,
      succeeded: succeeded.length,
      failed: failed.length,
      bytes: plan.bytesUsed,
      urls: extracted.map((row) => ({
        url: row.url,
        ok: row.ok !== false,
        failureCode: row.failureCode ?? null,
        bytes: Number(row.bytes) || 0,
      })),
      omitted: Array.isArray(input.omitted) ? input.omitted : [],
    },
    stop: {
      reason: plan.stop ?? "in_progress",
      nextUrl: plan.nextUrl,
    },
    ok:
      failed.length === 0
      && succeeded.length > 0
      && ["complete", "budget_pages", "budget_time", "budget_bytes"].includes(plan.stop ?? ""),
  };
}

export function researchFiles(report, excerpts = "", pages = []) {
  return {
    "research-state.json": `${JSON.stringify(report, null, 2)}\n`,
    "discovery.json": `${JSON.stringify(report.discovery, null, 2)}\n`,
    "extraction.json": `${JSON.stringify(report.extraction, null, 2)}\n`,
    "pages.json": `${JSON.stringify({ schema: PAGES_SCHEMA, pages }, null, 2)}\n`,
    "excerpts.txt": excerpts.endsWith("\n") ? excerpts : `${excerpts}\n`,
  };
}

/**
 * Split excerpts.txt `## url` sections. Used when resuming a folder that
 * predates pages.json.
 * @param {string} text
 * @returns {Array<[string, string]>}
 */
export function parseExcerptSections(text) {
  /** @type {Array<[string, string]>} */
  const out = [];
  for (const part of String(text ?? "").split(/^## /m).slice(1)) {
    const nl = part.indexOf("\n");
    const heading = (nl === -1 ? part : part.slice(0, nl)).trim();
    const body = nl === -1 ? "" : part.slice(nl + 1).replace(/^\n/, "").replace(/\s+$/u, "");
    if (!heading || heading === "Omissions" || heading === "Site research") continue;
    out.push([heading, body]);
  }
  return out;
}

/**
 * Restore per-page markdown from pages.json, then excerpts.txt, then metadata.
 * @param {{
 *   urls?: Array<{ url: string, ok?: boolean, bytes?: number, failureCode?: string, markdown?: string }>,
 *   pages?: Array<{ url: string, ok?: boolean, bytes?: number, failureCode?: string, markdown?: string, fetchedAt?: string }>,
 *   excerpts?: string,
 * }} input
 */
export function restoreExtractedPages(input) {
  const byKey = new Map();
  const order = [];
  const remember = (row) => {
    const key = canonicalizeUrl(row.url) || String(row.url ?? "");
    if (!key) return;
    if (!byKey.has(key)) order.push(key);
    const prev = byKey.get(key) ?? { url: row.url || key };
    byKey.set(key, {
      url: prev.url || row.url || key,
      ok: row.ok ?? prev.ok,
      bytes: row.bytes ?? prev.bytes ?? 0,
      failureCode: row.failureCode ?? prev.failureCode ?? null,
      markdown: row.markdown != null && row.markdown !== "" ? row.markdown : (prev.markdown ?? ""),
      fetchedAt: row.fetchedAt ?? prev.fetchedAt ?? null,
    });
  };
  for (const row of input.urls ?? []) remember(row);
  for (const row of input.pages ?? []) remember(row);
  for (const [url, markdown] of parseExcerptSections(input.excerpts ?? "")) {
    const key = canonicalizeUrl(url) || url;
    const prev = byKey.get(key);
    if (prev && !prev.markdown) prev.markdown = markdown;
    else if (!prev && markdown) remember({ url, ok: true, bytes: Buffer.byteLength(markdown, "utf8"), markdown });
  }
  return order.map((key) => byKey.get(key)).filter(Boolean);
}

/**
 * @param {string} outDir
 */
export function loadResearchCheckpoint(outDir) {
  const statePath = join(outDir, "research-state.json");
  if (!existsSync(statePath)) return null;
  const prior = JSON.parse(readFileSync(statePath, "utf8"));
  const pagesPath = join(outDir, "pages.json");
  const excerptsPath = join(outDir, "excerpts.txt");
  const pagesRaw = existsSync(pagesPath) ? JSON.parse(readFileSync(pagesPath, "utf8")) : {};
  const pages = Array.isArray(pagesRaw.pages) ? pagesRaw.pages : [];
  const excerpts = existsSync(excerptsPath) ? readFileSync(excerptsPath, "utf8") : "";
  const extracted = restoreExtractedPages({
    urls: prior.extraction?.urls ?? [],
    pages,
    excerpts,
  });
  const discovered = Array.isArray(prior.discovery?.urls)
    ? prior.discovery.urls.map((url) => ({ url }))
    : [];
  return {
    prior,
    seed: prior.seed ?? null,
    focus: prior.focus ?? null,
    toolchain: prior.toolchain ?? null,
    createdAt: prior.createdAt ?? null,
    discovered,
    extracted,
    omitted: Array.isArray(prior.extraction?.omitted) ? prior.extraction.omitted : [],
  };
}

/**
 * @param {string} filePath
 * @param {string} body
 */
export function writeAtomicFile(filePath, body) {
  mkdirSync(dirname(filePath), { recursive: true });
  const tmp = `${filePath}.${process.pid}.tmp`;
  writeFileSync(tmp, body);
  try {
    renameSync(tmp, filePath);
  } catch {
    if (existsSync(filePath)) unlinkSync(filePath);
    renameSync(tmp, filePath);
  }
}

/**
 * @param {string} seed
 * @param {Array<{ url: string, ok?: boolean, markdown?: string, failureCode?: string }>} extracted
 * @param {Array<{ url: string, reason?: string, bytes?: number }>} [omitted]
 */
export function buildResearchExcerpts(seed, extracted, omitted = []) {
  const parts = ["# Site research\n", `Seed: ${seed}\n`];
  for (const row of extracted) {
    const body = row.ok === false
      ? `ok:false ${row.failureCode ?? ""}\n`
      : `${row.markdown ?? ""}\n`;
    parts.push(`## ${row.url}\n\n${body}`);
  }
  if (omitted.length) {
    parts.push("## Omissions\n");
    for (const row of omitted) {
      parts.push(`- ${row.url}: ${row.reason ?? "omitted"} (${Number(row.bytes) || 0} output bytes not kept)\n`);
    }
  }
  return parts.join("\n");
}

/**
 * @param {Array<{ url: string, ok?: boolean, bytes?: number, failureCode?: string, markdown?: string, fetchedAt?: string }>} extracted
 */
export function pageRecords(extracted) {
  return extracted.map((row) => ({
    url: row.url,
    ok: row.ok !== false,
    bytes: Number(row.bytes) || 0,
    failureCode: row.failureCode ?? null,
    markdown: row.markdown ?? "",
    fetchedAt: row.fetchedAt ?? null,
  }));
}
