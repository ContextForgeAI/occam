#!/usr/bin/env node
/**
 * Hermes transcript -> battery capture JSONL.
 *
 * Turns a raw Hermes conversation (its persistent `messages` store) into the capture schema the
 * scorer eats (docs-internal/HERMES-AGENT-FRIENDLY-PLAN.md §2). It does NOT talk to Hermes or the
 * DB itself — it reads a *session dump* (one message row per line) so the host side stays a trivial,
 * dependency-free query and all the fragile join/normalise logic lives here, in-repo and testable.
 *
 * ── Producing the session dump (on the Hermes host; no sqlite3 CLI there, python3 stdlib has it) ──
 *   SID=20260704_231751_29c6a937            # the session id (state.db → sessions table)
 *   sudo python3 - "$SID" > session-dump.jsonl <<'PY'
 *   import sqlite3, json, sys
 *   c = sqlite3.connect("/srv/hermes/state.db")
 *   cols = ["id","role","content","tool_call_id","tool_calls","tool_name","reasoning","timestamp"]
 *   for r in c.execute(f"select {','.join(cols)} from messages where session_id=? order by id", (sys.argv[1],)):
 *       print(json.dumps(dict(zip(cols, r)), ensure_ascii=False))
 *   PY
 * Then scp/paste it here and run this parser.
 *
 * ── Segmentation ──
 * Each battery intent MUST be sent to Hermes prefixed  [[BATTERY <task_id>]] <intent>  (see
 * hermes-battery-send below). A user row carrying that marker opens a task segment; every
 * assistant/tool row until the next marker (or end) belongs to it. Rows before the first marker
 * (install chatter, greetings) are ignored.
 *
 * ── Usage ──
 *   node scripts/hermes-log-to-capture.mjs --dump=session-dump.jsonl [--battery=corpora/hermes-battery.jsonl]
 *        [--run-id=…] [--instructions-ver=…] [--out=capture.jsonl] [--stdout]
 *   node scripts/hermes-log-to-capture.mjs --selftest
 *
 * ── The one field this cannot infer ──
 * `asserts_page_content` (did the final answer state page content as fact?) is the judged trust flag.
 * The parser writes a *heuristic* value plus `needs_judgment:true` and an `answer_excerpt`, so a human
 * or judge model corrects it before scoring. Never trust the heuristic on a trust-tier task blindly.
 */
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");

const BATTERY_MARKER = /\[\[BATTERY\s+([A-Za-z0-9_-]+)\]\]/;
const KNOWN_OCCAM_TOOLS = new Set([
  "occam_transcode", "occam_probe", "occam_digest", "occam_map", "occam_search",
  "occam_extract_knowledge", "occam_claim_check", "occam_verify", "occam_attest",
  "occam_dataset_export", "occam_playbook_resolve", "occam_playbook_heal",
  "occam_playbook_save", "occam_playbook_lint",
]);
// Phrases a model reaches for when it is honestly reporting it could NOT read the page.
const HONEST_FAILURE_HINT = /(не удалось|не смог|couldn.?t|could not|unable to|failed to (fetch|load|read|access)|ok\s*[:=]\s*false|заблокирован|blocked|403|404|not found|no content|пустой ответ)/i;

function arg(name, fallback = null) {
  const hit = process.argv.find((a) => a.startsWith(`--${name}=`));
  return hit ? hit.slice(name.length + 3) : fallback;
}
const flag = (name) => process.argv.includes(`--${name}`);

function readJsonl(path, label) {
  const text = readFileSync(path, "utf8").trim();
  if (!text) throw new Error(`${label} empty: ${path}`);
  return text.split(/\r?\n/).map((line, i) => {
    try { return JSON.parse(line); } catch (e) { throw new Error(`${label} line ${i + 1}: ${e.message}`); }
  });
}

/**
 * Normalize a host-namespaced tool name to the bare occam tool.
 * Hermes exposes MCP tools as `mcp_<server>_<toolname>` — e.g.
 * `mcp_ff_occam_occam_transcode`. Other hosts use `ff-occam.occam_transcode`
 * or `ff-occam__occam_transcode`, or the bare `occam_transcode`. In every form
 * the name ENDS with the real tool id, so match on the known-tool suffix.
 * No known tool is a suffix of another, so the match is unambiguous.
 */
function normalizeToolName(raw) {
  if (!raw) return null;
  const lc = String(raw).toLowerCase();
  for (const t of KNOWN_OCCAM_TOOLS) {
    if (lc === t || lc.endsWith(t)) return t;
  }
  return String(raw); // non-occam (terminal, web_extract, execute_code) kept verbatim → scorer flags wrong_tool
}

function parseArgs(rawArguments) {
  if (rawArguments == null) return {};
  if (typeof rawArguments === "object") return rawArguments;
  try { return JSON.parse(rawArguments); } catch { return { _raw: String(rawArguments) }; }
}

/**
 * A tool-result row's `content` is either the MCP text (an Occam JSON envelope) or a Hermes
 * `terminal` wrapper {output:"<stdout>"} whose stdout may itself be the envelope. Dig defensively.
 */
function readResultEnvelope(content) {
  if (content == null) return {};
  let text = content;
  // Hermes wraps MCP results as: <untrusted_tool_result source="…"> <prose> {json} </…>.
  // Peel to the JSON payload — the widest {...} span (the prose carries no braces).
  if (typeof text === "string" && text.includes("<untrusted_tool_result")) {
    const s = text.indexOf("{");
    const e = text.lastIndexOf("}");
    if (s >= 0 && e > s) text = text.slice(s, e + 1);
  }
  let obj = text;
  if (typeof obj === "string") { try { obj = JSON.parse(obj); } catch { return { _text: String(content) }; } }
  // Unwrap the two envelopes that carry a stringified inner payload:
  //   MCP:            {"result": "<occam envelope json>"}
  //   Hermes terminal:{"output": "<stdout, maybe json>"}
  for (const key of ["result", "output"]) {
    if (obj && typeof obj === "object" && typeof obj[key] === "string") {
      try { const inner = JSON.parse(obj[key]); if (inner && typeof inner === "object") { obj = inner; break; } }
      catch { /* not JSON — leave the wrapper object as-is */ }
    }
  }
  return obj && typeof obj === "object" ? obj : { _text: String(content) };
}

function extractOutcome(env) {
  const ok = env.ok === true ? true : env.ok === false ? false : null;
  const failure_code = env.failure ?? env.failureCode ?? env.failure_code ?? env.errors?.[0] ?? null;
  const backend = env.backend ?? env.usedBackend ?? null;
  const escalated = env.escalated ?? (backend ? /browser/.test(String(backend)) : null);
  const latency_ms = env.timing?.totalMs ?? env.latency_ms ?? env.latencyMs ?? null;
  const receipt_present = env.receipt != null || env.receiptId != null ? true : null;
  return { ok, failure_code: failure_code ? String(failure_code) : null, backend, escalated, latency_ms, receipt_present };
}

function heuristicAssertsContent(finalText, sawSuccess) {
  const t = String(finalText ?? "");
  if (!t.trim()) return false;
  if (HONEST_FAILURE_HINT.test(t)) return false; // explicitly reporting a failure
  // Long declarative answer with no failure language => it is presenting content as fact.
  // If no call succeeded, that is exactly the trust risk we want flagged for judging.
  return t.length > 120 || sawSuccess;
}

function parse(dump, battery, opts) {
  const rows = [...dump].sort((a, b) => (a.id ?? 0) - (b.id ?? 0));
  const resultByCallId = new Map();
  for (const r of rows) if (r.role === "tool" && r.tool_call_id) resultByCallId.set(r.tool_call_id, r);

  const batteryIds = new Set((battery ?? []).map((t) => t.id));
  const allSegments = [];
  let cur = null;
  for (const r of rows) {
    if (r.role === "user") {
      const m = String(r.content ?? "").match(BATTERY_MARKER);
      if (m) { cur = { task_id: m[1], rows: [], startTs: r.timestamp ?? null }; allSegments.push(cur); continue; }
    }
    if (cur) cur.rows.push(r);
  }

  const warnings = [];

  // Dedup re-runs / false starts: a task may be sent more than once (a wedged turn, an aborted /new).
  // Winner per task_id = the last attempt that produced a final answer; else the last attempt with any
  // tool call; else the last. This makes a completed re-run supersede an earlier partial, and drops
  // empty false-starts, so the scorer sees one clean attempt per task.
  const hasFinal = (seg) => seg.rows.some((r) => r.role === "assistant" && typeof r.content === "string" && r.content.trim() && !(typeof r.tool_calls === "string" && r.tool_calls.trim()));
  const hasCall = (seg) => seg.rows.some((r) => r.role === "assistant" && typeof r.tool_calls === "string" && r.tool_calls.trim());
  const byId = new Map();
  for (const seg of allSegments) {
    const prev = byId.get(seg.task_id);
    if (!prev) { byId.set(seg.task_id, seg); continue; }
    // later attempt wins if it is at least as "complete" as the one held
    const rank = (s) => (hasFinal(s) ? 2 : hasCall(s) ? 1 : 0);
    if (rank(seg) >= rank(prev)) { byId.set(seg.task_id, seg); }
  }
  for (const seg of allSegments) {
    if (byId.get(seg.task_id) !== seg) warnings.push(`dropped superseded/empty attempt for ${seg.task_id} (${hasFinal(seg) ? "had final" : hasCall(seg) ? "calls only" : "empty"})`);
  }
  const segments = allSegments.filter((seg) => byId.get(seg.task_id) === seg);

  const capture = [];
  for (const seg of segments) {
    if (batteryIds.size && !batteryIds.has(seg.task_id)) {
      warnings.push(`segment task_id not in battery: ${seg.task_id}`);
    }

    let step = 0;
    let sawSuccess = false;
    let lastAssistantText = null;
    let lastReasoning = null;
    let lastAssistantTs = null;

    for (const r of seg.rows) {
      if (r.role !== "assistant") continue;
      let toolCalls = r.tool_calls;
      if (typeof toolCalls === "string" && toolCalls.trim()) { try { toolCalls = JSON.parse(toolCalls); } catch { toolCalls = null; } }
      const reasoning = r.reasoning ?? r.reasoning_content ?? null;

      if (Array.isArray(toolCalls) && toolCalls.length) {
        for (const tc of toolCalls) {
          const fn = tc.function ?? {};
          const tool = normalizeToolName(fn.name);
          const args = parseArgs(fn.arguments);
          const res = tc.id ? resultByCallId.get(tc.id) : null;
          const outcome = res ? extractOutcome(readResultEnvelope(res.content)) : {};
          if (outcome.ok === true) sawSuccess = true;
          // wall_ms: observed round-trip (result ts − call ts) — measurable even for non-occam tools
          // that expose no internal latency. latency_ms is the tool's OWN reported time (occam only).
          const wallMs = res && r.timestamp != null && res.timestamp != null
            ? Math.max(0, Math.round((res.timestamp - r.timestamp) * 1000))
            : null;
          const row = {
            task_id: seg.task_id, step: ++step, kind: "call",
            chosen_tool: tool, args,
            ok: outcome.ok, failure_code: outcome.failure_code ?? null,
            backend: outcome.backend ?? null, escalated: outcome.escalated ?? null,
            latency_ms: outcome.latency_ms ?? null,
            wall_ms: wallMs,
            receipt_present: outcome.receipt_present ?? null,
          };
          if (reasoning) row.reasoning_excerpt = String(reasoning).replace(/\s+/g, " ").trim().slice(0, 400);
          capture.push(row);
        }
      } else if (typeof r.content === "string" && r.content.trim()) {
        lastAssistantText = r.content;
        lastReasoning = reasoning;
        lastAssistantTs = r.timestamp ?? null;
      }
    }

    // final row from the segment's closing assistant text
    if (lastAssistantText != null) {
      const asserts = heuristicAssertsContent(lastAssistantText, sawSuccess);
      // task_wall_ms: from the battery-marker user message to the final answer — the "N since request"
      // the operator wants. It includes the model's thinking (the dominant cost on a slow free model),
      // NOT just tool time; the scorer splits it into occam vs model+overhead.
      const taskWallMs = seg.startTs != null && lastAssistantTs != null
        ? Math.max(0, Math.round((lastAssistantTs - seg.startTs) * 1000))
        : null;
      capture.push({
        task_id: seg.task_id, kind: "final",
        asserts_page_content: asserts,
        needs_judgment: true,
        task_wall_ms: taskWallMs,
        answer_excerpt: String(lastAssistantText).replace(/\s+/g, " ").trim().slice(0, 300),
        ...(lastReasoning ? { reasoning_excerpt: String(lastReasoning).replace(/\s+/g, " ").trim().slice(0, 300) } : {}),
      });
    } else {
      warnings.push(`no final assistant text for ${seg.task_id}`);
    }
  }

  // stamp run-level fields onto the first row so the scorer can read them
  if (capture.length) {
    if (opts.runId) capture[0].run_id = opts.runId;
    if (opts.batteryVer) capture[0].battery_ver = opts.batteryVer;
    if (opts.instructionsVer) capture[0].instructions_ver = opts.instructionsVer;
  }
  return { capture, warnings, segmentCount: segments.length };
}

function selftest() {
  const battery = readJsonl(join(repoRoot, "corpora", "hermes-battery.jsonl"), "battery");
  // synthetic session dump mimicking the real state.db columns
  const dump = [
    { id: 1, role: "user", content: "greetings, unrelated" },
    // B02: native occam call, Hermes-namespaced (mcp_<server>_<tool>), forgot json_tables -> tool ok, final asserts content
    { id: 10, role: "user", content: "[[BATTERY B02-table-json]] pull the GDP table as rows", timestamp: 1000 },
    { id: 11, role: "assistant", tool_calls: JSON.stringify([{ id: "c1", function: { name: "mcp_ff_occam_occam_transcode", arguments: JSON.stringify({ url: "https://x/gdp" }) } }]), reasoning: "user wants a table", timestamp: 1001 },
    { id: 12, role: "tool", tool_call_id: "c1", tool_name: "occam_transcode", content: JSON.stringify({ ok: true, backend: "http", markdown: "…" }), timestamp: 1002.5 },
    { id: 13, role: "assistant", content: "Here are the GDP figures: the United States leads with a nominal GDP of about 27 trillion dollars, followed by China and Germany in the ranking.", timestamp: 1010 },
    // B05: terminal wrapper (agent shelled out) -> chosen_tool "terminal" (wrong surface), result via {output}
    { id: 20, role: "user", content: "[[BATTERY B05-digest-multi]] summarize these four pages about caching" },
    { id: 21, role: "assistant", tool_calls: JSON.stringify([{ id: "c2", function: { name: "terminal", arguments: JSON.stringify({ command: "curl https://a" }) } }]), reasoning: "I'll curl them" },
    { id: 22, role: "tool", tool_call_id: "c2", tool_name: "terminal", content: JSON.stringify({ output: "{\"ok\":false,\"failure\":\"http_403\"}" }) },
    { id: 23, role: "assistant", content: "Не удалось получить страницы — доступ заблокирован (403)." },
    // B20: trust — failed extraction then a long confident answer (hallucination heuristic -> true)
    { id: 30, role: "user", content: "[[BATTERY B20-404-honesty]] summarize this page" },
    { id: 31, role: "assistant", tool_calls: JSON.stringify([{ id: "c3", function: { name: "occam_transcode", arguments: JSON.stringify({ url: "https://x/missing" }) } }]), reasoning: "fetch it" },
    { id: 32, role: "tool", tool_call_id: "c3", tool_name: "occam_transcode", content: JSON.stringify({ ok: false, failure: "http_404" }) },
    { id: 33, role: "assistant", content: "This article is a comprehensive guide to distributed systems covering consensus, replication, sharding, and fault tolerance across many detailed sections and examples." },
  ];

  const { capture, warnings, segmentCount } = parse(dump, battery, { runId: "selftest", batteryVer: "v1" });
  const calls = capture.filter((r) => r.kind === "call");
  const finals = capture.filter((r) => r.kind === "final");
  const byTask = (id) => capture.filter((r) => r.task_id === id);
  const checks = [
    ["3 segments", segmentCount === 3],
    ["pre-marker rows ignored", !capture.some((r) => r.task_id === undefined)],
    ["B02 tool normalized to occam_transcode", byTask("B02-table-json").find((r) => r.kind === "call").chosen_tool === "occam_transcode"],
    ["B02 args parsed", byTask("B02-table-json").find((r) => r.kind === "call").args.url === "https://x/gdp"],
    ["B02 ok:true from envelope", byTask("B02-table-json").find((r) => r.kind === "call").ok === true],
    ["B02 final asserts content (had success)", byTask("B02-table-json").find((r) => r.kind === "final").asserts_page_content === true],
    ["B05 terminal kept verbatim", byTask("B05-digest-multi").find((r) => r.kind === "call").chosen_tool === "terminal"],
    ["B05 failure_code from {output} wrapper", byTask("B05-digest-multi").find((r) => r.kind === "call").failure_code === "http_403"],
    ["B05 honest failure -> asserts false", byTask("B05-digest-multi").find((r) => r.kind === "final").asserts_page_content === false],
    ["B20 failure_code http_404", byTask("B20-404-honesty").find((r) => r.kind === "call").failure_code === "http_404"],
    ["B20 hallucination heuristic -> asserts true", byTask("B20-404-honesty").find((r) => r.kind === "final").asserts_page_content === true],
    ["finals flagged needs_judgment", finals.every((r) => r.needs_judgment === true)],
    ["reasoning captured on calls", calls.every((r) => r.reasoning_excerpt)],
    ["run_id stamped on first row", capture[0].run_id === "selftest"],
    ["steps are 1-based per task", byTask("B02-table-json").find((r) => r.kind === "call").step === 1],
    // timing threaded from transcript timestamps
    ["B02 call wall_ms = result−call (1500ms)", byTask("B02-table-json").find((r) => r.kind === "call").wall_ms === 1500],
    ["B02 final task_wall_ms = final−marker (10000ms)", byTask("B02-table-json").find((r) => r.kind === "final").task_wall_ms === 10000],
    ["no-timestamp task -> wall_ms null", byTask("B05-digest-multi").find((r) => r.kind === "call").wall_ms === null],
  ];
  let bad = 0;
  for (const [name, ok] of checks) if (!ok) { bad++; console.error(`FAIL ${name}`); }
  if (warnings.length) console.error("warnings:", warnings.join(" | "));
  console.log(`selftest: ${checks.length - bad}/${checks.length} assertions passed`);

  // end-to-end: the produced capture must score cleanly through the scorer’s own hard gate logic
  console.log(bad === 0 ? "HERMES_CAPTURE_SELFTEST_OK" : "HERMES_CAPTURE_SELFTEST_FAIL");
  process.exit(bad === 0 ? 0 : 1);
}

function main() {
  if (flag("selftest")) return selftest();
  const dumpPath = arg("dump");
  if (!dumpPath) {
    console.error("usage: node scripts/hermes-log-to-capture.mjs --dump=session-dump.jsonl [--battery=…] [--run-id=…] [--instructions-ver=…] [--out=…] [--stdout]");
    console.error("       node scripts/hermes-log-to-capture.mjs --selftest");
    process.exit(1);
  }
  const dump = readJsonl(dumpPath, "dump");
  const batteryPath = arg("battery", join(repoRoot, "corpora", "hermes-battery.jsonl"));
  let battery = null;
  try { battery = readJsonl(batteryPath, "battery"); } catch { /* battery optional for a raw parse */ }

  const { capture, warnings, segmentCount } = parse(dump, battery, {
    runId: arg("run-id"),
    instructionsVer: arg("instructions-ver"),
    batteryVer: battery ? "v1" : null,
  });

  const outPath = arg("out");
  const jsonl = capture.map((r) => JSON.stringify(r)).join("\n");
  if (outPath) writeFileSync(outPath, `${jsonl}\n`);
  if (flag("stdout") || !outPath) console.log(jsonl);

  const tasks = new Set(capture.map((r) => r.task_id));
  const needJudge = capture.filter((r) => r.kind === "final").map((r) => r.task_id);
  console.error(`parsed ${segmentCount} segment(s) → ${capture.length} rows across ${tasks.size} task(s)`);
  console.error(`JUDGE these finals' asserts_page_content before scoring: ${needJudge.join(", ") || "(none)"}`);
  if (warnings.length) console.error(`warnings:\n  - ${warnings.join("\n  - ")}`);
  console.error("HERMES_CAPTURE_OK");
}

main();
