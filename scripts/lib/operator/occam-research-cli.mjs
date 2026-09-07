/**
 * occam research — bounded resumable site research outside L0.
 * Reuses occam_map + occam_transcode. Not a new MCP tool.
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildStableLaunchSpec } from "./connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../mcp-stdio-client.mjs";
import {
  assembleResearch,
  canonicalizeUrl,
  parseResearchArgs,
  planResearch,
  researchFiles,
} from "./occam-research.mjs";

export const RESEARCH_USAGE = `usage: occam research --seed URL --out DIR [--focus Q] [--max-urls N] [--max-pages N] [--deadline-ms N] [--max-bytes N] [--resume] [--from-json FILE] [--json]
Bounded site research: discover (map) then extract (transcode) under URL/page/time/byte budgets.
Discovery and extraction are reported separately. Resume continues a previous --out folder.
Not a new MCP tool. Exit 0 when at least one page succeeded and stop is complete or a declared budget; 1 on cancel, discovery failure, or extract failure; 2 on usage.`;

function resolveOccamHome() {
  const fromEnv = process.env.OCCAM_HOME?.trim();
  if (fromEnv) return fromEnv;
  return join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
}

async function invokeMcpTool(tool, arguments_) {
  const occamHome = resolveOccamHome();
  const spec = buildStableLaunchSpec(occamHome);
  const client = await openOccamMcpSession({
    occamHome,
    command: spec.command,
    args: spec.args,
    env: { ...process.env, ...spec.env, OCCAM_PROFILE: process.env.OCCAM_PROFILE || "reader" },
    cwd: spec.cwd,
    clientInfo: { name: "occam-research-cli", version: "1.0" },
    requestTimeoutMs: 180_000,
  });
  try {
    const result = await client.request("tools/call", { name: tool, arguments: arguments_ });
    const text = mcpToolText(result);
    if (!text) throw new Error(`${tool} returned no text payload`);
    return JSON.parse(text);
  } finally {
    await client.close({ graceMs: 1500 });
  }
}

function asRecord(value) {
  return value && typeof value === "object" ? /** @type {Record<string, unknown>} */ (value) : {};
}

function linksFromMap(payload) {
  const links = Array.isArray(payload.links) ? payload.links : [];
  return links
    .map((row) => {
      const rec = asRecord(row);
      const url = typeof rec.url === "string" ? rec.url : "";
      return url ? { url, title: typeof rec.title === "string" ? rec.title : "" } : null;
    })
    .filter(Boolean);
}

function markdownBytes(payload) {
  const rec = asRecord(payload);
  const markdown = typeof rec.markdown === "string" ? rec.markdown : "";
  return Buffer.byteLength(markdown, "utf8");
}

/**
 * @param {ReturnType<typeof parseResearchArgs>} flags
 * @param {(tool: string, args: Record<string, unknown>) => Promise<unknown>} [callTool]
 */
export async function runResearch(flags, callTool = invokeMcpTool) {
  if (flags.help) {
    console.error(RESEARCH_USAGE);
    return 0;
  }
  if (flags.error) {
    console.error(flags.error);
    console.error(RESEARCH_USAGE);
    return 2;
  }

  const startedAt = Date.now();
  let cancelled = false;
  const onSigint = () => {
    cancelled = true;
  };
  process.on("SIGINT", onSigint);

  try {
    let seed = flags.seed;
    let focus = flags.focus;
    /** @type {Array<{ url: string, title?: string }>} */
    let discovered = [];
    /** @type {Array<{ url: string, ok?: boolean, bytes?: number, failureCode?: string, markdown?: string }>} */
    let extracted = [];
    let discoveryTool = "occam_map";
    let provider = null;
    let toolchain = "ff-occam";
    let createdAt = null;

    if (flags.fromJson) {
      const raw = JSON.parse(readFileSync(flags.fromJson, "utf8"));
      seed = raw.seed ?? seed;
      focus = raw.focus ?? focus;
      discovered = Array.isArray(raw.discovered) ? raw.discovered : [];
      extracted = Array.isArray(raw.extracted) ? raw.extracted : [];
      discoveryTool = raw.discoveryTool ?? discoveryTool;
      provider = raw.provider ?? null;
      if (typeof raw.toolchain === "string") toolchain = raw.toolchain;
      if (typeof raw.createdAt === "string") createdAt = raw.createdAt;
    }

    const outDir = flags.out ? flags.out : null;
    if (flags.resume && outDir) {
      const priorPath = join(outDir, "research-state.json");
      if (existsSync(priorPath)) {
        const prior = JSON.parse(readFileSync(priorPath, "utf8"));
        seed = seed || prior.seed;
        focus = focus ?? prior.focus;
        if (discovered.length === 0 && Array.isArray(prior.discovery?.urls)) {
          discovered = prior.discovery.urls.map((url) => ({ url }));
        }
        if (extracted.length === 0 && Array.isArray(prior.extraction?.urls)) {
          extracted = prior.extraction.urls.map((row) => ({
            url: row.url,
            ok: row.ok,
            bytes: row.bytes,
            failureCode: row.failureCode,
          }));
        }
      }
    }

    if (!flags.fromJson) {
      const map = await callTool("occam_map", {
        url: seed,
        max_links: flags.maxUrls,
        same_domain: flags.sameDomain,
        focus_query: focus ?? undefined,
      });
      const rec = asRecord(map);
      if (rec.ok === false) {
        const report = assembleResearch({
          seed,
          focus,
          sameDomain: flags.sameDomain,
          discovered: [],
          extracted: [],
          budgets: {
            maxUrls: flags.maxUrls,
            maxPages: flags.maxPages,
            deadlineMs: flags.deadlineMs,
            maxBytes: flags.maxBytes,
          },
          startedAt,
          now: Date.now(),
          cancelled,
          discoveryTool,
        });
        report.stop.reason = String(rec.failureCode ?? rec.failure?.code ?? "discovery_failed");
        report.ok = false;
        writeReport(outDir, report, "");
        printReport(flags, report);
        return 1;
      }
      discovered = linksFromMap(rec);
      if (canonicalizeUrl(seed) && !discovered.some((hit) => canonicalizeUrl(hit.url) === canonicalizeUrl(seed))) {
        discovered.unshift({ url: seed, title: "seed" });
      }
    }

    const excerpts = ["# Site research\n", `Seed: ${seed}\n`];
    for (const row of extracted) {
      if (row.markdown) excerpts.push(`## ${row.url}\n\n${row.markdown}\n`);
    }

    while (!cancelled) {
      const plan = planResearch({
        seed,
        focus,
        sameDomain: flags.sameDomain,
        discovered,
        extracted,
        budgets: {
          maxUrls: flags.maxUrls,
          maxPages: flags.maxPages,
          deadlineMs: flags.deadlineMs,
          maxBytes: flags.maxBytes,
        },
        startedAt,
        now: Date.now(),
        cancelled,
      });
      if (plan.stop || !plan.nextUrl) break;
      if (flags.fromJson) {
        // Offline replay: treat remaining planned URLs as already known; stop after applying budgets.
        break;
      }
      const payload = await callTool("occam_transcode", {
        url: plan.nextUrl,
        ...(focus ? { focus_query: focus, fit_markdown: true } : {}),
      });
      const rec = asRecord(payload);
      const markdown = typeof rec.markdown === "string" ? rec.markdown : "";
      const ok = rec.ok === true;
      extracted.push({
        url: plan.nextUrl,
        ok,
        bytes: markdownBytes(rec),
        failureCode: ok ? null : String(rec.failure?.code ?? rec.failureCode ?? "extraction_failed"),
        markdown,
      });
      excerpts.push(`## ${plan.nextUrl}\n\n${ok ? markdown : `ok:false ${rec.failure?.code ?? rec.failureCode ?? ""}\n`}`);
    }

    const report = assembleResearch({
      seed,
      focus,
      sameDomain: flags.sameDomain,
      discovered,
      extracted,
      budgets: {
        maxUrls: flags.maxUrls,
        maxPages: flags.maxPages,
        deadlineMs: flags.deadlineMs,
        maxBytes: flags.maxBytes,
      },
      startedAt,
      now: Date.now(),
      cancelled,
      discoveryTool,
      provider,
      toolchain,
      createdAt,
    });
    writeReport(outDir, report, excerpts.join("\n"));
    printReport(flags, report);
    return report.ok ? 0 : 1;
  } finally {
    process.off("SIGINT", onSigint);
  }
}

function writeReport(outDir, report, excerpts) {
  if (!outDir) return;
  mkdirSync(outDir, { recursive: true });
  const files = researchFiles(report, excerpts);
  for (const [name, body] of Object.entries(files)) {
    writeFileSync(join(outDir, name), body);
  }
}

function printReport(flags, report) {
  if (flags.json) {
    console.log(JSON.stringify(report, null, 2));
    return;
  }
  console.error(
    `research ${report.stop.reason}  discovery=${report.discovery.ranked}  extracted=${report.extraction.pages}  failed=${report.extraction.failed}`,
  );
}

/**
 * @param {string[]} argv
 */
export async function runResearchCommand(argv, callTool) {
  return runResearch(parseResearchArgs(argv), callTool);
}
