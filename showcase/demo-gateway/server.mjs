#!/usr/bin/env node
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createDemoGateway } from "./lib/gateway.mjs";
import { createOccamDemoRuntime } from "./lib/runtime.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "../..");
const host = process.env.DEMO_HOST || "127.0.0.1";
const port = Number(process.env.PORT || 8787);
const runtime = createOccamDemoRuntime({
  repoRoot,
  timeoutMs: Number(process.env.DEMO_MCP_TIMEOUT_MS || 40_000),
  maxTokens: Number(process.env.DEMO_MAX_TOKENS || 2048),
});
const server = createDemoGateway({
  runtime,
  enabled: process.env.DEMO_ENABLED !== "0",
  allowedOrigin: process.env.DEMO_ALLOWED_ORIGIN || "",
  trustProxy: process.env.DEMO_TRUST_PROXY === "1",
  perMinute: Number(process.env.DEMO_RATE_PER_MINUTE || 3),
  perDay: Number(process.env.DEMO_RATE_PER_DAY || 30),
  maxConcurrency: Number(process.env.DEMO_MAX_CONCURRENCY || 2),
  maxMarkdownChars: Number(process.env.DEMO_MAX_MARKDOWN_CHARS || 12_000),
});

server.listen(port, host, () => {
  console.log(`OCCAM_DEMO_GATEWAY_READY http://${host}:${port}`);
});

async function shutdown() {
  server.close();
  await runtime.close();
}

process.once("SIGINT", shutdown);
process.once("SIGTERM", shutdown);
