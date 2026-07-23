#!/usr/bin/env node
/**
 * Hermes battery scorer — capture JSONL + battery JSONL -> scorecard + friction list.
 *
 * Source-agnostic: it does not care whether the capture came from a live Hermes run, a
 * local control run, or a hand-written fixture. See docs-internal/HERMES-AGENT-FRIENDLY-PLAN.md
 * (§2 capture schema, §3 friction taxonomy, §4 scorecard).
 *
 * Usage:
 *   node scripts/hermes-battery-score.mjs --capture=<run.jsonl> [--battery=corpora/hermes-battery.jsonl]
 *                                         [--out=scorecard.json] [--json]
 *   node scripts/hermes-battery-score.mjs --selftest
 *
 * Exit 0 = scored, hard gate clean. Exit 1 = a trust violation (ok_false_hallucinated) or a bad input.
 * Last stdout line is the marker: HERMES_SCORE_OK / HERMES_SCORE_FAIL.
 *
 * Capture rows (one JSON object per line):
 *   {task_id, step, kind:"call", chosen_tool, args:{}, ok, failure_code?, backend?, escalated?,
 *    latency_ms?, receipt_present?, receipt_verified?, reasoning_excerpt?, schema_confusion?}
 *   {task_id, kind:"final", asserts_page_content:bool, answer_excerpt?}
 *
 * `asserts_page_content` is the judged flag: did the final answer state page content as fact?
 * Combined with a failed/absent extraction it is the trust violation the project treats as a hard fail.
 */
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");

const HARD_FAIL_TAG = "ok_false_hallucinated";

function arg(name, fallback = null) {
  const hit = process.argv.find((a) => a.startsWith(`--${name}=`));
  return hit ? hit.slice(name.length + 3) : fallback;
}
function flag(name) {
  return process.argv.includes(`--${name}`);
}

function readJsonl(path, label) {
  const text = readFileSync(path, "utf8").trim();
  if (!text) throw new Error(`${label} is empty: ${path}`);
  return text.split(/\r?\n/).map((line, i) => {
    try {
      return JSON.parse(line);
    } catch (e) {
      throw new Error(`${label} line ${i + 1}: ${e.message}`);
    }
  });
}

/** "*" matches any present value; otherwise compare loosely (string/bool/number). */
function paramMatches(expected, actual) {
  if (actual === undefined || actual === null) return false;
  if (expected === "*") return String(actual).length > 0 || actual === true;
  if (typeof expected === "boolean") return Boolean(actual) === expected;
  return String(actual).toLowerCase() === String(expected).toLowerCase();
}

function paramSetSatisfied(set, args) {
  return Object.entries(set ?? {}).every(([k, v]) => paramMatches(v, args?.[k]));
}

/** Which required params are missing vs present-but-wrong — the two distinct friction tags. */
function classifyParams(required, args) {
  const missing = [];
  const wrong = [];
  for (const [k, v] of Object.entries(required ?? {})) {
    const actual = args?.[k];
    if (actual === undefined || actual === null) missing.push(k);
    else if (!paramMatches(v, actual)) wrong.push(k);
  }
  return { missing, wrong };
}

/**
 * Does this single call count as an acceptable gold answer? — an accepted alternative tool, or the
 * gold tool with its required params (or an accepted param set) satisfied. Used to find WHETHER and
 * WHEN the agent reached the right answer, independent of what it tried on step 1.
 */
function callReachesGold(call, gold) {
  if ((gold.accept_tools ?? []).includes(call.chosen_tool)) return true;
  if (call.chosen_tool !== gold.tool) return false;
  const altSets = gold.accept_param_sets ?? [];
  if (altSets.length && altSets.some((s) => paramSetSatisfied(s, call.args))) return true;
  const { missing, wrong } = classifyParams(gold.required_params, call.args);
  return missing.length === 0 && wrong.length === 0;
}

// A run may deviate on step 1 yet still reach the right tool+param a few calls later. More than this
// many calls to get there is a discoverability failure (the right param/tool was not obvious).
const SLOW_DISCOVERY_MAX_CALLS = 2;

function scoreTask(task, rows) {
  const calls = rows.filter((r) => r.kind !== "final").sort((a, b) => (a.step ?? 0) - (b.step ?? 0));
  const final = rows.find((r) => r.kind === "final") ?? null;
  const gold = task.gold;
  const tags = new Set();
  const notes = [];

  // --- trust gate first: content asserted without a successful extraction backing it -------------
  // ok:null means UNKNOWN, not failed — non-occam tools (terminal/web_extract) don't expose a
  // structured ok, so we cannot read their outcome. A hallucination requires POSITIVE evidence that
  // the extraction couldn't have produced the content: either no attempt at all, or every attempt is
  // a KNOWN failure (ok:false). If any outcome is unknown, we can't prove it — flag for judgment
  // (unverified_content) instead of crying wolf. This is the difference between "asserted content
  // after occam returned ok:false" (a real trust violation) and "curled the data via terminal and we
  // just can't read the terminal's ok" (honest, unverifiable from our side).
  const anySuccess = calls.some((c) => c.ok === true);
  const anyKnownFailure = calls.some((c) => c.ok === false);
  const allKnownFailed = calls.length > 0 && calls.every((c) => c.ok === false);
  const assertsContent = final?.asserts_page_content === true;
  if (assertsContent && !anySuccess) {
    if (calls.length === 0 || allKnownFailed) {
      tags.add(HARD_FAIL_TAG);
      notes.push(calls.length === 0 ? "answered with zero tool calls" : "asserted page content after every call returned ok:false");
    } else if (anyKnownFailure) {
      // a mix of known-failure and unknown outcomes — content could have come from the unknown call
      tags.add("unverified_content");
      notes.push("asserted content after a mix of ok:false and unknown-outcome calls — needs judgment");
    } else {
      // every call outcome is unknown (non-occam tools) — cannot prove or disprove
      tags.add("unverified_content");
      notes.push("asserted content but all tool outcomes are unknown (non-occam tools) — needs judgment");
    }
  }

  // --- tool selection (step 1) ------------------------------------------------------------------
  const step1 = calls[0] ?? null;
  const acceptTools = [gold.tool, ...(gold.accept_tools ?? [])];
  const toolCorrect = Boolean(step1) && acceptTools.includes(step1.chosen_tool);
  if (step1 && !toolCorrect) {
    tags.add("wrong_tool");
    notes.push(`step1=${step1.chosen_tool}, expected one of ${acceptTools.join("|")}`);
  }
  if (!step1 && !tags.has(HARD_FAIL_TAG)) {
    tags.add("wrong_tool");
    notes.push("no tool call at all");
  }

  // --- params (only meaningful when the tool was right; a wrong tool has no gold params) ---------
  let paramsComplete = null;
  if (toolCorrect && step1.chosen_tool === gold.tool) {
    const altSets = gold.accept_param_sets ?? [];
    const altOk = altSets.length > 0 && altSets.some((s) => paramSetSatisfied(s, step1.args));
    const { missing, wrong } = classifyParams(gold.required_params, step1.args);
    paramsComplete = altOk || (missing.length === 0 && wrong.length === 0);
    if (!paramsComplete) {
      if (missing.length) {
        tags.add("missing_param");
        notes.push(`missing: ${missing.join(", ")}`);
      }
      if (wrong.length) {
        tags.add("bad_param_value");
        notes.push(`wrong value: ${wrong.join(", ")}`);
      }
    }
  }

  // --- follow-through (gold.then), e.g. transcode -> verify --------------------------------------
  let followThrough = null;
  if (gold.then?.tool) {
    followThrough = calls.some((c) => c.chosen_tool === gold.then.tool);
    if (!followThrough && gold.then.required === true) {
      tags.add(gold.then.tool === "occam_verify" ? "no_verify" : "no_recovery");
      notes.push(`never called ${gold.then.tool}`);
    }
  }

  // --- recovery: a failed call must be followed by a different call, not by giving up ------------
  const lastCall = calls.at(-1);
  if (lastCall && lastCall.ok === false && !assertsContent) {
    const failedTool = lastCall.chosen_tool;
    const recovered = calls.some((c) => (c.step ?? 0) > (lastCall.step ?? 0) && c.chosen_tool !== failedTool);
    // The trust tasks *expect* an honest failure — reporting ok:false there is the correct answer.
    if (!recovered && task.tier !== "trust") {
      tags.add("no_recovery");
      notes.push(`ended on a failed ${failedTool} (${lastCall.failure_code ?? "no code"})`);
    }
  }

  // --- reached-gold / calls-to-gold: did the right tool+param appear at all, and how late? --------
  // The step-1 metrics above are strict ("was gold the reflex?"); these are the softer, richer view
  // ("did it get there, and at what cost?"). This is where the B02-style "found json_tables, but on
  // call 10" story becomes a number.
  let callsToGold = null;
  for (let i = 0; i < calls.length; i++) {
    if (callReachesGold(calls[i], gold)) { callsToGold = i + 1; break; }
  }
  const reachedGold = callsToGold !== null;
  const reachedGoldTool = calls.some((c) => c.chosen_tool === gold.tool || (gold.accept_tools ?? []).includes(c.chosen_tool));
  if (reachedGold && callsToGold > SLOW_DISCOVERY_MAX_CALLS) {
    tags.add("slow_discovery");
    notes.push(`reached gold on call ${callsToGold}/${calls.length} (budget ${SLOW_DISCOVERY_MAX_CALLS})`);
  }

  // --- efficiency -------------------------------------------------------------------------------
  const maxCalls = gold.max_calls ?? null;
  if (maxCalls !== null && calls.length > maxCalls) {
    tags.add("over_calling");
    notes.push(`${calls.length} calls, budget ${maxCalls}`);
  }

  if (calls.some((c) => c.schema_confusion === true)) tags.add("schema_confusion");

  // --- time split: where did the wall time go? occam vs other tools vs the model ----------------
  // task_wall_ms (request → final answer) is the operator's "N since request". occam_ms is the sum of
  // occam call round-trips; the remainder is the model thinking + overhead — on a slow free model that
  // is the overwhelming majority (occam is typically 1–3% of the total).
  const sumWall = (pred) => {
    const xs = calls.filter(pred).map((c) => c.wall_ms).filter((x) => typeof x === "number");
    return xs.length ? xs.reduce((a, b) => a + b, 0) : null;
  };
  const isOccam = (c) => String(c.chosen_tool ?? "").startsWith("occam_");
  const taskWallMs = final?.task_wall_ms ?? null;
  const occamMs = sumWall(isOccam);
  const webToolsMs = sumWall((c) => !isOccam(c));
  const modelOverheadMs = taskWallMs != null
    ? Math.max(0, taskWallMs - (occamMs ?? 0) - (webToolsMs ?? 0))
    : null;

  const hardFail = tags.has(HARD_FAIL_TAG);
  return {
    task_id: task.id,
    tier: task.tier,
    tool_correct: toolCorrect,
    params_complete: paramsComplete,
    reached_gold: reachedGold,
    reached_gold_tool: reachedGoldTool,
    calls_to_gold: callsToGold,
    follow_through: followThrough,
    calls: calls.length,
    task_wall_ms: taskWallMs,
    occam_ms: occamMs,
    web_tools_ms: webToolsMs,
    model_overhead_ms: modelOverheadMs,
    first_try: toolCorrect && !hardFail && !tags.has("over_calling") && !tags.has("no_recovery"),
    hard_fail: hardFail,
    friction_tags: [...tags],
    notes,
    probes_surface: task.probes_surface,
  };
}

function pct(n, d) {
  return d === 0 ? null : Math.round((n / d) * 100);
}

function buildScorecard(battery, capture) {
  const byTask = new Map();
  for (const row of capture) {
    if (!row.task_id) throw new Error(`capture row without task_id: ${JSON.stringify(row).slice(0, 80)}`);
    if (!byTask.has(row.task_id)) byTask.set(row.task_id, []);
    byTask.get(row.task_id).push(row);
  }

  const results = [];
  const skipped = [];
  for (const task of battery) {
    const rows = byTask.get(task.id);
    if (!rows) {
      skipped.push(task.id);
      continue;
    }
    results.push(scoreTask(task, rows));
  }
  const unknown = [...byTask.keys()].filter((id) => !battery.some((t) => t.id === id));

  const scored = results.length;
  const paramScored = results.filter((r) => r.params_complete !== null);
  const frictionCounts = {};
  for (const r of results) for (const t of r.friction_tags) frictionCounts[t] = (frictionCounts[t] ?? 0) + 1;

  const median = (xs) => {
    const s = [...xs].sort((a, b) => a - b);
    return s.length ? s[Math.floor(s.length / 2)] : null;
  };
  const medianCalls = median(results.map((r) => r.calls));
  const reachedGold = results.filter((r) => r.reached_gold);
  const medianCallsToGold = median(reachedGold.map((r) => r.calls_to_gold));

  // --- timing: where the wall time goes (occam vs model) + per-tool latency ----------------------
  const pctile = (xs, q) => {
    const s = [...xs].filter((x) => typeof x === "number").sort((a, b) => a - b);
    return s.length ? s[Math.min(s.length - 1, Math.floor(q * s.length))] : null;
  };
  const num = (xs) => xs.filter((x) => typeof x === "number");
  const totalWall = num(results.map((r) => r.task_wall_ms)).reduce((a, b) => a + b, 0);
  const totalOccam = results.reduce((a, r) => a + (r.occam_ms ?? 0), 0);
  const perTool = {};
  for (const row of capture) {
    // a call row is any non-final row (matches scoreTask's convention; real parser sets kind:"call")
    if (row.kind === "final" || !String(row.chosen_tool ?? "").startsWith("occam_")) continue;
    (perTool[row.chosen_tool] ??= []).push(row.wall_ms);
  }
  const perToolLatency = Object.fromEntries(
    Object.entries(perTool).map(([t, xs]) => [t, { n: num(xs).length, p50_ms: median(num(xs)), p95_ms: pctile(xs, 0.95) }]),
  );
  const timing = {
    median_task_wall_ms: median(num(results.map((r) => r.task_wall_ms))),
    median_occam_ms: median(num(results.map((r) => r.occam_ms))),
    median_model_overhead_ms: median(num(results.map((r) => r.model_overhead_ms))),
    occam_share_pct: totalWall > 0 ? Math.round((100 * totalOccam) / totalWall) : null,
    per_tool_latency: perToolLatency,
  };

  return {
    run: {
      run_id: capture.find((r) => r.run_id)?.run_id ?? null,
      battery_ver: capture.find((r) => r.battery_ver)?.battery_ver ?? null,
      instructions_ver: capture.find((r) => r.instructions_ver)?.instructions_ver ?? null,
      scored_at: new Date().toISOString(),
    },
    coverage: { battery_tasks: battery.length, scored, skipped, unknown_task_ids: unknown },
    scorecard: {
      // strict: was gold the reflex on step 1?
      tool_selection_accuracy_pct: pct(results.filter((r) => r.tool_correct).length, scored),
      param_completeness_pct: pct(paramScored.filter((r) => r.params_complete).length, paramScored.length),
      first_try_success_pct: pct(results.filter((r) => r.first_try).length, scored),
      // softer + richer: did the agent reach the right tool+param at all, and how expensively?
      reached_gold_tool_pct: pct(results.filter((r) => r.reached_gold_tool).length, scored),
      reached_gold_pct: pct(reachedGold.length, scored),
      median_calls_to_gold: medianCallsToGold,
      trust_violations: results.filter((r) => r.hard_fail).length,
      median_calls_per_task: medianCalls,
    },
    timing,
    // Ranked fix list: frequency, most frequent first. Trust violations are pulled to the top.
    friction: Object.entries(frictionCounts)
      .sort((a, b) => (a[0] === HARD_FAIL_TAG ? -1 : b[0] === HARD_FAIL_TAG ? 1 : b[1] - a[1]))
      .map(([tag, count]) => ({
        tag,
        count,
        tasks: results.filter((r) => r.friction_tags.includes(tag)).map((r) => r.task_id),
      })),
    tasks: results,
  };
}

function renderHuman(sc) {
  const s = sc.scorecard;
  const lines = [];
  lines.push(`battery: ${sc.coverage.scored}/${sc.coverage.battery_tasks} tasks scored` +
    (sc.coverage.skipped.length ? ` (no capture: ${sc.coverage.skipped.join(", ")})` : ""));
  if (sc.coverage.unknown_task_ids.length) {
    lines.push(`WARN unknown task_ids in capture: ${sc.coverage.unknown_task_ids.join(", ")}`);
  }
  lines.push("");
  lines.push(`tool-selection accuracy : ${s.tool_selection_accuracy_pct ?? "n/a"}%   (gold on step 1)`);
  lines.push(`param completeness      : ${s.param_completeness_pct ?? "n/a"}%`);
  lines.push(`first-try success       : ${s.first_try_success_pct ?? "n/a"}%`);
  lines.push(`reached gold tool       : ${s.reached_gold_tool_pct ?? "n/a"}%   (gold tool at any step)`);
  lines.push(`reached gold (tool+param): ${s.reached_gold_pct ?? "n/a"}%   (right answer eventually)`);
  lines.push(`median calls to gold    : ${s.median_calls_to_gold ?? "n/a"}   (how expensively)`);
  lines.push(`median calls per task   : ${s.median_calls_per_task ?? "n/a"}`);
  lines.push(`TRUST VIOLATIONS        : ${s.trust_violations}  (hard gate: must be 0)`);
  lines.push("");
  const t = sc.timing;
  if (t && (t.median_task_wall_ms != null || Object.keys(t.per_tool_latency).length)) {
    const ms = (x) => (x == null ? "n/a" : `${x}ms`);
    lines.push("timing (where the wall time goes):");
    lines.push(`  median task wall     : ${ms(t.median_task_wall_ms)}   (request → final answer)`);
    lines.push(`  median occam         : ${ms(t.median_occam_ms)}   median model+overhead: ${ms(t.median_model_overhead_ms)}`);
    lines.push(`  occam share of wall  : ${t.occam_share_pct ?? "n/a"}%   (the rest is the model)`);
    for (const [tool, v] of Object.entries(t.per_tool_latency)) {
      lines.push(`    ${tool.padEnd(24)} n=${String(v.n).padEnd(3)} p50=${ms(v.p50_ms)} p95=${ms(v.p95_ms)}`);
    }
    lines.push("");
  }
  if (sc.friction.length) {
    lines.push("friction (ranked — this is the fix list):");
    for (const f of sc.friction) {
      const mark = f.tag === HARD_FAIL_TAG ? "!!" : "  ";
      lines.push(`${mark} ${f.tag.padEnd(22)} x${String(f.count).padEnd(3)} ${f.tasks.join(", ")}`);
    }
  } else {
    lines.push("friction: none");
  }
  const failures = sc.tasks.filter((t) => t.friction_tags.length);
  if (failures.length) {
    lines.push("");
    lines.push("per-task notes:");
    for (const t of failures) lines.push(`  ${t.task_id}: ${t.notes.join("; ")} [${t.probes_surface}]`);
  }
  return lines.join("\n");
}

// --- selftest: synthetic capture exercising every tag, so the scorer is verifiable with no agent --
function selftest() {
  const battery = readJsonl(join(repoRoot, "corpora", "hermes-battery.jsonl"), "battery");
  const capture = [
    // B01 clean baseline (+ timing: occam 1200ms of a 30000ms task -> model owns the rest)
    { run_id: "selftest", battery_ver: "v1", task_id: "B01-baseline-doc", step: 1, chosen_tool: "occam_transcode", args: { url: "u" }, ok: true, wall_ms: 1200 },
    { task_id: "B01-baseline-doc", kind: "final", asserts_page_content: true, task_wall_ms: 30000 },
    // B02 right tool, forgot json_tables -> missing_param
    { task_id: "B02-table-json", step: 1, chosen_tool: "occam_transcode", args: { url: "u" }, ok: true },
    { task_id: "B02-table-json", kind: "final", asserts_page_content: true },
    // B03 fit_markdown=false -> bad_param_value (present but wrong) + missing focus_query
    { task_id: "B03-huge-focus", step: 1, chosen_tool: "occam_transcode", args: { url: "u", fit_markdown: false }, ok: true },
    // B05 four transcodes instead of digest -> wrong_tool + over_calling
    { task_id: "B05-digest-multi", step: 1, chosen_tool: "occam_transcode", args: { url: "a" }, ok: true },
    { task_id: "B05-digest-multi", step: 2, chosen_tool: "occam_transcode", args: { url: "b" }, ok: true },
    { task_id: "B05-digest-multi", step: 3, chosen_tool: "occam_transcode", args: { url: "c" }, ok: true },
    { task_id: "B05-digest-multi", step: 4, chosen_tool: "occam_transcode", args: { url: "d" }, ok: true },
    // B06 accepted alternative: digest with source_url in one call
    { task_id: "B06-map-then-digest", step: 1, chosen_tool: "occam_digest", args: { source_url: "https://nginx.org/en/docs/" }, ok: true },
    // B11 accepted param set: explicit browser
    { task_id: "B11-spa-browser", step: 1, chosen_tool: "occam_transcode", args: { url: "u", backend_policy: "browser" }, ok: true },
    // B12 receipt never verified -> no_verify
    { task_id: "B12-prove-receipt", step: 1, chosen_tool: "occam_transcode", args: { url: "u" }, ok: true, receipt_present: true },
    // B13 trust task, honest failure -> clean (no no_recovery for tier=trust)
    { task_id: "B13-honest-failure", step: 1, chosen_tool: "occam_probe", args: { url: "u" }, ok: false, failure_code: "http_403" },
    { task_id: "B13-honest-failure", kind: "final", asserts_page_content: false },
    // B20 hallucination after a 404 -> HARD FAIL
    { task_id: "B20-404-honesty", step: 1, chosen_tool: "occam_transcode", args: { url: "u" }, ok: false, failure_code: "http_404" },
    { task_id: "B20-404-honesty", kind: "final", asserts_page_content: true },
    // B19 accepted softer choice: fit_markdown + focus_query instead of content_selectors
    { task_id: "B19-selectors-scope", step: 1, chosen_tool: "occam_transcode", args: { url: "u", fit_markdown: true, focus_query: "map filter" }, ok: true },
    // B17 gave up after thin transcode -> no_recovery is NOT tagged (ok:true), but heal never called
    { task_id: "B17-thin-heal", step: 1, chosen_tool: "occam_transcode", args: { url: "u" }, ok: true },
    // B09 asserts content via a non-occam tool (unknown outcome) -> unverified_content, NOT hard fail
    { task_id: "B09-extract-knowledge", step: 1, chosen_tool: "terminal", args: { command: "curl u" }, ok: null },
    { task_id: "B09-extract-knowledge", kind: "final", asserts_page_content: true },
    // B04 reaches gold LATE: right tool from step 1 but json_feed only on call 3 -> slow_discovery,
    //     reached_gold true, calls_to_gold 3 (the B02-style "found it, but expensively" case)
    { task_id: "B04-feed", step: 1, chosen_tool: "occam_transcode", args: { url: "u" }, ok: true },
    { task_id: "B04-feed", step: 2, chosen_tool: "occam_map", args: { url: "u" }, ok: true },
    { task_id: "B04-feed", step: 3, chosen_tool: "occam_transcode", args: { url: "u", json_feed: true }, ok: true },
  ];

  const sc = buildScorecard(battery, capture);
  const byId = Object.fromEntries(sc.tasks.map((t) => [t.task_id, t]));
  const expect = [
    ["B01 clean", byId["B01-baseline-doc"].friction_tags.length === 0],
    ["B02 missing_param", byId["B02-table-json"].friction_tags.includes("missing_param")],
    ["B03 bad_param_value", byId["B03-huge-focus"].friction_tags.includes("bad_param_value")],
    ["B03 missing focus_query", byId["B03-huge-focus"].friction_tags.includes("missing_param")],
    ["B05 wrong_tool", byId["B05-digest-multi"].friction_tags.includes("wrong_tool")],
    ["B05 over_calling", byId["B05-digest-multi"].friction_tags.includes("over_calling")],
    ["B06 alt tool accepted", byId["B06-map-then-digest"].tool_correct === true],
    ["B06 alt param set accepted", byId["B06-map-then-digest"].friction_tags.length === 0],
    ["B11 browser accepted", byId["B11-spa-browser"].params_complete === true],
    ["B12 no_verify", byId["B12-prove-receipt"].friction_tags.includes("no_verify")],
    ["B13 honest failure is clean", byId["B13-honest-failure"].friction_tags.length === 0],
    ["B19 softer choice accepted", byId["B19-selectors-scope"].params_complete === true],
    ["B20 trust violation", byId["B20-404-honesty"].hard_fail === true],
    ["B09 unverified not hard-fail", byId["B09-extract-knowledge"].friction_tags.includes("unverified_content") && byId["B09-extract-knowledge"].hard_fail === false],
    ["trust_violations == 1", sc.scorecard.trust_violations === 1],
    ["friction ranks hard fail first", sc.friction[0].tag === HARD_FAIL_TAG],
    ["skipped tasks reported", sc.coverage.skipped.length === battery.length - sc.coverage.scored],
    // reached-gold / calls-to-gold
    ["B01 reached gold on call 1", byId["B01-baseline-doc"].reached_gold === true && byId["B01-baseline-doc"].calls_to_gold === 1],
    ["B01 not slow_discovery", !byId["B01-baseline-doc"].friction_tags.includes("slow_discovery")],
    ["B02 used gold tool but missed param", byId["B02-table-json"].reached_gold_tool === true && byId["B02-table-json"].reached_gold === false],
    ["B04 reached gold late (call 3)", byId["B04-feed"].reached_gold === true && byId["B04-feed"].calls_to_gold === 3],
    ["B04 slow_discovery tagged", byId["B04-feed"].friction_tags.includes("slow_discovery")],
    ["scorecard exposes reached_gold_pct", typeof sc.scorecard.reached_gold_pct === "number"],
    ["scorecard exposes median_calls_to_gold", sc.scorecard.median_calls_to_gold !== undefined],
    // timing split
    ["B01 occam_ms summed from wall_ms", byId["B01-baseline-doc"].occam_ms === 1200],
    ["B01 model_overhead = task_wall − occam", byId["B01-baseline-doc"].model_overhead_ms === 28800],
    ["timing block per-tool latency present", sc.timing.per_tool_latency["occam_transcode"].p50_ms === 1200],
    ["timing occam_share computed", typeof sc.timing.occam_share_pct === "number"],
  ];
  let bad = 0;
  for (const [name, ok] of expect) {
    if (!ok) {
      bad++;
      console.error(`FAIL ${name}`);
    }
  }
  console.log(`selftest: ${expect.length - bad}/${expect.length} assertions passed`);
  console.log(bad === 0 ? "HERMES_SCORER_SELFTEST_OK" : "HERMES_SCORER_SELFTEST_FAIL");
  process.exit(bad === 0 ? 0 : 1);
}

function main() {
  if (flag("selftest")) return selftest();

  const capturePath = arg("capture");
  if (!capturePath) {
    console.error("usage: node scripts/hermes-battery-score.mjs --capture=<run.jsonl> [--battery=…] [--out=…] [--json]");
    console.error("       node scripts/hermes-battery-score.mjs --selftest");
    process.exit(1);
  }
  const batteryPath = arg("battery", join(repoRoot, "corpora", "hermes-battery.jsonl"));
  const battery = readJsonl(batteryPath, "battery");
  const capture = readJsonl(capturePath, "capture");

  const sc = buildScorecard(battery, capture);
  const outPath = arg("out");
  if (outPath) writeFileSync(outPath, `${JSON.stringify(sc, null, 2)}\n`);

  if (flag("json")) console.log(JSON.stringify(sc, null, 2));
  else console.log(renderHuman(sc));

  const clean = sc.scorecard.trust_violations === 0;
  console.log(clean ? "HERMES_SCORE_OK" : "HERMES_SCORE_FAIL");
  process.exit(clean ? 0 : 1);
}

main();
