#!/usr/bin/env node
/**
 * HN RSS stage microbench + optional Node CPU profile (.cpuprofile → Speedscope / DevTools).
 *
 * Usage:
 *   node scripts/bench/hn-rss-stage-profile.mjs
 *   node scripts/bench/hn-rss-stage-profile.mjs --cpu-prof
 *   node --cpu-prof --cpu-prof-dir=artifacts/perf ...  (when --cpu-prof wraps a re-exec)
 */
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import { mkdirSync, writeFileSync, readFileSync, existsSync, renameSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { performance } from "node:perf_hooks";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..");
const httpRequire = createRequire(resolve(REPO, "workers", "http-extract", "package.json"));
const { JSDOM, VirtualConsole } = httpRequire("jsdom");
const {
  collectFeedItems,
  formatSummaryFields,
} = await import(pathToFileURL(resolve(REPO, "workers", "shared", "lib", "feed-items.mjs")).href);
const HN_URL = "https://hnrss.org/frontpage";
const OUT_DIR = resolve(REPO, "artifacts", "perf", `hn-rss-${stamp()}`);
const wantCpuProf = process.argv.includes("--cpu-prof");
const isProfileChild = process.argv.includes("--profile-child");

function stamp() {
  const d = new Date();
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
}

function ms(n) {
  return Math.round(n * 100) / 100;
}

async function fetchBody(url) {
  const t0 = performance.now();
  const res = await fetch(url, {
    headers: { "user-agent": "FF-Occam-perf-audit/1.0", accept: "application/rss+xml, application/xml, text/xml, */*" },
    signal: AbortSignal.timeout(60_000),
  });
  const buf = Buffer.from(await res.arrayBuffer());
  const networkMs = performance.now() - t0;
  return {
    ok: res.ok,
    status: res.status,
    contentType: res.headers.get("content-type") ?? "",
    body: buf.toString("utf8"),
    bytes: buf.length,
    networkMs,
  };
}

function parseXmlDom(xml, url) {
  const t0 = performance.now();
  const virtualConsole = new VirtualConsole();
  virtualConsole.on("jsdomError", () => {});
  const dom = new JSDOM(xml, { url, contentType: "text/xml", virtualConsole });
  return { doc: dom.window.document, jsdomMs: performance.now() - t0 };
}

function collect(doc, url) {
  const t0 = performance.now();
  const feed = collectFeedItems(doc, { baseUrl: url });
  return { feed, collectMs: performance.now() - t0 };
}

function articlePathCost(html, url) {
  // Approximate the non-json_feed path: full HTML JSDOM (not XML) — expensive on large RSS.
  const t0 = performance.now();
  const virtualConsole = new VirtualConsole();
  virtualConsole.on("jsdomError", () => {});
  const dom = new JSDOM(html, { url, virtualConsole });
  const textLen = (dom.window.document.body?.textContent ?? "").length;
  return { articleJsdomMs: performance.now() - t0, textLen };
}

function summaryOnlyCost(rawSummaries) {
  const t0 = performance.now();
  let mdChars = 0;
  for (const raw of rawSummaries) {
    const f = formatSummaryFields(raw);
    mdChars += (f.summaryMarkdown ?? "").length;
  }
  return { summaryMs: performance.now() - t0, mdChars };
}

async function runProfile() {
  mkdirSync(OUT_DIR, { recursive: true });
  const rounds = 3;
  const net = await fetchBody(HN_URL);
  writeFileSync(resolve(OUT_DIR, "hn-frontpage.rss"), net.body, "utf8");

  const samples = [];
  for (let i = 0; i < rounds; i++) {
    const { doc, jsdomMs } = parseXmlDom(net.body, HN_URL);
    const { feed, collectMs } = collect(doc, HN_URL);
    const rawSummaries = [];
    // Re-walk for summary-only cost: collect already did formatSummaryFields; isolate by
    // re-parsing once more and measuring formatSummaryFields on description-like strings.
    const items = [...doc.getElementsByTagName("item")];
    for (const item of items.slice(0, feed?.items?.length ?? 0)) {
      for (const child of item.children ?? []) {
        if (child.localName === "description" || child.localName === "content") {
          rawSummaries.push(child.innerHTML || child.textContent || "");
          break;
        }
      }
    }
    const { summaryMs, mdChars } = summaryOnlyCost(rawSummaries);
    const art = i === 0 ? articlePathCost(net.body, HN_URL) : null;
    samples.push({
      round: i + 1,
      jsdomMs: ms(jsdomMs),
      collectMs: ms(collectMs),
      summaryMs: ms(summaryMs),
      itemCount: feed?.items?.length ?? 0,
      mdChars,
      articleJsdomMs: art ? ms(art.articleJsdomMs) : undefined,
    });
  }

  // Burn CPU for --cpu-prof child: repeat JSDOM+collect so the profiler has signal.
  if (isProfileChild) {
    for (let i = 0; i < 8; i++) {
      const { doc } = parseXmlDom(net.body, HN_URL);
      collect(doc, HN_URL);
    }
  }

  const report = {
    url: HN_URL,
    fetched: {
      ok: net.ok,
      status: net.status,
      contentType: net.contentType,
      bytes: net.bytes,
      networkMs: ms(net.networkMs),
    },
    samples,
    medians: {
      jsdomMs: median(samples.map((s) => s.jsdomMs)),
      collectMs: median(samples.map((s) => s.collectMs)),
      summaryMs: median(samples.map((s) => s.summaryMs)),
    },
    outDir: OUT_DIR,
  };
  writeFileSync(resolve(OUT_DIR, "hn-rss-stages.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
  return report;
}

function median(xs) {
  const a = [...xs].sort((x, y) => x - y);
  const m = Math.floor(a.length / 2);
  return a.length % 2 ? a[m] : ms((a[m - 1] + a[m]) / 2);
}

async function main() {
  if (wantCpuProf && !isProfileChild) {
    mkdirSync(OUT_DIR, { recursive: true });
    const child = spawnSync(
      process.execPath,
      [
        "--cpu-prof",
        `--cpu-prof-dir=${OUT_DIR}`,
        "--cpu-prof-name=hn-rss",
        fileURLToPath(import.meta.url),
        "--profile-child",
      ],
      { cwd: REPO, encoding: "utf8", env: process.env },
    );
    process.stdout.write(child.stdout || "");
    process.stderr.write(child.stderr || "");
    let prof = resolve(OUT_DIR, "hn-rss.cpuprofile");
    if (!existsSync(prof)) {
      // Node may write the profile as `--cpu-prof-name` without extension.
      const alt = resolve(OUT_DIR, "hn-rss");
      if (existsSync(alt)) {
        try {
          renameSync(alt, prof);
        } catch {
          prof = alt;
        }
      }
    }
    if (existsSync(prof)) {
      console.log(`CPU_PROFILE: ${prof}`);
      try {
        const svg = cpuprofileToSvg(JSON.parse(readFileSync(prof, "utf8")));
        const svgPath = resolve(OUT_DIR, "hn-rss-flame.svg");
        writeFileSync(svgPath, svg, "utf8");
        console.log(`FLAME_SVG: ${svgPath}`);
      } catch (e) {
        console.error(`flame svg failed: ${e.message}`);
      }
    }
    process.exit(child.status ?? 1);
  }
  await runProfile();
}

/**
 * Minimal inverted-stack flame SVG from a V8 .cpuprofile (self-time by frame name).
 */
function cpuprofileToSvg(profile) {
  const nodes = profile.nodes || [];
  const samples = profile.samples || [];
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const selfHits = new Map();
  for (const sid of samples) {
    selfHits.set(sid, (selfHits.get(sid) || 0) + 1);
  }
  const rows = [];
  for (const [id, hits] of selfHits) {
    const n = byId.get(id);
    if (!n) continue;
    const fn = n.callFrame?.functionName || "(anonymous)";
    const url = n.callFrame?.url || "";
    const shortUrl = url.includes("node_modules")
      ? url.split("node_modules/").pop()
      : url.split(/[/\\]/).slice(-2).join("/");
    const label = `${fn} @ ${shortUrl || "native"}`;
    rows.push({ label, hits });
  }
  rows.sort((a, b) => b.hits - a.hits);
  const top = rows.slice(0, 40);
  const total = top.reduce((s, r) => s + r.hits, 0) || 1;
  const width = 1200;
  const rowH = 18;
  const height = 40 + top.length * rowH;
  let y = 28;
  const rects = top.map((r) => {
    const w = Math.max(2, (r.hits / total) * (width - 20));
    const hue = 200 - Math.min(180, (r.hits / total) * 200);
    const esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/"/g, "&quot;");
    const el = `<g transform="translate(10,${y})">
  <rect width="${w.toFixed(1)}" height="${rowH - 2}" fill="hsl(${hue},70%,55%)"/>
  <title>${esc(r.label)} — ${r.hits} samples</title>
  <text x="4" y="13" font-size="11" font-family="Consolas,monospace" fill="#111">${esc(r.label.slice(0, 90))} (${r.hits})</text>
</g>`;
    y += rowH;
    return el;
  });
  return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
  <rect width="100%" height="100%" fill="#fafafa"/>
  <text x="10" y="18" font-size="14" font-family="sans-serif" fill="#222">HN RSS CPU self-time (top ${top.length} frames)</text>
  ${rects.join("\n")}
</svg>
`;
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
