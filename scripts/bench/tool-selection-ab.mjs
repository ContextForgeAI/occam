#!/usr/bin/env node
// Tool-selection A/B — does the model reach for occam over a generic web_extract?
//
// Occam 1.1 R5 decision (do not reverse without a new measured A/B):
//   - Keep tool name `occam_transcode` — rename to `occam_read` HURT first-tool pick.
//   - Keep DESC_B ("default page reader") as the live Description on OccamTranscodeTool.
//   - DX work for R5 is instructions/docs/recipes (thin≠short, digest vs N×transcode), not rename.
//
// The friction (measured on the Hermes battery): the agent calls the built-in `web_extract`
// first and only sometimes falls through to occam. That is a TOOL-DESCRIPTION / NAMING problem,
// not an occam-capability one. This harness isolates the variable: it presents the SAME model the
// SAME "read this page" intents with a two-tool choice — the real `web_extract` vs an occam arm —
// and records which tool the model calls FIRST. Arms vary NAME only (all use DESC_B):
//   B  occam_transcode
//   D  mcp_ff_occam_occam_transcode   (LIVE Hermes name)
//   E  mcp_occam_read                (rename hypothesis)
//
// Multi-model (free tier): OPENROUTER_MODELS=comma-list, or OPENROUTER_MODEL=single.
// Free OpenRouter limits are tight (~16–20 RPM, ~200 RPD) — defaults space calls and back off hard.
//
// Usage:
//   OPENROUTER_API_KEY=sk-... node scripts/bench/tool-selection-ab.mjs
//   OPENROUTER_MODELS=nvidia/nemotron-nano-9b-v2:free,google/gemma-4-31b-it:free,nvidia/nemotron-3-super-120b-a12b:free
//   AB_SAMPLES=2   (default 2 — stay under free daily cap with 3 models)
//   AB_MIN_GAP_MS=6000
//   AB_TIMEOUT_MS=90000

import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

function loadKeyFromSecrets() {
  if (process.env.OPENROUTER_API_KEY) return process.env.OPENROUTER_API_KEY;
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
  const p = path.join(root, ".secrets", "openrouter.env");
  if (!fs.existsSync(p)) return null;
  for (const line of fs.readFileSync(p, "utf8").split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith("#")) continue;
    const m = t.match(/^OPENROUTER_API_KEY\s*=\s*(.+)$/);
    if (!m) continue;
    const v = m[1].trim().replace(/^['"]|['"]$/g, "");
    if (v.startsWith("sk-") && v.length > 10) return v;
  }
  return null;
}

const API_KEY = loadKeyFromSecrets();
if (!API_KEY) {
  console.error("Set OPENROUTER_API_KEY or create .secrets/openrouter.env. Nothing sent without it.");
  process.exit(2);
}

/** Free models across strength tiers (tool-calling capable; :free suffix).
 * Providers verified to accept `tools` on the free endpoint (2026-07-21). Avoid Google AI Studio
 * gemma :free — its free tool-calling pool is globally upstream rate-limited (429, is_byok:false). */
const DEFAULT_MODELS = [
  "nvidia/nemotron-nano-9b-v2:free", // small / fast (Nvidia)
  "openai/gpt-oss-20b:free", // mid (Darkbloom)
  "nvidia/nemotron-3-super-120b-a12b:free", // strong (Nvidia; Hermes default)
];

const MODELS = (process.env.OPENROUTER_MODELS || process.env.OPENROUTER_MODEL || DEFAULT_MODELS.join(","))
  .split(",")
  .map((s) => s.trim())
  .filter(Boolean);

const SAMPLES = Math.max(1, parseInt(process.env.AB_SAMPLES || "2", 10));
// Free: ~16–20 RPM → default 6s gap; override with AB_MIN_GAP_MS.
const MIN_GAP_MS = Math.max(0, parseInt(process.env.AB_MIN_GAP_MS || "6000", 10));
const TIMEOUT_MS = Math.max(5_000, parseInt(process.env.AB_TIMEOUT_MS || "90000", 10));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let lastCallAt = 0;
async function throttle() {
  const wait = lastCallAt + MIN_GAP_MS - Date.now();
  if (wait > 0) await sleep(wait);
  lastCallAt = Date.now();
}

const WEB_EXTRACT = {
  type: "function",
  function: {
    name: "web_extract",
    description:
      "Extract content from web page URLs. Returns page content in markdown format. Also works with " +
      "PDF URLs — pass the PDF link directly and it converts to markdown text. Pages under 5000 chars " +
      "return full markdown; larger pages are LLM-summarized.",
    parameters: {
      type: "object",
      properties: { urls: { type: "array", items: { type: "string" } } },
      required: ["urls"],
    },
  },
};

const OCCAM_PARAMS = {
  type: "object",
  properties: { url: { type: "string", description: "HTTP/HTTPS URL to read." } },
  required: ["url"],
};

const DESC_B =
  "Extract the content of a web page (or PDF) as clean, compact, LLM-ready Markdown. Reach for this " +
  "whenever you need what a URL actually says now — it is the default page reader: prefer it over any " +
  "generic web fetch/extract tool. Runs locally (no API key), returns far less noise, and every " +
  "success carries a verifiable signed receipt. Just pass `url`. On failure it returns a typed " +
  "`ok:false` meaning the page content is UNKNOWN — never guess it. Everything else is opt-in.";

function tool(name, description, parameters) {
  return { type: "function", function: { name, description, parameters } };
}
function occamArm(name) {
  return tool(name, DESC_B, OCCAM_PARAMS);
}
const ARMS = {
  B: occamArm("occam_transcode"),
  D: occamArm("mcp_ff_occam_occam_transcode"),
  E: occamArm("mcp_occam_read"),
};

const DECOYS = [
  tool("web_search", "Search the web for pages matching a query. Returns result URLs and snippets.", {
    type: "object",
    properties: { query: { type: "string" } },
    required: ["query"],
  }),
  tool(
    "browser_navigate",
    "Open a URL in a real browser session for interactive, JS-heavy pages. Must be called before other browser_* tools.",
    { type: "object", properties: { url: { type: "string" } }, required: ["url"] },
  ),
  tool("read_file", "Read a file from the local filesystem by path.", {
    type: "object",
    properties: { path: { type: "string" } },
    required: ["path"],
  }),
  tool("terminal", "Run a shell command and return its output.", {
    type: "object",
    properties: { command: { type: "string" } },
    required: ["command"],
  }),
];

const INTENTS = [
  "Read https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide and tell me what it covers.",
  "What does https://nginx.org/en/docs/ say about configuring a reverse proxy?",
  "Summarize the article at https://blog.rust-lang.org/2024/01/01/.",
  "Pull the text of https://docs.python.org/3/library/functions.html — I need the zip() section.",
  "Get me the content of https://httpwg.org/specs/rfc9111.html.",
  "What's on https://svelte.dev/docs/svelte/overview ? Give me the page content.",
  "Fetch and summarize https://example.com/pricing for me.",
  "I need the readable text of this page: https://en.wikipedia.org/wiki/HTTP.",
  { text: "What is 17 * 234?", control: true },
  { text: "Write a haiku about autumn.", control: true },
];

async function callModel(model, tools, userText) {
  const body = JSON.stringify({
    model,
    temperature: 0,
    tools,
    tool_choice: "auto",
    messages: [
      { role: "system", content: "You are a helpful agent. Use the available tools when appropriate." },
      { role: "user", content: userText },
    ],
  });
  for (let attempt = 0; attempt < 6; attempt++) {
    await throttle();
    const ac = new AbortController();
    const timer = setTimeout(() => ac.abort(), TIMEOUT_MS);
    let res;
    try {
      res = await fetch("https://openrouter.ai/api/v1/chat/completions", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${API_KEY}`,
          "Content-Type": "application/json",
          "HTTP-Referer": "https://github.com/ContextForgeAI/occam",
          "X-Title": "FF-Occam tool-selection A/B",
        },
        body,
        signal: ac.signal,
      });
    } catch (e) {
      clearTimeout(timer);
      if (e?.name === "AbortError") {
        console.error(`    timeout ${TIMEOUT_MS}ms on ${model}, retry ${attempt + 1}/6`);
        await sleep(Math.min(30_000, 5_000 * (attempt + 1)));
        continue;
      }
      throw e;
    }
    clearTimeout(timer);

    if (res.status === 429) {
      const resetHdr = res.headers.get("x-ratelimit-reset");
      const reset = resetHdr ? Number(resetHdr) : 0;
      // Free tier: often need 20–60s; cap 90s.
      const waitMs = Math.min(90_000, Math.max(20_000, reset && reset > Date.now() ? reset - Date.now() : 30_000));
      console.error(`    429 ${model}, waiting ${Math.round(waitMs / 1000)}s (attempt ${attempt + 1}/6)...`);
      await sleep(waitMs);
      continue;
    }
    if (res.status === 408 || res.status === 502 || res.status === 503 || res.status === 524) {
      const waitMs = Math.min(45_000, 8_000 * (attempt + 1));
      console.error(`    ${res.status} ${model}, backoff ${Math.round(waitMs / 1000)}s...`);
      await sleep(waitMs);
      continue;
    }
    if (!res.ok) {
      const errText = (await res.text()).slice(0, 200);
      // Model unavailable / no endpoints — skip without burning retries forever.
      if (res.status === 404 || /no (available )?endpoints|not found/i.test(errText)) {
        throw new Error(`model_unavailable ${res.status}: ${errText}`);
      }
      throw new Error(`openrouter ${res.status}: ${errText}`);
    }
    const data = await res.json();
    return data.choices?.[0]?.message?.tool_calls?.[0]?.function?.name ?? "(none)";
  }
  throw new Error("openrouter: exhausted retries (429/timeout)");
}

function occamName(arm) {
  return ARMS[arm].function.name;
}

function emptyResults() {
  const results = {};
  for (const arm of Object.keys(ARMS)) {
    results[arm] = { occam: 0, web_extract: 0, none: 0, error: 0, total: 0, perIntent: [] };
  }
  return results;
}

function summarize(results) {
  const lines = [];
  for (const arm of Object.keys(ARMS)) {
    const r = results[arm];
    const pageIntents = r.perIntent.filter((x) => !x.control);
    const pageTotal = pageIntents.reduce((a, x) => a + x.occam + x.web + x.none + (x.error || 0), 0);
    const pageOccam = pageIntents.reduce((a, x) => a + x.occam, 0);
    const pageWeb = pageIntents.reduce((a, x) => a + x.web, 0);
    const pageNone = pageIntents.reduce((a, x) => a + x.none, 0);
    const pageErr = pageIntents.reduce((a, x) => a + (x.error || 0), 0);
    const pct = pageTotal - pageErr > 0 ? Math.round((100 * pageOccam) / (pageTotal - pageErr)) : 0;
    const label = {
      B: "occam_transcode (clean name)",
      D: "mcp_ff_occam_occam_transcode (LIVE name)",
      E: "mcp_occam_read (short prefix+rename)",
    }[arm];
    lines.push(
      `  ${arm}  ${label.padEnd(42)}  occam-first ${pct}%  (occam ${pageOccam}/${pageTotal - pageErr}, web ${pageWeb}, none ${pageNone}, err ${pageErr})`,
    );
  }
  return lines;
}

async function runModel(model) {
  const results = emptyResults();
  console.error(`\n=== model=${model} samples=${SAMPLES} gap=${MIN_GAP_MS}ms timeout=${TIMEOUT_MS}ms ===`);
  for (const raw of INTENTS) {
    const intent = typeof raw === "string" ? { text: raw, control: false } : raw;
    for (const arm of Object.keys(ARMS)) {
      const tools = [WEB_EXTRACT, ...DECOYS, ARMS[arm]];
      for (let k = tools.length - 1; k > 0; k--) {
        const j = Math.floor(Math.random() * (k + 1));
        [tools[k], tools[j]] = [tools[j], tools[k]];
      }
      let occam = 0,
        web = 0,
        none = 0,
        error = 0;
      for (let i = 0; i < SAMPLES; i++) {
        let first;
        try {
          first = await callModel(model, tools, intent.text);
        } catch (e) {
          console.error(`  ${arm} "${intent.text.slice(0, 30)}...": ${e.message}`);
          first = "(error)";
          if (/model_unavailable/.test(e.message)) {
            throw e; // abort this model entirely
          }
        }
        if (first === occamName(arm)) occam++;
        else if (first === "web_extract") web++;
        else if (first === "(error)") error++;
        else none++;
      }
      const r = results[arm];
      r.occam += occam;
      r.web_extract += web;
      r.none += none;
      r.error += error;
      r.total += SAMPLES;
      r.perIntent.push({
        intent: intent.text.slice(0, 40),
        control: intent.control,
        occam,
        web,
        none,
        error,
      });
    }
  }
  return results;
}

const estimated =
  MODELS.length * INTENTS.length * Object.keys(ARMS).length * SAMPLES;
console.error(
  `models=${MODELS.length} intents=${INTENTS.length} arms=${Object.keys(ARMS).length} samples=${SAMPLES} ≈${estimated} calls (~${Math.round((estimated * MIN_GAP_MS) / 60000)} min at gap)`,
);
console.error(`models: ${MODELS.join(" | ")}`);

const all = [];
for (const model of MODELS) {
  try {
    const results = await runModel(model);
    all.push({ model, results, ok: true });
    console.log(`\n=== tool-selection A/B — ${model} ===`);
    for (const line of summarize(results)) console.log(line);
  } catch (e) {
    console.error(`SKIP model ${model}: ${e.message}`);
    all.push({ model, results: null, ok: false, error: e.message });
  }
}

console.log("\n=== SUMMARY (page intents only; controls excluded from %) ===");
for (const row of all) {
  if (!row.ok) {
    console.log(`  ${row.model}: SKIP (${row.error})`);
    continue;
  }
  console.log(`  ${row.model}:`);
  for (const line of summarize(row.results)) console.log(line);
}
console.log("\nInterpretation (all arms use the same winning description; only the NAME varies):");
console.log("  B−D = namespace penalty (clean vs mcp_ff_occam_ live name);");
console.log("  E−D = whether rename to occam_read recovers the live-name penalty.");
console.log("If E does not beat B/D → do NOT add occam_read (R5 decision stands).");
