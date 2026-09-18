#!/usr/bin/env node
/**
 * Live MCP canary E2E: tools/list → issue → HTTP fetch → verify (+ HALLUCINATED).
 *
 *   node scripts/lib/canary-mcp-e2e.selftest.mjs
 *
 * Uses dotnet run (current tree), not a stale AOT publish.
 * Marker: CANARY_MCP_E2E_OK
 */
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { openOccamMcpSession, mcpToolText } from "./mcp-stdio-client.mjs";
import { parseToolJson } from "./mcp-tool-json.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(here, "..", "..");

function extractSentinel(htmlOrMd) {
  const meta = htmlOrMd.match(/occam-sentinel["']\s+content=["']([^"']+)["']/i);
  if (meta) return meta[1];
  const md = htmlOrMd.match(/Sentinel:\s*`([^`]+)`/);
  if (md) return md[1];
  throw new Error("sentinel not found in fetched page");
}

async function callTool(client, name, args) {
  const result = await client.request("tools/call", { name, arguments: args || {} }, 60_000);
  const { parsed, isError, raw } = parseToolJson(result);
  if (!parsed) {
    throw new Error(`tools/call ${name}: no JSON body (${typeof raw === "string" ? raw.slice(0, 200) : "empty"})`);
  }
  return { parsed, isError, result };
}

async function main() {
  const client = await openOccamMcpSession({
    occamHome: root,
    command: "dotnet",
    args: ["run", "--project", join(root, "src", "FFOccamMcp.Core"), "-c", "Debug", "--no-build"],
    env: {
      OCCAM_HOME: root,
      OCCAM_BANNER: "0",
      OCCAM_PROFILE: "reader",
      OCCAM_FORCE_DOTNET_RUN: "1",
    },
    clientInfo: { name: "canary-mcp-e2e", version: "1.0" },
    requestTimeoutMs: 90_000,
  });

  try {
    const listed = await client.request("tools/list", {});
    const names = (listed.tools || []).map((t) => t.name);
    assert.ok(names.includes("occam_canary_issue"), `tools/list missing occam_canary_issue; got ${names.join(",")}`);
    assert.ok(names.includes("occam_canary_verify"), `tools/list missing occam_canary_verify; got ${names.join(",")}`);
    console.error(`[canary-e2e] tools/list ok (${names.length} tools, canary present)`);

    const { parsed: issued } = await callTool(client, "occam_canary_issue", {});
    assert.equal(issued.ok, true);
    assert.ok(issued.url && issued.sessionId);
    assert.ok(!JSON.stringify(issued).includes("sentinel"), "issue must not leak sentinel field");
    // Sentinel value must not appear in the issue envelope at all.
    console.error(`[canary-e2e] issue ok url=${issued.url}`);

    const res = await fetch(issued.url);
    assert.equal(res.ok, true, `GET ${issued.url} → ${res.status}`);
    const body = await res.text();
    const sentinel = extractSentinel(body);
    assert.ok(sentinel.length >= 16);
    console.error(`[canary-e2e] fetched sentinel (${sentinel.length} chars)`);

    const { parsed: verified } = await callTool(client, "occam_canary_verify", {
      session_id: issued.sessionId,
      sentinel,
    });
    assert.equal(verified.verdict, "READ_VERIFIED", JSON.stringify(verified));
    assert.equal(verified.ok, true);
    console.error(`[canary-e2e] verify READ_VERIFIED ok`);

    const { parsed: hallucinated } = await callTool(client, "occam_canary_verify", {
      session_id: issued.sessionId,
      sentinel: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
    });
    assert.equal(hallucinated.verdict, "HALLUCINATED", JSON.stringify(hallucinated));
    assert.equal(hallucinated.ok, false);
    console.error(`[canary-e2e] verify HALLUCINATED ok`);

    // REPLAY_SUSPECT / READ_STALE need clock/secret control — covered by OccamCanaryMcpToolTests.
    console.log("CANARY_MCP_E2E_OK");
  } finally {
    await client.close({ graceMs: 3_000 });
  }
}

main().catch((err) => {
  console.error(`[canary-e2e] FAIL ${err?.stack || err}`);
  process.exit(1);
});
