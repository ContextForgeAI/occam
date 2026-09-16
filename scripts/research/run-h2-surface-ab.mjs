#!/usr/bin/env node
/**
 * H2 surface A/B — strong-agent protocol under real OCCAM_PROFILE surfaces.
 *
 * Spawns one MCP host per surface (minimal | basic | full). A deterministic
 * "strong" selector picks the preferred tool when the surface exposes it;
 * otherwise the trial is recorded as missingCapability (expected LOD cost).
 *
 * This is NOT a weak-model selection measurement and NOT multi-model Experiment 2.
 * Marker: H2_SURFACE_AB_OK when every cell finishes (failures are data, not abort).
 *
 *   node scripts/research/run-h2-surface-ab.mjs
 *   node scripts/research/run-h2-surface-ab.mjs --surfaces minimal,basic,full
 *   node scripts/research/run-h2-surface-ab.mjs --only h2-01,h2-09,h2-11
 */
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { SURFACE_TOOLS } from "./mock-agents.mjs";
import { buildStableLaunchSpec } from "../lib/operator/connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../lib/mcp-stdio-client.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "../..");
const outIdx = process.argv.indexOf("--out");
const outPath =
  outIdx >= 0
    ? process.argv[outIdx + 1]
    : join(root, "docs/research/results/h2-surface-ab.jsonl");

const surfacesArg = process.argv.indexOf("--surfaces");
const surfaces =
  surfacesArg >= 0
    ? process.argv[surfacesArg + 1].split(",").map((s) => s.trim()).filter(Boolean)
    : ["minimal", "basic", "full"];

const onlyArg = process.argv.indexOf("--only");
const only =
  onlyArg >= 0
    ? new Set(process.argv[onlyArg + 1].split(",").map((s) => s.trim()).filter(Boolean))
    : null;

const tasks = readFileSync(join(root, "docs/research/tasks/h2-web-reading.jsonl"), "utf8")
  .split(/\r?\n/)
  .filter(Boolean)
  .map((l) => JSON.parse(l))
  .filter((t) => !only || only.has(t.id));

const commit =
  spawnSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).stdout?.trim() ||
  "unknown";

function toolsFor(surface) {
  const listed = SURFACE_TOOLS[surface];
  if (listed) return listed;
  // full — enough to prefer correct tools; distractors exist but strong selector ignores them
  return [
    "occam",
    "occam_transcode",
    "occam_digest",
    "occam_search",
    "occam_playbook_heal",
    "occam_playbook_save",
    "occam_attest",
  ];
}

/** Strong selector: preferred tool if exposed; else missingCapability. */
function selectStrong(task, surface) {
  const needs = task.needs?.[0] || "read";
  const available = toolsFor(surface);
  const preferred =
    needs === "digest" ? "occam_digest" : needs === "search" ? "occam_search" : "occam";
  if (!available.includes(preferred)) {
    return {
      tool: preferred,
      missingCapability: true,
      irrelevant: false,
      reason: `surface_lacks_${needs}`,
    };
  }
  return { tool: preferred, missingCapability: false, irrelevant: false, reason: "preferred" };
}

function mustHit(text, must) {
  const hay = String(text || "").toLowerCase();
  return (must || []).every((s) => hay.includes(String(s).toLowerCase()));
}

async function callTool(client, name, args) {
  const result = await client.request("tools/call", { name, arguments: args });
  const text = mcpToolText(result);
  if (!text) throw new Error(`${name} returned no text`);
  return JSON.parse(text);
}

async function execute(client, task, selection) {
  if (selection.missingCapability) {
    return {
      ok: false,
      mustContainHit: false,
      extractOk: false,
      failure: "missing_capability",
      latencyMs: 0,
    };
  }

  const start = Date.now();
  const needs = task.needs?.[0] || "read";
  let payload;
  if (needs === "digest") {
    const urls = task.digest_urls || [task.url];
    payload = await callTool(client, "occam_digest", {
      urls,
      per_url_max_tokens: task.budget || 512,
      fit_markdown: true,
    });
  } else if (needs === "search") {
    payload = await callTool(client, "occam_search", {
      query: task.search_query || task.must_contain?.[0] || "example",
      max_results: 5,
    });
  } else {
    // minimal/basic expose `occam`; reader/full also expose it. Prefer cascade facade.
    /** @type {Record<string, unknown>} */
    const args = { url: task.url, budget: task.budget || 512 };
    if (task.focus) args.task = task.focus;
    payload = await callTool(client, "occam", args);
  }
  const ms = Date.now() - start;

  let body = "";
  if (needs === "search") {
    const results = Array.isArray(payload.results) ? payload.results : [];
    body = results
      .map((r) => `${r.title || ""} ${r.url || ""} ${r.snippet || ""}`)
      .join("\n");
    // Search success: tool ok + at least one hit; must_contain is soft on title/url/snippet union
  } else if (needs === "digest") {
    body = `${payload.combined || ""}\n${(payload.items || []).map((i) => i.excerpt || "").join("\n")}`;
  } else {
    body = payload.markdown || "";
  }

  const hit = mustHit(body, task.must_contain);
  const extractOk = !!payload.ok;
  // Search tasks: if tool ok and any result, accept must_contain miss as soft fail still counted
  const ok = extractOk && hit;
  return {
    ok,
    mustContainHit: hit,
    extractOk,
    failure: extractOk ? (hit ? null : "must_contain_miss") : payload.failure?.code || "tool_failed",
    latencyMs: ms,
    resultCount: needs === "search" ? (payload.results?.length ?? 0) : undefined,
  };
}

mkdirSync(dirname(outPath), { recursive: true });
const lines = [];
lines.push(
  JSON.stringify({
    type: "meta",
    protocol: "occam-h2-surface-ab-v1",
    hostCommit: commit,
    surfaces,
    taskCount: tasks.length,
    agentPolicy: "strong-preferred-tool",
    collectedAt: new Date().toISOString(),
    note: "Strong-agent protocol under real OCCAM_PROFILE MCP surfaces. Not weak-model selection. Not multi-model Experiment 2.",
  }),
);

const cells = [];

for (const surface of surfaces) {
  console.error(`[h2-ab] opening MCP surface=${surface}`);
  const spec = buildStableLaunchSpec(root);
  const client = await openOccamMcpSession({
    occamHome: root,
    command: spec.command,
    args: spec.args,
    env: {
      ...process.env,
      ...spec.env,
      OCCAM_HOME: root,
      OCCAM_PROFILE: surface,
      OCCAM_BANNER: "0",
    },
    cwd: spec.cwd,
    clientInfo: { name: "h2-surface-ab", version: "1.0" },
    requestTimeoutMs: 180_000,
  });

  let listed = [];
  try {
    const list = await client.request("tools/list", {});
    listed = (list.tools || []).map((t) => t.name);
    lines.push(
      JSON.stringify({
        type: "surface",
        surface,
        listedToolCount: listed.length,
        listedTools: listed,
      }),
    );

    let ok = 0;
    let missing = 0;
    let irrelevant = 0;
    const latencies = [];

    for (const task of tasks) {
      const selection = selectStrong(task, surface);
      // If strong selector wants a tool not in listed tools, treat as missing even if our table says available
      if (
        !selection.missingCapability &&
        listed.length &&
        !listed.includes(selection.tool)
      ) {
        selection.missingCapability = true;
        selection.reason = "listed_tools_lack_preferred";
      }

      let exec;
      try {
        exec = await execute(client, task, selection);
      } catch (err) {
        exec = {
          ok: false,
          mustContainHit: false,
          extractOk: false,
          failure: err instanceof Error ? err.message : String(err),
          latencyMs: 0,
        };
      }

      if (exec.ok) {
        ok++;
        if (exec.latencyMs) latencies.push(exec.latencyMs);
      }
      if (selection.missingCapability) missing++;
      if (selection.irrelevant) irrelevant++;

      const row = {
        type: "trial",
        surface,
        taskId: task.id,
        needs: task.needs?.[0] || "read",
        tool: selection.tool,
        missingCapability: !!selection.missingCapability,
        irrelevant: !!selection.irrelevant,
        reason: selection.reason,
        ok: exec.ok,
        mustContainHit: exec.mustContainHit,
        extractOk: exec.extractOk,
        failure: exec.failure,
        latencyMs: exec.latencyMs,
      };
      lines.push(JSON.stringify(row));
      console.error(
        `[h2-ab] ${surface} ${task.id} ${exec.ok ? "OK" : "FAIL"} tool=${selection.tool} ${exec.failure || selection.reason}`,
      );
    }

    const sorted = [...latencies].sort((a, b) => a - b);
    const cell = {
      type: "cell",
      surface,
      listedToolCount: listed.length,
      n: tasks.length,
      ok,
      successRate: ok / tasks.length,
      missingCapabilityCount: missing,
      irrelevantCount: irrelevant,
      latencyP50Ms: sorted.length ? sorted[Math.floor((sorted.length - 1) * 0.5)] : null,
    };
    cells.push(cell);
    lines.push(JSON.stringify(cell));
  } finally {
    await client.close({ graceMs: 1500 });
  }
}

const summary = {
  protocol: "occam-h2-surface-ab-summary-v1",
  hostCommit: commit,
  agentPolicy: "strong-preferred-tool",
  surfaces,
  cells: cells.map((c) => ({
    surface: c.surface,
    listedToolCount: c.listedToolCount,
    n: c.n,
    ok: c.ok,
    successRate: c.successRate,
    missingCapabilityCount: c.missingCapabilityCount,
  })),
  // LOD distinguishing signal for *strong*: minimal should be worse than full on digest/search tasks
  strongMinimalBelowFull:
    (cells.find((c) => c.surface === "minimal")?.successRate ?? 1) <
    (cells.find((c) => c.surface === "full")?.successRate ?? 0),
  allSurfacesRan: cells.length === surfaces.length,
  note: "Strong agent only. Weak-model selection rates unmeasured.",
};

writeFileSync(outPath, `${lines.join("\n")}\n`);
writeFileSync(
  outPath.replace(/\.jsonl$/, "-summary.json"),
  `${JSON.stringify(summary, null, 2)}\n`,
);

if (summary.allSurfacesRan) {
  console.log("H2_SURFACE_AB_OK");
  console.error(`[h2-ab] wrote ${outPath}`);
  console.error(`[h2-ab] strongMinimalBelowFull=${summary.strongMinimalBelowFull}`);
  process.exit(0);
}
console.error("H2_SURFACE_AB_FAIL", summary);
process.exit(1);
