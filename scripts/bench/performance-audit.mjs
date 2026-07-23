#!/usr/bin/env node
/**
 * Warm HTTP-daemon latency matrix for the performance audit.
 * Usage: node scripts/bench/performance-audit.mjs [--port=39241] [--runs=2]
 */
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { performance } from "node:perf_hooks";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..");
const DAEMON = resolve(REPO, "workers", "http-extract", "http-daemon.mjs");

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const m = a.match(/^--([^=]+)(?:=(.*))?$/);
    return m ? [m[1], m[2] ?? true] : [a, true];
  }),
);
const PORT = Number(args.port ?? 39241);
const RUNS = Number(args.runs ?? 2);
const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const OUT_DIR = resolve(REPO, "artifacts", "perf", `worker-${stamp}`);

const CASES = [
  { id: "html-example", url: "https://example.com/", features: null },
  { id: "html-mdn", url: "https://developer.mozilla.org/en-US/docs/Web/HTTP", features: null },
  { id: "rss-hn", url: "https://hnrss.org/frontpage", features: null },
  { id: "rss-hn-json_feed", url: "https://hnrss.org/frontpage", features: "json_feed" },
  { id: "pdf-w3c", url: "https://www.w3.org/WAI/WCAG21/Techniques/pdf/img/table-word.pdf", features: null },
];

let daemonProc = null;

async function startDaemon() {
  daemonProc = spawn(process.execPath, [DAEMON, `--port=${PORT}`], {
    stdio: ["ignore", "ignore", "pipe"],
    env: { ...process.env, OCCAM_HOME: REPO },
  });
  for (let i = 0; i < 80; i++) {
    if (await healthy()) return;
    await sleep(200);
  }
  throw new Error("http-daemon not healthy");
}

async function healthy() {
  try {
    const r = await fetch(`http://127.0.0.1:${PORT}/health`, { signal: AbortSignal.timeout(1000) });
    return r.ok;
  } catch {
    return false;
  }
}

function stopDaemon() {
  try {
    daemonProc?.kill("SIGKILL");
  } catch {
    /* ignore */
  }
}

async function extract(url, features) {
  const t0 = performance.now();
  const r = await fetch(`http://127.0.0.1:${PORT}/extract`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ url, features: features ?? undefined }),
    signal: AbortSignal.timeout(180_000),
  });
  const wallMs = Math.round(performance.now() - t0);
  const body = await r.json();
  return {
    wallMs,
    ok: !!body.ok,
    failure: body.failure ?? null,
    network_ms: body.network_ms ?? null,
    parse_ms: body.parse_ms ?? null,
    latency_ms: body.latency_ms ?? null,
    text_length: body.text_length ?? body.markdown?.length ?? 0,
    feed_items: body.feed?.items?.length ?? null,
    content_type: body.content_type ?? null,
  };
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });
  await startDaemon();
  try {
    // warmup
    await extract("https://example.com/", null);
    const cases = [];
    for (const c of CASES) {
      const runs = [];
      for (let i = 0; i < RUNS; i++) {
        const row = await extract(c.url, c.features);
        runs.push({ run: i + 1, ...row });
        console.log(
          `  ${c.id} run=${i + 1} ok=${row.ok} wall=${row.wallMs} net=${row.network_ms} parse=${row.parse_ms} ` +
            `feed=${row.feed_items} fail=${row.failure ?? "-"} chars=${row.text_length}`,
        );
      }
      cases.push({ ...c, runs });
    }
    const payload = { stamp, port: PORT, cases };
    const out = resolve(OUT_DIR, "worker-perf-audit.json");
    writeFileSync(out, JSON.stringify(payload, null, 2));
    console.log(`WORKER_PERF_JSON: ${out}`);
    console.log("WORKER_PERF_OK");
  } finally {
    stopDaemon();
  }
}

main().catch((e) => {
  console.error(e);
  stopDaemon();
  process.exit(1);
});
