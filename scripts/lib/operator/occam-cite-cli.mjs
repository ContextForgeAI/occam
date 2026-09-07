/**
 * occam cite — Citation Inspector outside L0.
 * Reuses occam_claim_check. Does not judge support vs refute.
 */
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildStableLaunchSpec } from "./connect/launch-spec.mjs";
import { mcpToolText, openOccamMcpSession } from "../mcp-stdio-client.mjs";
import { assembleCite, citeFiles, parseCiteArgs } from "./occam-cite.mjs";

export const CITE_USAGE = `usage: occam cite --claim TEXT --url URL --out DIR [--max-matches N] [--from-json FILE] [--json]
Citation inspector: retrieve page blocks for a claim. You judge support vs refute.
Not a new MCP tool. Exit 0 when the page was read, 1 on extract failure, 2 on usage.
found:false is a successful retrieval-negative, not a failure.`;

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
    env: { ...process.env, ...spec.env, OCCAM_PROFILE: process.env.OCCAM_PROFILE || "researcher" },
    cwd: spec.cwd,
    clientInfo: { name: "occam-cite-cli", version: "1.0" },
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
 * @param {ReturnType<typeof parseCiteArgs>} flags
 * @param {(tool: string, args: Record<string, unknown>) => Promise<unknown>} [callTool]
 */
export async function runCite(flags, callTool = invokeMcpTool) {
  if (flags.help) {
    console.error(CITE_USAGE);
    return 0;
  }
  if (flags.error) {
    console.error(flags.error);
    console.error(CITE_USAGE);
    return 2;
  }

  let claim = flags.claim;
  let url = flags.url;
  let toolchain = "ff-occam";
  let createdAt = null;
  let payload;

  if (flags.fromJson) {
    const raw = JSON.parse(readFileSync(flags.fromJson, "utf8"));
    claim = raw.claim ?? claim;
    url = raw.url ?? url;
    payload = raw.payload ?? raw;
    if (typeof raw.toolchain === "string") toolchain = raw.toolchain;
    if (typeof raw.createdAt === "string") createdAt = raw.createdAt;
  } else {
    payload = await callTool("occam_claim_check", {
      claim,
      url,
      max_matches: flags.maxMatches,
      backend_policy: "http",
    });
    const stamped = payload?.receipt?.toolchain ?? payload?.receipt?.signed?.toolchain;
    if (typeof stamped === "string") toolchain = stamped;
  }

  const report = assembleCite({ claim, url, payload, toolchain, createdAt });
  if (flags.out) {
    mkdirSync(flags.out, { recursive: true });
    const files = citeFiles(report);
    for (const [name, body] of Object.entries(files)) writeFileSync(join(flags.out, name), body);
  }
  if (flags.json) console.log(JSON.stringify(report, null, 2));
  else {
    console.error(
      report.ok
        ? `cite found=${report.found} matches=${report.matchCount} verdict=${report.verdict}`
        : `cite ok:false ${report.failureCode}`,
    );
  }
  return report.ok ? 0 : 1;
}

export async function runCiteCommand(argv, callTool) {
  return runCite(parseCiteArgs(argv), callTool);
}
