#!/usr/bin/env node
/**
 * Agent-First MVP Phase 2 — popular hosts corpus via subprocess stdio MCP.
 * Usage: node scripts/run-agent-popular-hosts.mjs [--out artifacts/agent-popular-hosts/<timestamp>.json]
 * Exit 0 when ≥12/15 rows match expected (WORKS | FAIL_HONEST | NEEDS_SESSION).
 */
import { spawn } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const corpusPath = join(root, "corpora", "agent-popular-hosts.jsonl");
const PASS_BAR = 12;
const REQUEST_TIMEOUT_MS = 180_000;

const ts = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
const outArg = process.argv.find((a) => a.startsWith("--out="));
const outPath =
  outArg?.slice("--out=".length) ||
  join(root, "artifacts", "agent-popular-hosts", `${ts}.json`);

const HONEST_CODES = new Set([
  "http_401",
  "http_403",
  "http_404",
  "http_410",
  "http_429",
  "http_5xx",
  "http_503",
  "http_error",
  "timeout",
  "network_error",
  "thin_extract",
  "captcha_or_challenge",
  "extraction_failed",
  "transcode_failed",
  "digest_failed",
  "private_url_blocked",
  "requires_login",
  "playbook_not_found",
  "sitemap_not_found",
]);

const SESSION_CODES = new Set([
  "http_401",
  "http_403",
  "requires_login",
  "likelyLoginRequired",
]);

// Live popular hosts flake transiently on CI egress (connect resets, DNS, rate limits). Retry
// once on these so genuine failures (4xx content, extraction bugs) still fail fast.
const RETRYABLE_CODES = new Set([
  "timeout",
  "network_error",
  "dns_error",
  "tls_error",
  "http_429",
  "http_503",
  "http_5xx",
  "http_error",
]);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Outcome ranking for the "over-performance is never a failure" rule. Live hosts drift: a page
// the corpus expected to FAIL_HONEST may start WORKS-ing, or a public page may begin asking for a
// session. Penalising the tool for doing *as well or better* than expected is wrong and makes the
// gate brittle. A row passes when actual ranks >= expected (and isn't FAIL_BUG/ERROR). Only doing
// WORSE than expected (regression) or a real bug counts against the gate.
const OUTCOME_RANK = { WORKS: 3, NEEDS_SESSION: 2, FAIL_HONEST: 1, FAIL_BUG: 0, ERROR: 0 };

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;

  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", () => {});
  }

  #sendLine(obj) {
    this.#proc.stdin.write(`${JSON.stringify(obj)}\n`);
  }

  #onData(chunk) {
    this.#buffer += chunk;
    for (;;) {
      const nl = this.#buffer.indexOf("\n");
      if (nl === -1) break;
      const line = this.#buffer.slice(0, nl).trim();
      this.#buffer = this.#buffer.slice(nl + 1);
      if (!line) continue;
      let msg;
      try {
        msg = JSON.parse(line);
      } catch {
        continue;
      }
      if (msg.id != null && this.#pending.has(msg.id)) {
        const { resolve, reject } = this.#pending.get(msg.id);
        this.#pending.delete(msg.id);
        if (msg.error) reject(new Error(JSON.stringify(msg.error)));
        else resolve(msg.result);
      }
    }
  }

  notify(method, params = {}) {
    this.#sendLine({ jsonrpc: "2.0", method, params });
  }

  request(method, params = {}) {
    const id = this.#id++;
    this.#sendLine({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.#pending.has(id)) {
          this.#pending.delete(id);
          reject(new Error(`MCP timeout ${REQUEST_TIMEOUT_MS}ms: ${method}`));
        }
      }, REQUEST_TIMEOUT_MS);
      this.#pending.set(id, {
        resolve: (v) => {
          clearTimeout(timer);
          resolve(v);
        },
        reject: (e) => {
          clearTimeout(timer);
          reject(e);
        },
      });
    });
  }

  close() {
    this.#proc.stdin.end();
  }
}

function parseToolJson(result) {
  if (result?.isError) {
    const text = result?.content?.find((c) => c.type === "text")?.text ?? "tool error";
    return { raw: text, parsed: null, isError: true };
  }
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return { raw: result, parsed: null, isError: false };
  try {
    return { raw: text, parsed: JSON.parse(text), isError: false };
  } catch {
    return { raw: text, parsed: null, isError: false };
  }
}

function loadJsonl(path) {
  return readFileSync(path, "utf8")
    .trim()
    .split(/\n+/)
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function collectHintActions(...parsedList) {
  const actions = new Set();
  for (const p of parsedList) {
    if (!p) continue;
    for (const d of p.agentHints?.decisions ?? []) {
      if (d?.action) actions.add(d.action);
    }
    for (const d of p.agentMeta?.decisions ?? []) {
      if (d?.action) actions.add(d.action);
    }
    const next = p.agentHints?.suggestedNext ?? p.agentHints?.suggestedNextTool;
    if (typeof next === "string" && next.includes("session")) actions.add("configure_session_profile");
  }
  return [...actions];
}

function extractFailureCode(...parsedList) {
  for (const p of parsedList) {
    if (!p) continue;
    if (p.failure?.code) return p.failure.code;
    if (p.failureCode) return p.failureCode;
    if (p.classification?.likelyLoginRequired) return "likelyLoginRequired";
  }
  return null;
}

function hintsOk(actual, failureCode, hintActions) {
  if (actual !== "NEEDS_SESSION" && failureCode !== "workers_unavailable") return true;
  return (
    hintActions.includes("configure_session_profile") ||
    hintActions.includes("run_doctor")
  );
}

function classifyTerminal(parsedList, terminalOk, mdLen = 0) {
  if (terminalOk) return "WORKS";

  const code = extractFailureCode(...parsedList);
  const hintActions = collectHintActions(...parsedList);

  if (
    SESSION_CODES.has(code) ||
    hintActions.includes("configure_session_profile") ||
    parsedList.some((p) => p?.classification?.likelyLoginRequired)
  ) {
    return "NEEDS_SESSION";
  }

  if (code && HONEST_CODES.has(code)) return "FAIL_HONEST";

  if (!terminalOk && code) return "FAIL_HONEST";

  return "FAIL_BUG";
}

async function callTool(client, name, args) {
  const result = await client.request("tools/call", { name, arguments: args });
  return parseToolJson(result);
}

async function runRecipeA(client, row) {
  const probe = await callTool(client, "occam_probe", { url: row.url });
  const transcode = await callTool(client, "occam_transcode", {
    url: row.url,
    backend_policy: "http_then_browser",
  });
  const t = transcode.parsed;
  const mdLen = typeof t?.markdown === "string" ? t.markdown.length : 0;
  const actual = classifyTerminal([probe.parsed, transcode.parsed], t?.ok === true, mdLen);
  return { probe, transcode, actual, mdLen };
}

async function runRecipeC(client, row) {
  const probe = await callTool(client, "occam_probe", { url: row.url });
  const resolve = await callTool(client, "occam_playbook_resolve", { url: row.url });
  const transcode = await callTool(client, "occam_transcode", {
    url: row.url,
    backend_policy: "http_then_browser",
    playbook_policy: "auto",
  });
  const t = transcode.parsed;
  const mdLen = typeof t?.markdown === "string" ? t.markdown.length : 0;
  const actual = classifyTerminal(
    [probe.parsed, resolve.parsed, transcode.parsed],
    t?.ok === true,
    mdLen,
  );
  return { probe, resolve, transcode, actual, mdLen };
}

async function runRecipeB(client, row) {
  const digest = await callTool(client, "occam_digest", {
    urls: JSON.stringify([row.url]),
    backend_policy: "http_then_browser",
    fit_markdown: false,
  });
  const d = digest.parsed;
  const item = d?.items?.[0];
  const itemOk = item?.ok === true;
  const itemCode = item?.failure?.code ?? d?.failureCode ?? null;
  let actual;
  if (d?.ok === true || itemOk) {
    actual = "WORKS";
  } else if (itemCode && HONEST_CODES.has(itemCode)) {
    actual = "FAIL_HONEST";
  } else if (SESSION_CODES.has(itemCode)) {
    actual = "NEEDS_SESSION";
  } else {
    actual = classifyTerminal([digest.parsed, item], false);
  }
  return { digest, actual, itemCode };
}

/** Recipe R — resolve + map subset (no full digest chain in Phase 2 runner). */
async function runRecipeR(client, row) {
  const resolve = await callTool(client, "occam_playbook_resolve", {
    url: row.url,
    fetch_site_genome: true,
  });
  const map = await callTool(client, "occam_map", {
    url: row.url,
    source: "homepage",
    max_links: 16,
    same_domain: true,
  });
  const r = resolve.parsed;
  const m = map.parsed;
  const resolveOk = r?.ok === true;
  const mapOk = m?.ok === true && (m?.linkCount ?? 0) > 0;
  const actual = resolveOk && mapOk ? "WORKS" : classifyTerminal([r, m], resolveOk && mapOk);
  return { resolve, map, actual, linkCount: m?.linkCount ?? 0, hasSchema: Boolean(r?.knowledgeSchema) };
}

async function runRow(client, row) {
  const entry = {
    id: row.id,
    url: row.url,
    recipe: row.recipe,
    expected: row.expected,
    startedAt: new Date().toISOString(),
  };
  for (let attempt = 0; attempt < 2; attempt += 1) {
    try {
      let result;
      switch (row.recipe) {
        case "A":
          result = await runRecipeA(client, row);
          break;
        case "C":
          result = await runRecipeC(client, row);
          break;
        case "B":
          result = await runRecipeB(client, row);
          break;
        case "R":
          result = await runRecipeR(client, row);
          break;
        default:
          throw new Error(`unsupported recipe: ${row.recipe}`);
      }
      Object.assign(entry, result);
      entry.failureCode = extractFailureCode(
        entry.probe?.parsed,
        entry.transcode?.parsed,
        entry.resolve?.parsed,
        entry.digest?.parsed,
        entry.map?.parsed,
      );
      entry.hintActions = collectHintActions(
        entry.probe?.parsed,
        entry.transcode?.parsed,
        entry.resolve?.parsed,
        entry.digest?.parsed,
        entry.map?.parsed,
      );
      entry.hintsOk = hintsOk(entry.actual, entry.failureCode, entry.hintActions);
      entry.match = entry.actual === row.expected; // exact, kept for reporting
      const metBar = (OUTCOME_RANK[entry.actual] ?? 0) >= (OUTCOME_RANK[row.expected] ?? 0);
      entry.pass =
        entry.actual !== "FAIL_BUG" &&
        entry.actual !== "ERROR" &&
        metBar &&
        (entry.actual !== "NEEDS_SESSION" || entry.hintsOk);
      entry.overPerformed = entry.pass && !entry.match;
    } catch (e) {
      entry.error = String(e?.message ?? e);
      entry.actual = "ERROR";
      entry.match = false;
      entry.pass = false;
    }

    // Retry once on a transient failure (network/DNS/rate-limit/5xx or a thrown error).
    const transient = entry.actual === "ERROR" || RETRYABLE_CODES.has(entry.failureCode);
    if (entry.pass || attempt === 1 || !transient) {
      break;
    }
    console.log(`[popular-hosts] ${row.id} retry (transient ${entry.failureCode ?? entry.actual})`);
    entry.retried = true;
    await sleep(1500);
  }
  entry.finishedAt = new Date().toISOString();
  const status = entry.pass ? (entry.overPerformed ? "PASS (over-performed)" : "PASS") : entry.match ? "HINT_GAP" : "MISMATCH";
  console.log(`[popular-hosts] ${row.id} (${row.recipe}): ${entry.actual} expected=${row.expected} → ${status}`);
  return entry;
}

async function main() {
  const rows = loadJsonl(corpusPath);
  const proc = spawn(process.execPath, [join(root, "scripts", "launch-mcp-host.mjs")], {
    cwd: root,
    env: {
      ...process.env,
      OCCAM_HOME: root,
      Logging__LogLevel__Default: "None",
      WT_OCCAM_BANNER: "0",
    },
    stdio: ["pipe", "pipe", "pipe"],
  });
  const client = new McpStdioClient(proc);
  const killTimer = setTimeout(() => proc.kill(), 30 * 60_000);

  const report = {
    ok: false,
    startedAt: new Date().toISOString(),
    occamHome: root,
    corpus: corpusPath,
    passBar: PASS_BAR,
    rows: [],
  };

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "agent-popular-hosts", version: "1.0" },
    });
    client.notify("notifications/initialized");

    for (const row of rows) {
      report.rows.push(await runRow(client, row));
    }
  } finally {
    clearTimeout(killTimer);
    client.close();
    proc.kill();
  }

  const passed = report.rows.filter((r) => r.pass).length;
  // Genuine failures only (regressions / bugs). Over-performance is not a mismatch.
  const mismatches = report.rows.filter((r) => !r.pass).map((r) => r.id);
  const hintGaps = report.rows.filter((r) => r.match && !r.pass).map((r) => r.id);
  const overPerformed = report.rows.filter((r) => r.overPerformed).map((r) => r.id);

  report.rollup = {
    total: report.rows.length,
    passed,
    mismatches,
    hintGaps,
    overPerformed,
    byActual: Object.fromEntries(
      ["WORKS", "FAIL_HONEST", "NEEDS_SESSION", "FAIL_BUG", "ERROR"].map((k) => [
        k,
        report.rows.filter((r) => r.actual === k).length,
      ]),
    ),
  };
  report.ok = passed >= PASS_BAR;
  report.finishedAt = new Date().toISOString();

  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, JSON.stringify(report, null, 2), "utf8");
  console.log(`[popular-hosts] wrote ${outPath}`);
  console.log(JSON.stringify({ ...report.rollup, ok: report.ok, passBar: PASS_BAR }));
  process.exit(report.ok ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
