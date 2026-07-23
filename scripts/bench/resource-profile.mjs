// Resource profiler: RAM (peak RSS) + CPU% for each engine, measured from OUTSIDE the process.
// Runs each engine SEQUENTIALLY over the same URLs (no concurrency contamination) so the RSS/CPU
// numbers are honestly attributable. This is the companion to sweep.mjs: sweep answers "how small /
// how fast", this answers "at what RAM/CPU cost". Occam's per-stage timings are collected too.
//
//   scripts/bench/crawl4ai-up.cmd                         # crawl4ai sidecar must be running
//   node scripts/bench/resource-profile.mjs [--corpus=corpora/x.jsonl] [--count=15] [--policy=http_then_browser]
//
// Env: CRAWL4AI_URL (default http://localhost:11235), CRAWL4AI_TOKEN (default occam-bench-local).
import { spawn } from "node:child_process";
import { join } from "node:path";
import os from "node:os";
import fs from "node:fs";

const root = process.env.OCCAM_HOME?.trim() || process.cwd();
const args = Object.fromEntries(process.argv.slice(2).map((a) => {
  const m = a.match(/^--([^=]+)=(.*)$/); return m ? [m[1], m[2]] : [a.replace(/^--/, ""), true];
}));
const COUNT = parseInt(args.count ?? "15", 10);
const POLICY = args.policy || "http_then_browser";
const NUM_CPU = os.cpus().length;
const C4_URL = (process.env.CRAWL4AI_URL || "http://localhost:11235").replace(/\/$/, "");
const C4_TOKEN = process.env.CRAWL4AI_TOKEN || "occam-bench-local";
const OUT_DIR = join(root, "artifacts", args.out || `resource-profile-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, "-")}`);
fs.mkdirSync(OUT_DIR, { recursive: true });

// Default workload: a representative mix (light docs, heavy news, SPA, large) when no corpus given.
const DEFAULT_URLS = [
  "https://en.wikipedia.org/wiki/Markdown",
  "https://nginx.org/en/docs/",
  "https://developer.mozilla.org/en-US/docs/Web/JavaScript",
  "https://www.iana.org/help/example-domains",
  "https://docs.python.org/3/library/json.html",
  "https://www.bbc.com/news",
  "https://www.theguardian.com/international",
  "https://stackoverflow.com/questions/231767/what-does-the-yield-keyword-do-in-python",
  "https://news.ycombinator.com/",
  "https://www.cloudflare.com/",
  "https://reactjs.org/",
  "https://www.rust-lang.org/",
];
const urls = (args.corpus
  ? fs.readFileSync(join(root, args.corpus), "utf8").split("\n").filter((l) => l.trim()).map((l) => JSON.parse(l).url)
  : DEFAULT_URLS).slice(0, COUNT);

// ---- minimal MCP stdio client ----
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
      if (msg.id != null && this.#pending.has(msg.id)) { this.#pending.get(msg.id)(msg); this.#pending.delete(msg.id); }
    }
  }
  notify(method, params = {}) { this.#send({ jsonrpc: "2.0", method, params }); }
  request(method, params = {}, timeoutMs = 120000) {
    const id = this.#id++; this.#send({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { if (this.#pending.has(id)) { this.#pending.delete(id); reject(new Error("mcp_timeout")); } }, timeoutMs);
      this.#pending.set(id, (m) => { clearTimeout(timer); m.error ? reject(new Error(JSON.stringify(m.error))) : resolve(m.result); });
    });
  }
}
const parse = (r) => { const t = r?.content?.find((c) => c.type === "text")?.text; try { return JSON.parse(t); } catch { return null; } };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const mb = (bytes) => +(bytes / (1024 * 1024)).toFixed(1);

// Parse the sampler CSV -> peak RSS + CPU% series (CPU100ns deltas over wall deltas / cores).
function reduceSamples(csvPath) {
  const lines = fs.readFileSync(csvPath, "utf8").split("\n").filter((l) => l.trim()).slice(1);
  const rows = lines.map((l) => l.split(",").map(Number)).filter((r) => r.length >= 3 && r.slice(0, 3).every((x) => Number.isFinite(x)));
  if (rows.length < 2) return { samples: rows.length, peak_rss_mb: rows.length ? mb(rows[0][1]) : null, cpu_pct: null };
  let peakRss = 0;
  const cores = [];
  for (let i = 1; i < rows.length; i++) {
    peakRss = Math.max(peakRss, rows[i][1], rows[i - 1][1]);
    const wallMs = rows[i][0] - rows[i - 1][0];
    const cpu100ns = rows[i][2] - rows[i - 1][2];
    if (wallMs > 0 && cpu100ns >= 0) {
      // cores used = cpu-seconds / wall-seconds (1.0 == one core fully busy). Same unit as
      // docker stats CPUPerc/100, so occam and crawl4ai are directly comparable.
      cores.push((cpu100ns / 1e7) / (wallMs / 1000));
    }
  }
  const mean = cores.length ? +(cores.reduce((a, b) => a + b, 0) / cores.length).toFixed(2) : null;
  const peak = cores.length ? +Math.max(...cores).toFixed(2) : null;
  return { samples: rows.length, peak_rss_mb: mb(peakRss), cpu_cores: { mean, peak }, machine_cores: NUM_CPU };
}

async function profileOccam() {
  const samplesCsv = join(OUT_DIR, "occam-samples.csv");
  const stopFlag = join(OUT_DIR, "occam-stop.flag");
  try { fs.unlinkSync(stopFlag); } catch {}
  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root, env: { ...process.env, OCCAM_HOME: root, OCCAM_FORCE_DOTNET_RUN: "1", Logging__LogLevel__Default: "None", WT_OCCAM_BANNER: "0" },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new Mcp(proc);
  await client.request("initialize", { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "resprofile", version: "1" } }, 30000);
  client.notify("notifications/initialized");

  // Start the external sampler on the host's whole process tree (host -> dotnet -> node workers/Chromium).
  const sampler = spawn("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
    join(root, "scripts", "bench", "proc-tree-sampler.ps1"), "-RootPid", String(proc.pid),
    "-OutFile", samplesCsv, "-StopFile", stopFlag, "-IntervalMs", "150"], { stdio: "ignore" });
  await sleep(400); // let the first sample land

  const perUrl = [];
  for (const url of urls) {
    const t0 = performance.now();
    try {
      const res = await client.request("tools/call", { name: "occam_transcode", arguments: { url, backend_policy: POLICY } });
      const p = parse(res) || {};
      perUrl.push({ url, ok: !!p.ok, ms: Math.round(performance.now() - t0), timings: p.timings ?? null, backend: p.backend ?? null });
    } catch (e) { perUrl.push({ url, ok: false, ms: Math.round(performance.now() - t0), error: e.message }); }
    process.stderr.write(`  occam ${perUrl.length}/${urls.length} ${url} -> ${perUrl.at(-1).ok ? "ok" : "fail"}\n`);
  }

  fs.writeFileSync(stopFlag, "stop");
  await sleep(500);
  try { sampler.kill(); } catch {}
  client.proc.stdin.end(); proc.kill();
  return { resource: reduceSamples(samplesCsv), per_url: perUrl };
}

async function profileCrawl4ai() {
  // Poll the container stats from outside (docker stats), in parallel with a sequential URL run.
  const stats = [];
  let polling = true;
  const poll = (async () => {
    while (polling) {
      await new Promise((resolve) => {
        const ps = spawn("docker", ["stats", "crawl4ai-bench", "--no-stream", "--format", "{{.MemUsage}}|{{.CPUPerc}}"], { stdio: ["ignore", "pipe", "ignore"] });
        let out = ""; ps.stdout.on("data", (d) => (out += d));
        ps.on("close", () => {
          const m = out.trim().match(/([\d.]+)\s*([KMG]i?B)\s*\/.*\|\s*([\d.]+)%/i);
          if (m) {
            const unit = m[2].toUpperCase(); const val = parseFloat(m[1]);
            const memMb = unit.startsWith("G") ? val * 1024 : unit.startsWith("K") ? val / 1024 : val;
            stats.push({ memMb, cpu: parseFloat(m[3]) });
          }
          resolve();
        });
      });
    }
  })();

  const perUrl = [];
  for (const url of urls) {
    const t0 = performance.now();
    try {
      const res = await fetch(`${C4_URL}/md`, { method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${C4_TOKEN}` }, body: JSON.stringify({ url, f: "fit" }) });
      const data = await res.json();
      perUrl.push({ url, ok: !!data?.success, ms: Math.round(performance.now() - t0) });
    } catch (e) { perUrl.push({ url, ok: false, ms: Math.round(performance.now() - t0), error: e.message }); }
    process.stderr.write(`  crawl4ai ${perUrl.length}/${urls.length} ${url} -> ${perUrl.at(-1).ok ? "ok" : "fail"}\n`);
  }
  polling = false; await poll;

  const mems = stats.map((s) => s.memMb), cores = stats.map((s) => s.cpu / 100); // docker % -> cores
  const resource = {
    samples: stats.length,
    peak_rss_mb: mems.length ? +Math.max(...mems).toFixed(1) : null,
    cpu_cores: {
      mean: cores.length ? +(cores.reduce((a, b) => a + b, 0) / cores.length).toFixed(2) : null,
      peak: cores.length ? +Math.max(...cores).toFixed(2) : null,
    },
  };
  return { resource, per_url: perUrl };
}

async function main() {
  console.error(`resource-profile: ${urls.length} urls, policy=${POLICY}, cores=${NUM_CPU}\n[1/2] occam (host process tree)…`);
  const occam = await profileOccam();
  console.error(`[2/2] crawl4ai (container)…`);
  const crawl4ai = await profileCrawl4ai();

  const report = { generated_at: new Date().toISOString(), urls: urls.length, policy: POLICY, cores: NUM_CPU, occam, crawl4ai };
  const outPath = join(OUT_DIR, "resource.json");
  fs.writeFileSync(outPath, JSON.stringify(report, null, 2));

  const o = occam.resource, c = crawl4ai.resource;
  console.error(`\n=== RESOURCE PROFILE (${urls.length} urls, cpu in cores; 1.0 = one core busy) ===`);
  console.error(`occam     peak_rss=${o.peak_rss_mb}MB  cpu mean=${o.cpu_cores?.mean} peak=${o.cpu_cores?.peak} cores  ok=${occam.per_url.filter((u) => u.ok).length}/${urls.length}`);
  console.error(`crawl4ai  peak_rss=${c.peak_rss_mb}MB  cpu mean=${c.cpu_cores?.mean} peak=${c.cpu_cores?.peak} cores  ok=${crawl4ai.per_url.filter((u) => u.ok).length}/${urls.length}`);
  console.error(`OUT=${outPath}`);
  process.exit(0);
}
main().catch((e) => { console.error(e); process.exit(1); });
