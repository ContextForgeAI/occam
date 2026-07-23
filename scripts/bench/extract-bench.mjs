#!/usr/bin/env node
// Reproducible extraction-latency benchmark: FF-Occam (warm HTTP daemon) vs Jina Reader (hosted)
// vs a raw fetch floor. Honest by construction — warm-up discarded, N timed runs, and output size +
// success are reported alongside latency (fast-but-empty is not a win). See README.md for methodology.
//
// Usage:
//   node extract-bench.mjs [--runs=5] [--warmup=1] [--no-jina] [--port=39230]
//                          [--urls=path-to-newline-list] [--out=extract-bench-results.json]
//
// No API keys. Requires Node 18+ (global fetch). Occam daemon is spawned from this repo's workers.

import { spawn } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { performance } from "node:perf_hooks";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..");
const DAEMON = resolve(REPO, "workers", "http-extract", "http-daemon.mjs");

// ---- args ---------------------------------------------------------------------------------------
const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const m = a.match(/^--([^=]+)(?:=(.*))?$/);
    return m ? [m[1], m[2] ?? true] : [a, true];
  }),
);
const RUNS = Number(args.runs ?? 5);
const WARMUP = Number(args.warmup ?? 1);
const PORT = Number(args.port ?? 39230);
const WITH_JINA = !args["no-jina"];
const OUT = args.out ?? resolve(HERE, "extract-bench-results.json");

// A small, stable, extraction-friendly, robots-friendly corpus of real pages. Override with --urls.
const DEFAULT_URLS = [
  "https://example.com",
  "https://developer.mozilla.org/en-US/docs/Web/HTTP",
  "https://nginx.org/en/docs/",
  "https://en.wikipedia.org/wiki/HTTP",
  "https://docs.python.org/3/library/json.html",
];
const URLS = args.urls
  ? readFileSync(args.urls, "utf8").split("\n").map((s) => s.trim()).filter(Boolean)
  : DEFAULT_URLS;

// ---- daemon lifecycle ---------------------------------------------------------------------------
let daemonProc = null;

async function startDaemon() {
  daemonProc = spawn(process.execPath, [DAEMON, `--port=${PORT}`], {
    stdio: ["ignore", "ignore", "ignore"],
    env: { ...process.env, OCCAM_HOME: REPO },
  });
  daemonProc.unref?.();
  for (let i = 0; i < 60; i++) {
    if (await daemonHealthy()) return true;
    await sleep(250);
  }
  throw new Error("occam http-daemon did not become healthy");
}

async function daemonHealthy() {
  try {
    const r = await fetch(`http://127.0.0.1:${PORT}/health`, { signal: AbortSignal.timeout(1500) });
    return r.ok;
  } catch {
    return false;
  }
}

function stopDaemon() {
  try { daemonProc?.kill("SIGKILL"); } catch { /* best effort */ }
}

// ---- engines ------------------------------------------------------------------------------------
// Each returns { ok, chars } and is timed by the caller. Latency is wall-clock of the call.
const engines = {
  // The real product path: POST to the warm long-lived HTTP-extract daemon → clean markdown.
  async occam(url) {
    const r = await fetch(`http://127.0.0.1:${PORT}/extract`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ url }),
      signal: AbortSignal.timeout(35_000),
    });
    const j = await r.json();
    return { ok: !!j.ok, chars: (j.markdown ?? "").length };
  },
  // Hosted competitor: Jina Reader turns a URL into markdown server-side (no key for basic use).
  async jina(url) {
    const r = await fetch(`https://r.jina.ai/${url}`, {
      headers: { "x-respond-with": "markdown" },
      signal: AbortSignal.timeout(35_000),
    });
    const text = await r.text();
    return { ok: r.ok && text.length > 0, chars: text.length };
  },
  // Floor reference: a bare fetch of the HTML, NO extraction. Shows how much of occam's time is the
  // network fetch itself vs the extraction it adds on top (it returns raw HTML, not clean markdown).
  async raw(url) {
    const r = await fetch(url, { redirect: "follow", signal: AbortSignal.timeout(35_000) });
    const html = await r.text();
    return { ok: r.ok && html.length > 0, chars: html.length };
  },
};

// ---- run ----------------------------------------------------------------------------------------
async function timeCall(fn, url) {
  const t0 = performance.now();
  try {
    const { ok, chars } = await fn(url);
    return { ms: performance.now() - t0, ok, chars };
  } catch (e) {
    return { ms: performance.now() - t0, ok: false, chars: 0, error: String(e?.message ?? e) };
  }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const pct = (arr, p) => {
  if (arr.length === 0) return null;
  const s = [...arr].sort((a, b) => a - b);
  return Math.round(s[Math.min(s.length - 1, Math.floor((p / 100) * s.length))]);
};
const median = (arr) => pct(arr, 50);

async function main() {
  const names = ["occam", "raw", ...(WITH_JINA ? ["jina"] : [])];
  console.error(`# extract-bench — ${URLS.length} URLs × ${RUNS} runs (+${WARMUP} warmup), engines: ${names.join(", ")}`);
  console.error(`# started ${new Date().toISOString()} · node ${process.version} · port ${PORT}`);

  await startDaemon();
  const results = {}; // engine -> { latencies:[], oks:0, total:0, chars:[] }
  for (const n of names) results[n] = { latencies: [], oks: 0, total: 0, chars: [] };

  try {
    for (const url of URLS) {
      for (const name of names) {
        // Warm-up (discarded): first call per (engine,url) pays cold caches / connection setup.
        for (let w = 0; w < WARMUP; w++) await timeCall(engines[name], url);
        for (let i = 0; i < RUNS; i++) {
          const r = await timeCall(engines[name], url);
          const e = results[name];
          e.total++;
          if (r.ok) { e.oks++; e.latencies.push(r.ms); e.chars.push(r.chars); }
        }
      }
      console.error(`  · done ${url}`);
    }
  } finally {
    stopDaemon();
  }

  // ---- report -------------------------------------------------------------------------------------
  const rows = names.map((n) => {
    const e = results[n];
    return {
      engine: n,
      runs: e.total,
      successPct: e.total ? Math.round((100 * e.oks) / e.total) : 0,
      p50ms: median(e.latencies),
      p95ms: pct(e.latencies, 95),
      minMs: e.latencies.length ? Math.round(Math.min(...e.latencies)) : null,
      medianChars: median(e.chars),
    };
  });

  const label = { occam: "occam (warm daemon)", jina: "jina (hosted)", raw: "raw fetch (HTML, no extract)" };
  console.log("\n| engine | success | p50 ms | p95 ms | min ms | median chars |");
  console.log("|--------|:-------:|:------:|:------:|:------:|:------------:|");
  for (const r of rows) {
    console.log(`| ${label[r.engine] ?? r.engine} | ${r.successPct}% | ${r.p50ms ?? "—"} | ${r.p95ms ?? "—"} | ${r.minMs ?? "—"} | ${r.medianChars ?? "—"} |`);
  }
  console.log(
    "\nMethodology: warm-up run discarded per (engine,url); p50/p95 over successful runs only; same machine + network, back-to-back. " +
      "`occam` returns clean Markdown + a signed receipt locally; `jina` is a hosted service (your URL + its content leave your machine); " +
      "`raw` is a bare HTML fetch with NO extraction (a network floor, not a real competitor). Latency is network-dependent — re-run for your own numbers.",
  );

  const out = { generatedAt: new Date().toISOString(), node: process.version, runs: RUNS, warmup: WARMUP, urls: URLS, rows };
  writeFileSync(OUT, JSON.stringify(out, null, 2));
  console.error(`\n# wrote ${OUT}`);
}

main().catch((e) => { stopDaemon(); console.error(e); process.exit(1); });
