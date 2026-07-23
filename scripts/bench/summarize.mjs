// Aggregate a sweep results.jsonl into honest summary stats + flag trust-model violations.
//   node scripts/bench/summarize.mjs artifacts/bench-1k-2026-06-25/results.jsonl
import fs from "node:fs";

const path = process.argv[2];
if (!path) { console.error("usage: summarize.mjs <results.jsonl>"); process.exit(1); }
const rows = fs.readFileSync(path, "utf8").split("\n").filter((l) => l.trim()).map((l) => JSON.parse(l));

const pct = (arr, p) => { if (!arr.length) return null; const s = [...arr].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.floor(s.length * p))]; };
const mean = (arr) => (arr.length ? +(arr.reduce((a, b) => a + b, 0) / arr.length).toFixed(1) : null);
const hist = (arr) => arr.reduce((m, x) => ((m[x ?? "null"] = (m[x ?? "null"] || 0) + 1), m), {});

const n = rows.length;
const occamOk = rows.filter((r) => r.occam.ok);
const c4Ok = rows.filter((r) => r.crawl4ai?.ok);
const fetchOk = rows.filter((r) => r.fetch?.ok);
const contentFound = rows.filter((r) => r.content_found);

// Trust-model violations: ok:true but essentially no content.
const trustViol = rows.filter((r) => r.occam.ok && r.occam.tokens < 50);

// Compression: only where both occam and the peer produced real content.
const comp = rows.filter((r) => r.compression_vs_fetch != null).map((r) => r.compression_vs_fetch);
const occamVsC4 = rows.filter((r) => r.occam.ok && r.occam.tokens > 0 && r.crawl4ai?.ok && r.crawl4ai.tokens > 0)
  .map((r) => +(1 - r.occam.tokens / r.crawl4ai.tokens).toFixed(4));

// Stage timings: p50/p95 per stage so we can see where time goes ("with-internet vs without").
const stageStats = (arr) => arr.length ? { p50: pct(arr, 0.5), p95: pct(arr, 0.95), mean: mean(arr) } : null;
const tArr = (key) => occamOk.filter((r) => r.occam.timings).map((r) => r.occam.timings[key]).filter((v) => v != null);
const dispatchOverhead = occamOk.filter((r) => r.occam.timings)
  .map((r) => r.occam.timings.routeMs - (r.occam.timings.networkMs || 0) - (r.occam.timings.parseMs || 0))
  .filter((v) => v >= 0);
const fetchNet = fetchOk.filter((r) => r.fetch.network_ms != null).map((r) => r.fetch.network_ms);
const fetchDl = fetchOk.filter((r) => r.fetch.download_ms != null).map((r) => r.fetch.download_ms);

const summary = {
  corpus: path,
  n,
  occam: {
    ok_rate: +(occamOk.length / n).toFixed(3),
    content_found_rate: +(contentFound.length / n).toFixed(3),
    trust_violations: trustViol.length,
    failure_codes: hist(rows.filter((r) => !r.occam.ok).map((r) => r.occam.failure_code)),
    latency_ms: { p50: pct(occamOk.map((r) => r.occam.ms), 0.5), p95: pct(occamOk.map((r) => r.occam.ms), 0.95), mean: mean(occamOk.map((r) => r.occam.ms)) },
    tokens_median: pct(contentFound.map((r) => r.occam.tokens), 0.5),
    // Per-stage breakdown (ms). network=with-internet, parse=CPU, dispatch=worker spawn + IPC.
    stage_ms: {
      network: stageStats(tArr("networkMs")),
      parse: stageStats(tArr("parseMs")),
      dispatch_overhead: stageStats(dispatchOverhead),
      preflight: stageStats(tArr("preflightMs")),
      post_process: stageStats(tArr("postProcessMs")),
      compile: stageStats(tArr("compileMs")),
      total: stageStats(tArr("totalMs")),
      n_with_timings: tArr("totalMs").length,
    },
  },
  fetch: {
    ok_rate: +(fetchOk.length / n).toFixed(3),
    tokens_median: pct(fetchOk.map((r) => r.fetch.tokens), 0.5),
    stage_ms: { network: stageStats(fetchNet), download: stageStats(fetchDl) },
  },
  crawl4ai: {
    ok_rate: +(c4Ok.length / n).toFixed(3),
    tokens_median: pct(c4Ok.map((r) => r.crawl4ai.tokens), 0.5),
    latency_ms: { p50: pct(c4Ok.map((r) => r.crawl4ai.ms), 0.5), p95: pct(c4Ok.map((r) => r.crawl4ai.ms), 0.95) },
    errors: hist(rows.filter((r) => r.crawl4ai && !r.crawl4ai.ok).map((r) => r.crawl4ai.error)),
  },
  compression_vs_fetch: { n: comp.length, p50: pct(comp, 0.5), p90: pct(comp, 0.9), mean: mean(comp.map((x) => x * 100)) },
  compression_vs_crawl4ai: { n: occamVsC4.length, p50: pct(occamVsC4, 0.5), mean: mean(occamVsC4.map((x) => x * 100)) },
};

console.log(JSON.stringify(summary, null, 2));
if (trustViol.length) {
  console.error(`\n⚠ ${trustViol.length} trust-model suspects (ok:true, tokens<50):`);
  for (const r of trustViol.slice(0, 20)) console.error(`  ${r.url} (${r.occam.tokens}t)`);
}
