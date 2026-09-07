/**
 * occam research — bounded resumable site research outside L0.
 * Reuses occam_map + occam_transcode. Not a new MCP tool.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildStableLaunchSpec } from "./connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../mcp-stdio-client.mjs";
import {
  assembleResearch,
  buildResearchExcerpts,
  canonicalizeUrl,
  loadResearchCheckpoint,
  pageRecords,
  parseResearchArgs,
  planResearch,
  researchFiles,
  writeAtomicFile,
} from "./occam-research.mjs";

export const RESEARCH_USAGE = `usage: occam research --seed URL --out DIR [--focus Q] [--max-urls N] [--max-pages N] [--deadline-ms N] [--max-bytes N] [--resume] [--from-json FILE] [--json]
Bounded site research: discover (map) then extract (transcode) under URL/page/time/byte budgets.
--max-bytes counts UTF-8 bytes of kept extracted markdown (output), not HTTP download size.
Remaining deadline is applied to each MCP call. Resume restores pages.json (or excerpts.txt).
A discovery failure during --resume does not erase a prior report.
Not a new MCP tool. Exit 0 when at least one page succeeded and stop is complete or a declared budget; 1 on cancel, discovery failure, or extract failure; 2 on usage.`;

const INVOKE_CAP_MS = 180_000;

function resolveOccamHome() {
  const fromEnv = process.env.OCCAM_HOME?.trim();
  if (fromEnv) return fromEnv;
  return join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
}

async function invokeMcpTool(tool, arguments_, options = {}) {
  const occamHome = resolveOccamHome();
  const spec = buildStableLaunchSpec(occamHome);
  const requested = Number(options.requestTimeoutMs);
  const requestTimeoutMs = Number.isFinite(requested) && requested > 0
    ? Math.min(INVOKE_CAP_MS, Math.max(1, requested))
    : INVOKE_CAP_MS;
  const client = await openOccamMcpSession({
    occamHome,
    command: spec.command,
    args: spec.args,
    env: { ...process.env, ...spec.env, OCCAM_PROFILE: process.env.OCCAM_PROFILE || "reader" },
    cwd: spec.cwd,
    clientInfo: { name: "occam-research-cli", version: "1.0" },
    requestTimeoutMs,
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

function markdownBytes(markdown) {
  return Buffer.byteLength(String(markdown ?? ""), "utf8");
}

function errorCode(err) {
  return err && typeof err === "object" && "code" in err ? String(err.code) : "";
}

function keptBytes(extracted) {
  return extracted.reduce((sum, row) => {
    if (row.ok === false) return sum;
    return sum + (Number(row.bytes) || 0);
  }, 0);
}

function writeReport(outDir, report, excerpts, extracted) {
  if (!outDir) return;
  const files = researchFiles(report, excerpts, pageRecords(extracted));
  for (const [name, body] of Object.entries(files)) {
    writeAtomicFile(join(outDir, name), body);
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
 * @param {ReturnType<typeof parseResearchArgs>} flags
 * @param {(tool: string, args: Record<string, unknown>, options?: { requestTimeoutMs?: number }) => Promise<unknown>} [callTool]
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
    /** @type {Array<{ url: string, ok?: boolean, bytes?: number, failureCode?: string, markdown?: string, fetchedAt?: string }>} */
    let extracted = [];
    /** @type {Array<{ url: string, reason: string, bytes: number }>} */
    let omitted = [];
    let discoveryTool = "occam_map";
    let provider = null;
    let toolchain = "ff-occam";
    let createdAt = null;
    /** @type {string | null} */
    let halt = null;

    if (flags.fromJson) {
      const raw = JSON.parse(readFileSync(flags.fromJson, "utf8"));
      seed = raw.seed ?? seed;
      focus = raw.focus ?? focus;
      discovered = Array.isArray(raw.discovered) ? raw.discovered : [];
      extracted = Array.isArray(raw.extracted) ? raw.extracted : [];
      omitted = Array.isArray(raw.omitted) ? raw.omitted : [];
      discoveryTool = raw.discoveryTool ?? discoveryTool;
      provider = raw.provider ?? null;
      if (typeof raw.toolchain === "string") toolchain = raw.toolchain;
      if (typeof raw.createdAt === "string") createdAt = raw.createdAt;
    }

    const outDir = flags.out ? flags.out : null;
    if (flags.resume && outDir) {
      const loaded = loadResearchCheckpoint(outDir);
      if (loaded) {
        seed = seed || loaded.seed;
        focus = focus ?? loaded.focus;
        if (typeof loaded.toolchain === "string") toolchain = loaded.toolchain;
        if (typeof loaded.createdAt === "string") createdAt = loaded.createdAt;
        if (discovered.length === 0) discovered = loaded.discovered;
        if (extracted.length === 0) extracted = loaded.extracted;
        if (omitted.length === 0) omitted = loaded.omitted;
      }
    }

    const budgets = {
      maxUrls: flags.maxUrls,
      maxPages: flags.maxPages,
      deadlineMs: flags.deadlineMs,
      maxBytes: flags.maxBytes,
    };

    const remainingMs = () => flags.deadlineMs - (Date.now() - startedAt);

    const timedCall = async (tool, args) => {
      if (cancelled) {
        const err = new Error("cancelled");
        err.code = "cancelled";
        throw err;
      }
      const remaining = remainingMs();
      if (remaining <= 0) {
        const err = new Error("budget_time");
        err.code = "budget_time";
        throw err;
      }
      return callTool(tool, args, { requestTimeoutMs: remaining });
    };

    const snapshot = (now = Date.now()) => assembleResearch({
      seed,
      focus,
      sameDomain: flags.sameDomain,
      discovered,
      extracted,
      omitted,
      budgets,
      startedAt,
      now,
      cancelled,
      halt,
      discoveryTool,
      provider,
      toolchain,
      createdAt,
    });

    const persist = (report) => {
      writeReport(outDir, report, buildResearchExcerpts(seed, extracted, omitted), extracted);
    };

    if (!flags.fromJson) {
      let map;
      try {
        map = await timedCall("occam_map", {
          url: seed,
          max_links: flags.maxUrls,
          same_domain: flags.sameDomain,
          focus_query: focus ?? undefined,
        });
      } catch (err) {
        halt = errorCode(err) === "cancelled" || cancelled ? "cancelled" : "budget_time";
        const report = snapshot();
        report.ok = false;
        persist(report);
        printReport(flags, report);
        return 1;
      }
      const rec = asRecord(map);
      if (rec.ok === false) {
        const report = snapshot();
        report.stop.reason = String(rec.failureCode ?? rec.failure?.code ?? "discovery_failed");
        report.ok = false;
        persist(report);
        printReport(flags, report);
        return 1;
      }
      discovered = linksFromMap(rec);
      if (canonicalizeUrl(seed) && !discovered.some((hit) => canonicalizeUrl(hit.url) === canonicalizeUrl(seed))) {
        discovered.unshift({ url: seed, title: "seed" });
      }
      persist(snapshot());
    }

    while (!cancelled && !halt) {
      const plan = planResearch({
        seed,
        focus,
        sameDomain: flags.sameDomain,
        discovered,
        extracted,
        budgets,
        startedAt,
        now: Date.now(),
        cancelled,
        halt,
      });
      if (plan.stop || !plan.nextUrl) break;
      if (flags.fromJson) break;
      if (remainingMs() <= 0) {
        halt = "budget_time";
        break;
      }
      let payload;
      try {
        payload = await timedCall("occam_transcode", {
          url: plan.nextUrl,
          ...(focus ? { focus_query: focus, fit_markdown: true } : {}),
        });
      } catch (err) {
        halt = errorCode(err) === "cancelled" || cancelled ? "cancelled" : "budget_time";
        break;
      }
      if (cancelled) {
        halt = "cancelled";
        break;
      }
      const rec = asRecord(payload);
      const markdown = typeof rec.markdown === "string" ? rec.markdown : "";
      const ok = rec.ok === true;
      const bytes = ok ? markdownBytes(markdown) : 0;
      if (ok && keptBytes(extracted) + bytes > flags.maxBytes) {
        omitted.push({
          url: plan.nextUrl,
          reason: "budget_bytes",
          bytes,
        });
        halt = "budget_bytes";
        persist(snapshot());
        break;
      }
      extracted.push({
        url: plan.nextUrl,
        ok,
        bytes,
        failureCode: ok ? null : String(rec.failure?.code ?? rec.failureCode ?? "extraction_failed"),
        markdown,
        fetchedAt: new Date().toISOString(),
      });
      persist(snapshot());
    }

    const report = snapshot();
    persist(report);
    printReport(flags, report);
    return report.ok ? 0 : 1;
  } finally {
    process.off("SIGINT", onSigint);
  }
}

/**
 * @param {string[]} argv
 * @param {(tool: string, args: Record<string, unknown>, options?: { requestTimeoutMs?: number }) => Promise<unknown>} [callTool]
 */
export async function runResearchCommand(argv, callTool) {
  return runResearch(parseResearchArgs(argv), callTool);
}
