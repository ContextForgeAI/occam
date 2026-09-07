/**
 * occam brief — Docs Change Brief outside L0.
 * Reuses occam_transcode if_none_match. Not occam_watch, not a new MCP tool.
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildStableLaunchSpec } from "./connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../mcp-stdio-client.mjs";
import { assembleBrief, briefFiles, parseBriefArgs } from "./occam-brief.mjs";

export const BRIEF_USAGE = `usage: occam brief --url URL... --out DIR [--against HASH] [--focus Q] [--resume] [--from-json FILE] [--json]
Docs change brief: re-read URLs with if_none_match, report unchanged vs changed vs failed.
Not a new MCP tool and not occam_watch. Exit 0 when every URL was read, 1 on extract failure, 2 on usage.`;

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
    clientInfo: { name: "occam-brief-cli", version: "1.0" },
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

/**
 * @param {ReturnType<typeof parseBriefArgs>} flags
 * @param {(tool: string, args: Record<string, unknown>) => Promise<unknown>} [callTool]
 */
export async function runBrief(flags, callTool = invokeMcpTool) {
  if (flags.help) {
    console.error(BRIEF_USAGE);
    return 0;
  }
  if (flags.error) {
    console.error(flags.error);
    console.error(BRIEF_USAGE);
    return 2;
  }

  let toolchain = "ff-occam";
  let createdAt = null;
  let focus = flags.focus;
  /** @type {Array<{ url: string, ok?: boolean, unchanged?: boolean, contentHash?: string, markdown?: string, failureCode?: string }>} */
  let pages = [];
  /** @type {Record<string, string>} */
  let priorHashes = {};

  if (flags.fromJson) {
    const raw = JSON.parse(readFileSync(flags.fromJson, "utf8"));
    if (typeof raw.toolchain === "string") toolchain = raw.toolchain;
    if (typeof raw.createdAt === "string") createdAt = raw.createdAt;
    if (typeof raw.focus === "string") focus = raw.focus;
    pages = Array.isArray(raw.pages) ? raw.pages : [];
  }

  const outDir = flags.out;
  if (flags.resume && outDir) {
    const priorPath = join(outDir, "brief-state.json");
    if (existsSync(priorPath)) {
      const prior = JSON.parse(readFileSync(priorPath, "utf8"));
      for (const row of prior.pages ?? []) {
        if (row.url && row.contentHash) priorHashes[row.url] = row.contentHash;
      }
    }
  }
  if (flags.against && flags.urls[0]) priorHashes[flags.urls[0]] = flags.against;

  if (!flags.fromJson) {
    for (const url of flags.urls) {
      /** @type {Record<string, unknown>} */
      const arguments_ = { url, backend_policy: "http" };
      if (focus) {
        arguments_.focus_query = focus;
        arguments_.fit_markdown = true;
      }
      const prior = priorHashes[url];
      if (prior) arguments_.if_none_match = prior;
      const payload = asRecord(await callTool("occam_transcode", arguments_));
      const rec = asRecord(payload.url);
      const signed = asRecord(asRecord(payload.receipt).signed);
      pages.push({
        url: typeof rec.url === "string" ? rec.url : url,
        ok: payload.ok === true,
        unchanged: payload.unchanged === true,
        contentHash:
          typeof payload.contentHash === "string"
            ? payload.contentHash
            : typeof signed.contentHash === "string"
              ? String(signed.contentHash).replace(/^sha256:/, "")
              : null,
        markdown: typeof payload.markdown === "string" ? payload.markdown : "",
        failureCode:
          payload.ok === true
            ? null
            : String(asRecord(payload.failure).code ?? payload.failureCode ?? "extraction_failed"),
      });
      const stamped = asRecord(asRecord(payload.receipt).signed).toolchain;
      if (typeof stamped === "string") toolchain = stamped;
    }
  }

  const report = assembleBrief({ pages, toolchain, createdAt, focus });
  if (outDir) {
    mkdirSync(outDir, { recursive: true });
    const files = briefFiles(report);
    for (const [name, body] of Object.entries(files)) writeFileSync(join(outDir, name), body);
  }
  if (flags.json) console.log(JSON.stringify(report, null, 2));
  else {
    console.error(
      `brief ${report.stop.reason}  changed=${report.summary.changed}  unchanged=${report.summary.unchanged}  failed=${report.summary.failed}`,
    );
  }
  return report.ok ? 0 : 1;
}

export async function runBriefCommand(argv, callTool) {
  return runBrief(parseBriefArgs(argv), callTool);
}
