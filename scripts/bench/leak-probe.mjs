// Leak probe: drive the live MCP host through N sequential transcodes while sampling its process
// tree's RSS + process count from outside, then idle to let cleanup run, and judge whether memory
// or processes leak. Answers "is there a stability problem?", not "how fast" — a long-running MCP
// server that climbs in RSS or accumulates orphan workers/Chromium across calls is a P0 bug.
//
//   scripts/bench/crawl4ai-up.cmd   # not needed here; this probe is occam-only
//   node scripts/bench/leak-probe.mjs [--iters=50] [--policy=http] [--warmup=5] [--settle=8]
//
// Verdict heuristics (after a warmup + a post-run idle settle):
//   - RSS leak    : idle-settled RSS stays >15% (and >60MB) above the post-warmup baseline.
//   - proc leak   : tree process count at idle stays above baseline (workers/Chromium not reaped).
import { spawn } from "node:child_process";
import { join } from "node:path";
import fs from "node:fs";

const root = process.env.OCCAM_HOME?.trim() || process.cwd();
const args = Object.fromEntries(process.argv.slice(2).map((a) => {
  const m = a.match(/^--([^=]+)=(.*)$/); return m ? [m[1], m[2]] : [a.replace(/^--/, ""), true];
}));
const ITERS = parseInt(args.iters ?? "50", 10);
const WARMUP = parseInt(args.warmup ?? "5", 10);
const SETTLE_S = parseInt(args.settle ?? "8", 10);
const POLICY = args.policy || "http";
const OUT_DIR = join(root, "artifacts", args.out || `leak-probe-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, "-")}`);
fs.mkdirSync(OUT_DIR, { recursive: true });

// A small rotating set; default to mostly-http pages so N iterations stay quick.
const URLS = [
  "https://en.wikipedia.org/wiki/Markdown",
  "https://nginx.org/en/docs/",
  "https://docs.python.org/3/library/json.html",
  "https://www.iana.org/help/example-domains",
  "https://developer.mozilla.org/en-US/docs/Web/HTTP",
  "https://www.rust-lang.org/",
];

class Mcp {
  #buf = ""; #pending = new Map(); #id = 1;
  constructor(proc) { this.proc = proc; proc.stdout.on("data", (c) => this.#on(c.toString())); proc.stderr.on("data", () => {}); }
  #send(o) { this.proc.stdin.write(JSON.stringify(o) + "\n"); }
  #on(c) {
    this.#buf += c;
    for (;;) { const nl = this.#buf.indexOf("\n"); if (nl === -1) break;
      const line = this.#buf.slice(0, nl).trim(); this.#buf = this.#buf.slice(nl + 1);
      if (!line) continue; let m; try { m = JSON.parse(line); } catch { continue; }
      if (m.id != null && this.#pending.has(m.id)) { this.#pending.get(m.id)(m); this.#pending.delete(m.id); } }
  }
  notify(method, params = {}) { this.#send({ jsonrpc: "2.0", method, params }); }
  request(method, params = {}, timeoutMs = 120000) {
    const id = this.#id++; this.#send({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const t = setTimeout(() => { if (this.#pending.has(id)) { this.#pending.delete(id); reject(new Error("mcp_timeout")); } }, timeoutMs);
      this.#pending.set(id, (m) => { clearTimeout(t); m.error ? reject(new Error(JSON.stringify(m.error))) : resolve(m.result); });
    });
  }
}
const parse = (r) => { const t = r?.content?.find((c) => c.type === "text")?.text; try { return JSON.parse(t); } catch { return null; } };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const mb = (b) => +(b / (1024 * 1024)).toFixed(1);
const median = (a) => { if (!a.length) return null; const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };

async function main() {
  console.error(`leak-probe: iters=${ITERS} policy=${POLICY} warmup=${WARMUP} settle=${SETTLE_S}s`);
  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root, env: { ...process.env, OCCAM_HOME: root, OCCAM_FORCE_DOTNET_RUN: "1", Logging__LogLevel__Default: "None", WT_OCCAM_BANNER: "0" },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new Mcp(proc);
  await client.request("initialize", { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "leakprobe", version: "1" } }, 30000);
  client.notify("notifications/initialized");

  const call = (url) => client.request("tools/call", { name: "occam_transcode", arguments: { url, backend_policy: POLICY } }).then(parse).catch(() => null);

  // Warmup: settle daemons/caches/JIT so the baseline is steady-state, not cold-start.
  for (let i = 0; i < WARMUP; i++) await call(URLS[i % URLS.length]);

  const samplesCsv = join(OUT_DIR, "samples.csv");
  const stopFlag = join(OUT_DIR, "stop.flag");
  try { fs.unlinkSync(stopFlag); } catch {}
  const sampler = spawn("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
    join(root, "scripts", "bench", "proc-tree-sampler.ps1"), "-RootPid", String(proc.pid),
    "-OutFile", samplesCsv, "-StopFile", stopFlag, "-IntervalMs", "200"], { stdio: "ignore" });
  await sleep(500);
  const activeStart = Date.now();

  let ok = 0;
  for (let i = 0; i < ITERS; i++) {
    const p = await call(URLS[i % URLS.length]);
    if (p?.ok) ok++;
    if ((i + 1) % 10 === 0) console.error(`  iter ${i + 1}/${ITERS} ok=${ok}`);
  }
  const activeEnd = Date.now();

  // Idle settle: no requests — RSS/processes should fall back if cleanup is correct.
  console.error(`  idle settle ${SETTLE_S}s…`);
  await sleep(SETTLE_S * 1000);

  fs.writeFileSync(stopFlag, "stop");
  await sleep(600);
  try { sampler.kill(); } catch {}
  client.proc.stdin.end(); proc.kill();

  // --- analyze ---
  const rows = fs.readFileSync(samplesCsv, "utf8").split("\n").filter((l) => l.trim()).slice(1)
    .map((l) => l.split(",").map(Number)).filter((r) => r.length >= 4 && r.every((x) => Number.isFinite(x)));
  const inWindow = (r, a, b) => r[0] >= a && r[0] <= b;
  const earlyWin = rows.filter((r) => inWindow(r, activeStart, activeStart + (activeEnd - activeStart) * 0.25));
  const idleWin = rows.filter((r) => r[0] >= activeEnd + 1500); // after requests stop + brief drain
  const baselineRss = mb(median(earlyWin.map((r) => r[1])) ?? rows[0]?.[1] ?? 0);
  const idleRss = mb(median(idleWin.map((r) => r[1])) ?? rows.at(-1)?.[1] ?? 0);
  const peakRss = mb(Math.max(...rows.map((r) => r[1])));
  const baseProc = median(earlyWin.map((r) => r[3])) ?? rows[0]?.[3] ?? 0;
  const idleProc = median(idleWin.map((r) => r[3])) ?? rows.at(-1)?.[3] ?? 0;
  const peakProc = Math.max(...rows.map((r) => r[3]));

  const rssGrowthMb = +(idleRss - baselineRss).toFixed(1);
  const rssLeak = rssGrowthMb > Math.max(60, baselineRss * 0.15);
  const procLeak = idleProc > baseProc + 1;

  const verdict = rssLeak || procLeak ? "SUSPECT_LEAK" : "no_leak_detected";
  const report = {
    generated_at: new Date().toISOString(), iters: ITERS, ok, policy: POLICY, samples: rows.length,
    rss_mb: { baseline: baselineRss, idle_settled: idleRss, peak: peakRss, growth: rssGrowthMb },
    proc_count: { baseline: baseProc, idle_settled: idleProc, peak: peakProc },
    flags: { rss_leak: rssLeak, proc_leak: procLeak },
    verdict,
  };
  fs.writeFileSync(join(OUT_DIR, "leak.json"), JSON.stringify(report, null, 2));

  console.error(`\n=== LEAK PROBE (${ITERS} iters, ok=${ok}) ===`);
  console.error(`RSS  baseline=${baselineRss}MB  idle=${idleRss}MB  peak=${peakRss}MB  growth=${rssGrowthMb}MB  ${rssLeak ? "⚠ LEAK" : "ok"}`);
  console.error(`PROC baseline=${baseProc}  idle=${idleProc}  peak=${peakProc}  ${procLeak ? "⚠ LEAK" : "ok"}`);
  console.error(`VERDICT: ${verdict}`);
  console.error(`OUT=${join(OUT_DIR, "leak.json")}`);
  process.exit(0);
}
main().catch((e) => { console.error(e); process.exit(1); });
