#!/usr/bin/env node
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { createServer } from "node:http";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  mcpToolText,
  openOccamMcpSession,
} from "../../../scripts/lib/mcp-stdio-client.mjs";

const fixtureDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(fixtureDir, "../../..");
const inputName = "representative-input.html";
const inputBytes = readFileSync(join(fixtureDir, inputName));

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function writeJson(name, value) {
  writeFileSync(join(fixtureDir, name), `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function gitValue(args) {
  return execFileSync("git", args, {
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

async function listen(server) {
  await new Promise((resolveListen, rejectListen) => {
    server.once("error", rejectListen);
    server.listen(0, "127.0.0.1", resolveListen);
  });
  const address = server.address();
  if (!address || typeof address === "string") {
    throw new Error("fixture server did not expose a TCP address");
  }
  return `http://127.0.0.1:${address.port}/${inputName}`;
}

async function closeServer(server) {
  await new Promise((resolveClose, rejectClose) => {
    server.close((error) => (error ? rejectClose(error) : resolveClose()));
  });
}

async function main() {
  const server = createServer((request, response) => {
    if (request.url !== `/${inputName}`) {
      response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
      response.end("Not found");
      return;
    }
    response.writeHead(200, {
      "Cache-Control": "no-store",
      "Content-Length": inputBytes.length,
      "Content-Type": "text/html; charset=utf-8",
    });
    response.end(inputBytes);
  });

  let client;
  const capturedAt = new Date().toISOString();
  const runtimeSourceSha = gitValue(["rev-parse", "HEAD"]);

  try {
    const localUrl = await listen(server);
    client = await openOccamMcpSession({
      occamHome: repoRoot,
      command: process.execPath,
      args: [join(repoRoot, "scripts", "launch-mcp-host.mjs")],
      cwd: repoRoot,
      env: {
        OCCAM_HOME: repoRoot,
        OCCAM_ALLOW_PRIVATE_URLS: "1",
        OCCAM_RECEIPTS: "1",
      },
      clientInfo: { name: "occam-representative-proof", version: "1.0" },
      requestTimeoutMs: 120_000,
    });

    const result = parseToolResult(
      await client.request(
        "tools/call",
        {
          name: "occam_transcode",
          arguments: { url: localUrl },
        },
        120_000,
      ),
      localUrl,
    );

    if (result?.ok !== true || typeof result.markdown !== "string" || !result.markdown.trim()) {
      throw new Error("representative fixture did not return ok:true with non-empty markdown");
    }
    if (!result.markdown.includes("Web context without the chrome")) {
      throw new Error("representative fixture output lost the article heading");
    }

    const markdownBytes = Buffer.byteLength(result.markdown, "utf8");
    const reductionPercent = Number(
      ((1 - markdownBytes / inputBytes.length) * 100).toFixed(1),
    );

    writeJson("representative-input-metadata.json", {
      schemaVersion: 1,
      capturedAt,
      runtimeSourceSha,
      input: {
        localReproductionUrl: localUrl,
        publishedUrl:
          "https://contextforgeai.github.io/occam/examples/current-proof/representative-input.html",
        contentType: "text/html; charset=utf-8",
        bodyBytesAfterHttpDecoding: inputBytes.length,
        bodySha256: sha256(inputBytes),
      },
      invocation: {
        transport: "MCP stdio",
        tool: "occam_transcode",
        arguments: { url: "<local fixture URL>" },
        environment: { OCCAM_ALLOW_PRIVATE_URLS: "1" },
        outputProfile: "default options; only the required url argument",
      },
    });

    writeJson("representative-measurement.json", {
      schemaVersion: 1,
      capturedAt,
      runtimeSourceSha,
      method:
        "Compare the UTF-8 byte length of the controlled HTML response body with the UTF-8 byte length of result.markdown.",
      inputBodyBytesAfterHttpDecoding: inputBytes.length,
      outputMarkdownUtf8Bytes: markdownBytes,
      byteReductionForThisExamplePercent: reductionPercent,
      tokenizer: null,
      qualification:
        "This controlled page demonstrates removal of representative webpage chrome. It is not a universal size, token-savings, or answer-quality guarantee.",
    });

    writeFileSync(
      join(fixtureDir, "representative-output.md"),
      result.markdown,
      "utf8",
    );
    writeJson("representative-result.json", result);

    console.log(
      `REPRESENTATIVE_PROOF_OK runtime_sha=${runtimeSourceSha} input_bytes=${inputBytes.length} markdown_bytes=${markdownBytes} reduction=${reductionPercent}%`,
    );
  } finally {
    if (client) {
      await client.close();
    }
    if (server.listening) {
      await closeServer(server);
    }
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
});
