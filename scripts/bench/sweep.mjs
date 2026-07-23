// Large-scale 3-arm benchmark sweep: Occam vs raw fetch vs crawl4ai (local Docker sidecar).
// Resumable: appends one JSON line per URL to results.jsonl; on restart skips done ids.
// Tokens via tiktoken o200k_base. Run with NODE_PATH pointing at a node_modules that has tiktoken.
//
//   scripts/bench/crawl4ai-up.cmd                       # start the crawl4ai sidecar first
//   NODE_PATH=artifacts/bench-scratch/node_modules \
//   node scripts/bench/sweep.mjs --corpus=corpora/bench-1k.jsonl --start=0 --count=1000
//
// Env: CRAWL4AI_URL (default http://localhost:11235), CRAWL4AI_TOKEN (default occam-bench-local).
//      OCCAM_HOME defaults to cwd.

import { spawn } from "node:child_process";
import { join } from "node:path";
import fs from "node:fs";
import { get_encoding } from "tiktoken";

const enc = get_encoding("o200k_base");
const tok = (t) => (t ? enc.encode(t).length : 0);

const root = process.env.OCCAM_HOME?.trim() || process.cwd();
const args = Object.fromEntries(process.argv.slice(2).map((a) => {
  const m = a.match(/^--([^=]+)=(.*)$/); return m ? [m[1], m[2]] : [a.replace(/^--/, ""), true];
}));
const CORPUS = join(root, args.corpus || "corpora/bench-1k.jsonl");
const START = parseInt(args.start ?? "0", 10);
const COUNT = parseInt(args.count ?? "1000000", 10);
const OUT_DIR = join(root, "artifacts", args.out || `bench-1k-${new Date().toISOString().slice(0, 10)}`);
const RESULTS = join(OUT_DIR, "results.jsonl");
const PER_ARM_TIMEOUT = parseInt(args.timeout ?? "90000", 10);
const PACE_MS = parseInt(args.pace ?? "200", 10);

// crawl4ai arm: local Docker sidecar (replaces Firecrawl — credits exhausted, and crawl4ai is the
// closer open-source peer: HTML -> markdown for LLMs, free + unlimited so it runs the full 1000).
// Bring it up with scripts/bench/crawl4ai-up.cmd (binds 0.0.0.0, token-gated).
const C4_URL = (process.env.CRAWL4AI_URL || "http://localhost:11235").replace(/\/$/, "");
const C4_TOKEN = process.env.CRAWL4AI_TOKEN || "occam-bench-local";

fs.mkdirSync(OUT_DIR, { recursive: true });
const done = new Set();
if (fs.existsSync(RESULTS)) {
  for (const line of fs.readFileSync(RESULTS, "utf8").split("\n")) {
    if (!line.trim()) continue;
    try { done.add(JSON.parse(line).id); } catch {}
  }
}

// ---- MCP stdio client (multiplexes by id) ----
class Mcp {
  #buf = ""; #pending = new Map(); #id = 1;
  constructor(proc) { this.proc = proc; proc.stdout.on("data", (c) => this.#on(c.toString())); proc.stderr.on("data", () => {}); }
  #send(o) { this.proc.stdin.write(JSON.stringify(o) + "\n"); }
  #on(c) {
    this.#buf += c;
    for (;;) {
      const nl = this.#buf.indexOf("\n"); if (nl === -1) break;
      const line = this.#buf.slice(0, nl).trim(); this.#buf = this.#buf.slice(nl + 1);
      if (!line) continue; let msg; try { msg = JSON.parse(line); } catch { continue; }
      if (msg.id != null && this.#pending.has(msg.id)) {
        const { resolve, reject } = this.#pending.get(msg.id); this.#pending.delete(msg.id);
        msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
      }
    }
  }
  notify(method, params = {}) { this.#send({ jsonrpc: "2.0", method, params }); }
  request(method, params = {}, timeoutMs = PER_ARM_TIMEOUT) {
    const id = this.#id++; this.#send({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { if (this.#pending.has(id)) { this.#pending.delete(id); reject(new Error("mcp_timeout")); } }, timeoutMs);
      this.#pending.set(id, { resolve: (v) => { clearTimeout(timer); resolve(v); }, reject: (e) => { clearTimeout(timer); reject(e); } });
    });
  }
}
const parse = (r) => { const t = r?.content?.find((c) => c.type === "text")?.text; try { return JSON.parse(t); } catch { return null; } };

async function withTimeout(p, ms, label) {
  let t; const to = new Promise((_, rej) => (t = setTimeout(() => rej(new Error(label + "_timeout")), ms)));
  try { return await Promise.race([p, to]); } finally { clearTimeout(t); }
}

async function armOccam(client, url) {
  const start = performance.now();
  try {
    const res = await client.request("tools/call", { name: "occam_transcode", arguments: { url, backend_policy: "http_then_browser" } });
    const p = parse(res) || {};
    const md = p.markdown || "";
    // Transcode nests the code under failure.code (success has no failure); probe-style fallbacks kept for safety.
    const code = p.failure?.code || p.failureCode || p.failure_code || null;
    // p.timings (camelCase from the tool) carries the stage breakdown: network (with-internet) vs
    // parse (CPU) inside the worker, plus host-side preflight/route/postProcess/compile.
    // `backend` records the FINAL backend (http/browser/managed_*) so triage can tell an escalation
    // gap (stayed on http) from a capability gap (browser ran and still failed).
    return { ok: !!p.ok, failure_code: code, backend: p.backend ?? null, tokens: tok(md), ms: Math.round(performance.now() - start), timings: p.timings ?? null };
  } catch (e) { return { ok: false, failure_code: e.message, backend: null, tokens: 0, ms: Math.round(performance.now() - start), timings: null }; }
}
async function armFetch(url) {
  const start = performance.now();
  try {
    // Split the wall time into network (time-to-headers ~ DNS+connect+TLS+TTFB) and download
    // (body read) so the fetch arm has the same "with-internet vs rest" axis as Occam.
    const res = await withTimeout(fetch(url, { headers: { "User-Agent": "Mozilla/5.0 OccamBench" }, redirect: "follow" }), PER_ARM_TIMEOUT, "fetch");
    const network_ms = Math.round(performance.now() - start);
    const text = await res.text();
    const total = Math.round(performance.now() - start);
    return { ok: res.ok, status: res.status, tokens: tok(text), bytes: text.length, ms: total, network_ms, download_ms: total - network_ms };
  } catch (e) { return { ok: false, status: 0, tokens: 0, bytes: 0, error: e.message, ms: Math.round(performance.now() - start) }; }
}
async function armCrawl4ai(url) {
  const start = performance.now();
  try {
    // f=fit -> filtered "readable" markdown, the apples-to-apples comparison to Occam's fit-markdown.
    const res = await withTimeout(fetch(`${C4_URL}/md`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${C4_TOKEN}` },
      body: JSON.stringify({ url, f: "fit" }),
    }), PER_ARM_TIMEOUT, "crawl4ai");
    const data = await res.json();
    const md = typeof data?.markdown === "string" ? data.markdown : (data?.markdown?.fit_markdown || data?.markdown?.raw_markdown || "");
    return { ok: !!data?.success, status: res.status, tokens: tok(md), ms: Math.round(performance.now() - start) };
  } catch (e) { return { ok: false, error: e.message, tokens: 0, ms: Math.round(performance.now() - start) }; }
}

async function main() {
  const rows = fs.readFileSync(CORPUS, "utf8").split("\n").filter((l) => l.trim()).map((l) => JSON.parse(l)).slice(START, START + COUNT);
  const todo = rows.filter((r) => !done.has(r.id));
  console.error(`corpus=${rows.length} done=${done.size} todo=${todo.length} out=${RESULTS}`);

  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: { ...process.env, OCCAM_HOME: root, OCCAM_FORCE_DOTNET_RUN: "1", Logging__LogLevel__Default: "None", WT_OCCAM_BANNER: "0" },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new Mcp(proc);
  await client.request("initialize", { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "sweep", version: "1.0" } }, 30000);
  client.notify("notifications/initialized");

  const fd = fs.openSync(RESULTS, "a");
  let i = 0;
  for (const r of todo) {
    i++;
    const [occam, fetchR, c4] = await Promise.all([armOccam(client, r.url), armFetch(r.url), armCrawl4ai(r.url)]);
    const compression_vs_fetch = occam.tokens > 0 && fetchR.tokens > 0 ? +(1 - occam.tokens / fetchR.tokens).toFixed(4) : null;
    const compression_vs_crawl4ai = occam.tokens > 0 && c4.tokens > 0 ? +(1 - occam.tokens / c4.tokens).toFixed(4) : null;
    const content_found = occam.ok && occam.tokens >= 50; // trust-model: ok+real content
    const rec = { id: r.id, rank: r.rank, url: r.url, occam, fetch: fetchR, crawl4ai: c4, compression_vs_fetch, compression_vs_crawl4ai, content_found, ts: new Date().toISOString() };
    fs.writeSync(fd, JSON.stringify(rec) + "\n");
    if (i % 10 === 0 || i === todo.length) console.error(`[${i}/${todo.length}] ${r.url} occam:${occam.ok?occam.tokens+"t":occam.failure_code} fetch:${fetchR.tokens}t c4:${c4.ok?c4.tokens+"t":c4.error||"fail"}`);
    if (PACE_MS) await new Promise((res) => setTimeout(res, PACE_MS));
  }
  fs.closeSync(fd);
  client.proc.stdin.end(); proc.kill();
  console.error("done.");
  process.exit(0);
}
main().catch((e) => { console.error(e); process.exit(1); });
