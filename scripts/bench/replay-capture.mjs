#!/usr/bin/env node
// Replay-capture — the faithful tool-selection A/B. Takes the REAL requests captured by
// capture-proxy.mjs (full system prompt + the whole crowded tools array the live agent sees) and
// re-asks the model under transformed variants, so we learn what actually moves the FIRST-tool pick
// in the real context — not in a hand-built 2-tool toy. The isolated harness said occam wins 88–100%;
// the live agent picked web_extract first. This tells us which lever (if any) closes that gap.
//
// Variants (each mutates the captured request, then measures which tool the model calls first):
//   as_captured      unchanged — should reproduce the live behavior (baseline truth)
//   nudge            append one tool-preference line to the system message (does a prompt line fix it?)
//   drop_other_occam remove the other mcp_ff_occam_* tools, keep occam_transcode (does 14-tool dilution hurt?)
//   no_web_extract   remove web_extract entirely (sanity: occam should then win)
//
// Usage (on the gateway host, after capturing):
//   OPENROUTER_API_KEY=... node scripts/bench/replay-capture.mjs [/tmp/captured-requests.jsonl]

import { readFile } from "node:fs/promises";

const API_KEY = process.env.OPENROUTER_API_KEY;
if (!API_KEY) { console.error("Set OPENROUTER_API_KEY."); process.exit(2); }
const IN = process.argv[2] || "/tmp/captured-requests.jsonl";
const MIN_GAP_MS = Math.max(0, parseInt(process.env.AB_MIN_GAP_MS || "4200", 10));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let lastCallAt = 0;
async function throttle() { const w = lastCallAt + MIN_GAP_MS - Date.now(); if (w > 0) await sleep(w); lastCallAt = Date.now(); }

const NUDGE =
  "\n\nTOOL PREFERENCE: when the user asks you to read, fetch, or summarize the content of a specific " +
  "web page (a URL), call occam_transcode — it runs locally, returns clean Markdown, and carries a " +
  "verifiable receipt. Prefer it over web_extract for reading a known URL.";

const isOccam = (n) => typeof n === "string" && n.includes("occam");
const occamTranscodeName = (tools) =>
  (tools.find((t) => (t.function?.name || "").endsWith("occam_transcode"))?.function?.name) || "occam_transcode";

function variants(req) {
  const clone = () => JSON.parse(JSON.stringify(req));
  const keepName = occamTranscodeName(req.tools || []);
  return {
    as_captured: clone(),
    nudge: (() => {
      const r = clone();
      const sys = r.messages.find((m) => m.role === "system");
      if (sys) sys.content = (sys.content || "") + NUDGE;
      else r.messages.unshift({ role: "system", content: NUDGE.trim() });
      return r;
    })(),
    drop_other_occam: (() => {
      const r = clone();
      r.tools = (r.tools || []).filter((t) => {
        const n = t.function?.name || "";
        return !isOccam(n) || n === keepName;
      });
      return r;
    })(),
    no_web_extract: (() => {
      const r = clone();
      r.tools = (r.tools || []).filter((t) => (t.function?.name || "") !== "web_extract");
      return r;
    })(),
  };
}

async function firstTool(req) {
  const body = JSON.stringify({ ...req, stream: false, temperature: 0, tool_choice: "auto" });
  for (let attempt = 0; attempt < 5; attempt++) {
    await throttle();
    const res = await fetch("https://openrouter.ai/api/v1/chat/completions", {
      method: "POST",
      headers: { Authorization: `Bearer ${API_KEY}`, "Content-Type": "application/json" },
      body,
    });
    if (res.status === 429) {
      const reset = Number(res.headers.get("x-ratelimit-reset")) || 0;
      const waitMs = Math.min(65_000, Math.max(3_000, reset ? reset - Date.now() : 15_000));
      console.error(`    429, waiting ${Math.round(waitMs / 1000)}s...`);
      await sleep(waitMs); continue;
    }
    if (!res.ok) throw new Error(`openrouter ${res.status}: ${(await res.text()).slice(0, 160)}`);
    const data = await res.json();
    return data.choices?.[0]?.message?.tool_calls?.[0]?.function?.name ?? "(none)";
  }
  throw new Error("429: exhausted retries");
}

const raw = (await readFile(IN, "utf8")).split("\n").filter(Boolean);
const requests = raw.map((l) => JSON.parse(l)).filter((r) => (r.tools || []).some((t) => isOccam(t.function?.name)));
if (!requests.length) { console.error(`No captured requests with an occam tool in ${IN}.`); process.exit(1); }
console.error(`replaying ${requests.length} captured request(s); each has ${requests[0].tools.length} tools`);

const VNAMES = ["as_captured", "nudge", "drop_other_occam", "no_web_extract"];
const tally = Object.fromEntries(VNAMES.map((v) => [v, { occam: 0, web: 0, other: 0, n: 0 }]));
for (let i = 0; i < requests.length; i++) {
  const vs = variants(requests[i]);
  for (const v of VNAMES) {
    let first;
    try { first = await firstTool(vs[v]); } catch (e) { console.error(`  req${i} ${v}: ${e.message}`); first = "(error)"; }
    const t = tally[v];
    t.n++;
    if (isOccam(first)) t.occam++; else if (first === "web_extract") t.web++; else t.other++;
    console.error(`  req${i} ${v.padEnd(16)} -> ${first}`);
  }
}

console.log("\n=== faithful replay (real captured context) — first-tool pick ===");
for (const v of VNAMES) {
  const t = tally[v];
  const pct = t.n ? Math.round((100 * t.occam) / t.n) : 0;
  console.log(`  ${v.padEnd(16)}  occam-first ${pct}%  (occam ${t.occam}/${t.n}, web_extract ${t.web}, other ${t.other})`);
}
console.log("\nRead: as_captured = the live truth with today's deployed description. If nudge >> as_captured,");
console.log("the lever is a prompt line in the agent's tool guidance. If drop_other_occam >> as_captured,");
console.log("occam's own 14-tool surface is diluting the pick. no_web_extract is a sanity floor.");
