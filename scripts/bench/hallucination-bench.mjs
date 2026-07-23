#!/usr/bin/env node
// Hallucination benchmark — evidence layer (OKP launch asset #3-B).
// Runs two readers over the trap corpus and records, side by side, what each hands an LLM:
//   - occam    : the full typed pipeline (ok:false + failure.code on traps) via the AOT host over MCP stdio
//   - rawfetch : a naive `read_url` (HTTP GET → strip tags → text) — what a generic web-fetch tool returns
// On a TRAP, occam should return ok:false (honest "unknown" → the agent abstains); rawfetch hands back
// misleading bytes (soft-404 body, challenge page, paywall teaser) that an LLM tends to fabricate from.
// This layer produces the evidence; the model-judge pass (fabrication scoring) is a separate step —
// feed results.json to a real model (self-use: Hermes / a sub-agent) with the strict ABSTAIN prompt.
//
// Usage:
//   node hallucination-bench.mjs [--corpus=../../corpora/hallucination-traps.jsonl]
//                                [--bin=<path to OccamMcp.Core host>] [--out=hallucination-bench-results.json]
//                                [--timeout=45000] [--only=<category>]
// No API keys. Node 18+ (global fetch). The occam arm needs a built host binary + OCCAM_HOME workers.

import { spawn } from "node:child_process";
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..");

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const m = a.match(/^--([^=]+)(?:=(.*))?$/);
    return m ? [m[1], m[2] ?? true] : [a, true];
  }),
);

const CORPUS = resolve(args.corpus ? String(args.corpus) : resolve(REPO, "corpora", "hallucination-traps.jsonl"));
const OUT = resolve(args.out ? String(args.out) : resolve(HERE, "hallucination-bench-results.json"));
const TIMEOUT = Number(args.timeout ?? 45000);
const DEFAULT_BIN = resolve(
  REPO, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", "win-x64", "publish",
  process.platform === "win32" ? "OccamMcp.Core.exe" : "OccamMcp.Core",
);
const BIN = resolve(args.bin ? String(args.bin) : DEFAULT_BIN);

function readCorpus() {
  const rows = readFileSync(CORPUS, "utf8").split("\n").map((l) => l.trim()).filter(Boolean).map((l) => JSON.parse(l));
  return args.only ? rows.filter((r) => r.category === args.only) : rows;
}

// ---- occam arm: one persistent MCP-stdio host, one tools/call per URL ---------------------------
class OccamHost {
  constructor(bin) {
    this.proc = spawn(bin, [], { env: { ...process.env, OCCAM_HOME: REPO, OCCAM_BANNER: "0", WT_OCCAM_BANNER: "0" }, stdio: ["pipe", "pipe", "ignore"] });
    this.buf = "";
    this.pending = new Map();
    this.proc.stdout.on("data", (d) => this.#onData(d));
    this.nextId = 1;
  }
  #onData(d) {
    this.buf += d.toString("utf8");
    let nl;
    while ((nl = this.buf.indexOf("\n")) >= 0) {
      const line = this.buf.slice(0, nl).trim();
      this.buf = this.buf.slice(nl + 1);
      if (!line) continue;
      let msg;
      try { msg = JSON.parse(line); } catch { continue; }
      if (msg.id != null && this.pending.has(msg.id)) {
        this.pending.get(msg.id)(msg);
        this.pending.delete(msg.id);
      }
    }
  }
  #send(method, params) {
    const id = this.nextId++;
    this.proc.stdin.write(JSON.stringify({ jsonrpc: "2.0", id, method, params }) + "\n");
    return new Promise((res, rej) => {
      const t = setTimeout(() => { this.pending.delete(id); rej(new Error("timeout")); }, TIMEOUT);
      this.pending.set(id, (m) => { clearTimeout(t); res(m); });
    });
  }
  #notify(method, params) { this.proc.stdin.write(JSON.stringify({ jsonrpc: "2.0", method, params }) + "\n"); }
  async init() {
    await this.#send("initialize", { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "halluc-bench", version: "0" } });
    this.#notify("notifications/initialized", {});
  }
  async transcode(url) {
    const m = await this.#send("tools/call", { name: "occam_transcode", arguments: { url } });
    // MCP tool result: { content: [{ type:"text", text:"<json>" }] }
    const text = m?.result?.content?.[0]?.text ?? m?.error?.message ?? "{}";
    let parsed; try { parsed = JSON.parse(text); } catch { parsed = { ok: false, parseError: true }; }
    return parsed;
  }
  stop() { try { this.proc.stdin.end(); this.proc.kill(); } catch { /* best effort */ } }
}

// ---- rawfetch arm: what a generic read_url hands the model --------------------------------------
async function rawFetch(url) {
  const ctrl = new AbortController();
  const t = setTimeout(() => ctrl.abort(), TIMEOUT);
  try {
    const r = await fetch(url, { redirect: "follow", signal: ctrl.signal, headers: { "user-agent": "Mozilla/5.0 (bench)" } });
    const html = await r.text();
    const text = html.replace(/<script[\s\S]*?<\/script>/gi, " ").replace(/<style[\s\S]*?<\/style>/gi, " ")
      .replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
    return { status: r.status, ok: r.ok, textLen: text.length, sample: text.slice(0, 240) };
  } catch (e) {
    return { status: 0, ok: false, error: String(e?.name ?? e), textLen: 0, sample: "" };
  } finally { clearTimeout(t); }
}

async function main() {
  const corpus = readCorpus();
  if (!existsSync(BIN)) {
    console.error(`error: occam host binary not found: ${BIN}\n  build it: dotnet publish src/FFOccamMcp.Core -c Release -r <rid>`);
    process.exit(1);
  }
  console.error(`hallucination-bench: ${corpus.length} cases · host=${BIN}`);
  const host = new OccamHost(BIN);
  await host.init();

  const results = [];
  for (const c of corpus) {
    process.error?.write?.("");
    let occam, raw;
    try { occam = await host.transcode(c.url); } catch (e) { occam = { ok: false, error: String(e?.message ?? e) }; }
    try { raw = await rawFetch(c.url); } catch (e) { raw = { error: String(e) }; }

    // Scoring (evidence, not judged): on a trap, occam is "honest" iff ok:false; rawfetch "handed content"
    // iff it returned a non-trivial body an LLM could fabricate from.
    const occamHonestOnTrap = c.is_trap ? occam.ok === false : occam.ok === true;
    const rawHandedContent = (raw.textLen ?? 0) >= 200;
    results.push({
      id: c.id, category: c.category, url: c.url, is_trap: c.is_trap, probe: c.probe,
      occam: { ok: occam.ok ?? null, failure: occam.failure?.code ?? null, markdownLen: (occam.markdown ?? "").length },
      rawfetch: { status: raw.status ?? null, textLen: raw.textLen ?? 0, sample: raw.sample ?? "" },
      signals: { occamHonestOnTrap, rawHandedContent },
    });
    const tag = c.is_trap ? "TRAP" : "clean";
    console.error(`  [${tag}] ${c.id.padEnd(22)} occam=${occam.ok === false ? "ok:false/" + (occam.failure?.code ?? "?") : "ok:true"}  raw=${raw.status}/${raw.textLen}b`);
  }
  host.stop();

  const traps = results.filter((r) => r.is_trap);
  const clean = results.filter((r) => !r.is_trap);
  const summary = {
    cases: results.length,
    traps: traps.length,
    clean: clean.length,
    occam_honest_on_traps: traps.filter((r) => r.signals.occamHonestOnTrap).length,
    occam_correct_on_clean: clean.filter((r) => r.occam.ok === true).length,
    rawfetch_handed_content_on_traps: traps.filter((r) => r.signals.rawHandedContent).length,
  };
  writeFileSync(OUT, JSON.stringify({ generatedAt: new Date().toISOString(), summary, results }, null, 2));
  console.error("\n=== summary ===");
  console.error(`  traps: occam honest (ok:false) ${summary.occam_honest_on_traps}/${summary.traps} · rawfetch handed >=200b content ${summary.rawfetch_handed_content_on_traps}/${summary.traps}`);
  console.error(`  clean: occam answered (ok:true) ${summary.occam_correct_on_clean}/${summary.clean}`);
  console.error(`  wrote ${OUT}`);
  console.error("  NEXT: feed results.json to a real model (Hermes / sub-agent) with the ABSTAIN prompt to score fabrication.");
}

main().catch((e) => { console.error(e); process.exit(1); });
