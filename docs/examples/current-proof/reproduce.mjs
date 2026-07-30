#!/usr/bin/env node
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  mcpToolText,
  openOccamMcpSession,
} from "../../../scripts/lib/mcp-stdio-client.mjs";

const fixtureDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(fixtureDir, "../../..");
const successUrl = "https://example.com/";
const failureUrl = "http://127.0.0.1:9/occam-marketing-proof";

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function writeJson(name, value) {
  writeFileSync(join(fixtureDir, name), `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function sourceSha() {
  return execFileSync("git", ["rev-parse", "HEAD"], {
    cwd: repoRoot,
    encoding: "utf8",
  }).trim();
}

function parseToolResult(result, label) {
  const text = mcpToolText(result);
  if (!text) {
    throw new Error(`${label}: tools/call returned no text payload`);
  }
  try {
    return JSON.parse(text);
  } catch (error) {
    throw new Error(`${label}: tools/call returned invalid JSON: ${error.message}`);
  }
}

async function callTranscode(client, url) {
  return parseToolResult(
    await client.request(
      "tools/call",
      {
        name: "occam_transcode",
        arguments: { url },
      },
      120_000,
    ),
    url,
  );
}

async function main() {
  mkdirSync(fixtureDir, { recursive: true });
  const capturedAt = new Date().toISOString();
  const sha = sourceSha();

  const inputResponse = await fetch(successUrl, {
    headers: {
      Accept: "text/html",
      "Cache-Control": "no-cache",
      "User-Agent": "Occam-Current-Proof/1.0",
    },
    redirect: "follow",
  });
  if (!inputResponse.ok) {
    throw new Error(`input fetch failed: HTTP ${inputResponse.status}`);
  }
  const rawBody = Buffer.from(await inputResponse.arrayBuffer());

  const client = await openOccamMcpSession({
    occamHome: repoRoot,
    command: process.execPath,
    args: [join(repoRoot, "scripts", "launch-mcp-host.mjs")],
    cwd: repoRoot,
    env: {
      OCCAM_HOME: repoRoot,
      OCCAM_ALLOW_PRIVATE_URLS: "",
      OCCAM_RECEIPTS: "1",
    },
    clientInfo: { name: "occam-current-proof", version: "1.0" },
    requestTimeoutMs: 120_000,
  });

  let success;
  let failure;
  try {
    success = await callTranscode(client, successUrl);
    failure = await callTranscode(client, failureUrl);
  } finally {
    await client.close();
  }

  if (success?.ok !== true || typeof success.markdown !== "string" || !success.markdown.trim()) {
    throw new Error("success fixture did not return ok:true with non-empty markdown");
  }
  if (failure?.ok !== false || failure?.failure?.code !== "private_url_blocked") {
    throw new Error(
      `failure fixture expected private_url_blocked, got ${JSON.stringify(failure?.failure ?? failure)}`,
    );
  }

  const markdownBytes = Buffer.byteLength(success.markdown, "utf8");
  const reductionPercent = Number(
    ((1 - markdownBytes / rawBody.length) * 100).toFixed(1),
  );

  writeJson("input-metadata.json", {
    schemaVersion: 1,
    capturedAt,
    sourceSha: sha,
    input: {
      url: successUrl,
      finalUrl: inputResponse.url,
      status: inputResponse.status,
      contentType: inputResponse.headers.get("content-type"),
      contentEncoding: inputResponse.headers.get("content-encoding"),
      bodyBytesAfterHttpDecoding: rawBody.length,
      bodySha256: sha256(rawBody),
    },
    invocation: {
      transport: "MCP stdio",
      tool: "occam_transcode",
      arguments: { url: successUrl },
      outputProfile: "default options; only the required url argument",
    },
    failureInvocation: {
      transport: "MCP stdio",
      tool: "occam_transcode",
      arguments: { url: failureUrl },
      environment: { OCCAM_ALLOW_PRIVATE_URLS: "unset/empty" },
    },
  });

  writeJson("measurement.json", {
    schemaVersion: 1,
    capturedAt,
    sourceSha: sha,
    method:
      "Compare the UTF-8 byte length of the fetched HTML response body after HTTP content decoding with the UTF-8 byte length of result.markdown.",
    inputBodyBytesAfterHttpDecoding: rawBody.length,
    outputMarkdownUtf8Bytes: markdownBytes,
    byteReductionForThisExamplePercent: reductionPercent,
    tokenizer: null,
    qualification:
      "This is one reproducible example, not a universal size or token-savings guarantee.",
  });

  writeFileSync(join(fixtureDir, "success-output.md"), success.markdown, "utf8");
  writeJson("success-result.json", success);
  writeJson("failure-result.json", failure);

  console.log(
    `CURRENT_PROOF_OK sha=${sha} input_bytes=${rawBody.length} markdown_bytes=${markdownBytes} reduction=${reductionPercent}% failure=${failure.failure.code}`,
  );
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
});
