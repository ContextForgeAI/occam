/**
 * occam pack — Context Pack orchestrator outside L0.
 * Reuses occam_search / occam_transcode / occam_digest. Not a new MCP tool.
 */
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { buildStableLaunchSpec } from "./connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../mcp-stdio-client.mjs";
import {
  assemblePack,
  packFiles,
  parsePackArgs,
  searchHitUrls,
} from "./occam-pack.mjs";

export const PACK_USAGE = `usage: occam pack --task TEXT (--url URL... | --search Q | --from-json FILE) [--budget N] [--focus Q] [--out DIR] [--json]
Build a context pack (manifest, sources, excerpts, omissions) from existing Occam tools.
Not a new MCP tool. Exit 0 when the pack is written and every source succeeded,
1 when the host failed or a source failed, 2 on usage.`;

function resolveOccamHome() {
  const fromEnv = process.env.OCCAM_HOME?.trim();
  if (fromEnv) return fromEnv;
  return join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
}

/**
 * @param {string} tool
 * @param {Record<string, unknown>} arguments_
 */
async function invokeMcpTool(tool, arguments_) {
  const occamHome = resolveOccamHome();
  const spec = buildStableLaunchSpec(occamHome);
  const client = await openOccamMcpSession({
    occamHome,
    command: spec.command,
    args: spec.args,
    env: { ...process.env, ...spec.env, OCCAM_PROFILE: process.env.OCCAM_PROFILE || "reader" },
    cwd: spec.cwd,
    clientInfo: { name: "occam-pack-cli", version: "1.0" },
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

/**
 * @param {ReturnType<typeof parsePackArgs>} flags
 * @param {(tool: string, args: Record<string, unknown>) => Promise<unknown>} callTool
 */
export async function collectResponses(flags, callTool) {
  /** @type {Array<{ tool: string, payload: unknown }>} */
  const responses = [];
  const backend = flags.backend ? flags.backend.replaceAll("-", "_") : undefined;
  let urls = [...flags.urls];

  if (flags.search) {
    const searchArgs = { query: flags.search, max_results: flags.maxSources };
    const search = await callTool("occam_search", searchArgs);
    responses.push({ tool: "occam_search", payload: search });
    urls = searchHitUrls(search).slice(0, flags.maxSources);
  }

  if (urls.length === 1) {
    /** @type {Record<string, unknown>} */
    const arguments_ = { url: urls[0] };
    if (backend) arguments_.backend_policy = backend;
    if (flags.focus) {
      arguments_.focus_query = flags.focus;
      arguments_.fit_markdown = true;
    }
    if (flags.budget != null) arguments_.max_tokens = flags.budget;
    responses.push({ tool: "occam_transcode", payload: await callTool("occam_transcode", arguments_) });
  } else if (urls.length > 1) {
    /** @type {Record<string, unknown>} */
    const arguments_ = { urls };
    if (backend) arguments_.backend_policy = backend;
    if (flags.focus) {
      arguments_.focus_query = flags.focus;
      arguments_.fit_markdown = true;
    }
    if (flags.budget != null) arguments_.per_url_max_tokens = flags.budget;
    responses.push({ tool: "occam_digest", payload: await callTool("occam_digest", arguments_) });
  }

  return responses;
}

/**
 * @param {string[]} argv
 * @param {{
 *   callTool?: (tool: string, args: Record<string, unknown>) => Promise<unknown>,
 *   now?: string,
 * }} [hooks]
 */
export async function runPackCommand(argv, hooks = {}) {
  const flags = parsePackArgs(argv);
  if (flags.help) {
    console.log(PACK_USAGE);
    return 0;
  }
  if (flags.error) {
    console.error(`error: ${flags.error}`);
    console.error(PACK_USAGE);
    return 2;
  }

  let responses;
  let toolchain = "ff-occam";
  let createdAt = hooks.now;
  try {
    if (flags.fromJson) {
      const raw = JSON.parse(readFileSync(resolve(flags.fromJson), "utf8"));
      responses = Array.isArray(raw.responses) ? raw.responses : [];
      if (typeof raw.toolchain === "string") toolchain = raw.toolchain;
      if (typeof raw.createdAt === "string" && !createdAt) createdAt = raw.createdAt;
      if (!flags.task && typeof raw.task === "string") flags.task = raw.task;
    } else {
      responses = await collectResponses(flags, hooks.callTool ?? invokeMcpTool);
    }
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    if (flags.json) console.log(JSON.stringify({ ok: false, error: message }, null, 2));
    else console.error(`error: ${message}`);
    return 1;
  }

  const pack = assemblePack({
    task: flags.task,
    toolchain,
    createdAt,
    settings: {
      budget: flags.budget,
      focus: flags.focus,
      backend: flags.backend || undefined,
      search: flags.search || undefined,
      urls: flags.urls.length ? flags.urls : urlsFromResponses(responses),
      maxSources: flags.search ? flags.maxSources : undefined,
    },
    responses,
  });

  if (flags.out) {
    const dir = resolve(flags.out);
    mkdirSync(dir, { recursive: true });
    const files = packFiles(pack);
    for (const [name, body] of Object.entries(files)) {
      writeFileSync(join(dir, name), body, "utf8");
    }
  }

  if (flags.json || !flags.out) {
    console.log(JSON.stringify(pack.manifest, null, 2));
  } else {
    console.error(
      `wrote ${flags.out} (${pack.manifest.sourceCount} sources, ${pack.manifest.failed} failed, budget ${pack.manifest.budget.total})`,
    );
  }

  return pack.manifest.ok ? 0 : 1;
}

function urlsFromResponses(responses) {
  const urls = [];
  for (const step of responses ?? []) {
    const payload = step?.payload && typeof step.payload === "object"
      ? /** @type {Record<string, unknown>} */ (step.payload)
      : {};
    const url = payload.url;
    if (typeof url === "string") urls.push(url);
    else if (url && typeof url === "object") {
      const row = /** @type {Record<string, unknown>} */ (url);
      if (typeof row.url === "string") urls.push(row.url);
    }
    if (Array.isArray(payload.items)) {
      for (const item of payload.items) {
        if (item && typeof item === "object" && typeof item.url === "string") {
          urls.push(item.url);
        }
      }
    }
  }
  return [...new Set(urls)];
}
