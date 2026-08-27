#!/usr/bin/env node
/**
 * Streamable HTTP smoke: POST /mcp initialize + GET /health.
 *
 * Usage:
 *   OCCAM_HOME=$PWD OCCAM_FORCE_DOTNET_RUN=1 node scripts/lib/mcp-streamable-http.selftest.mjs
 */
import { spawn } from "node:child_process";
import { setTimeout as delay } from "node:timers/promises";

const occamHome = process.env.OCCAM_HOME || process.cwd();
const port = Number(process.env.OCCAM_HTTP_PORT || 5055);
const baseUrl = `http://127.0.0.1:${port}`;

function spawnHttpHost() {
  const env = {
    ...process.env,
    OCCAM_HOME: occamHome,
    OCCAM_BANNER: process.env.OCCAM_BANNER ?? "0",
    OCCAM_FORCE_DOTNET_RUN: process.env.OCCAM_FORCE_DOTNET_RUN ?? "1",
  };
  return spawn(
    "dotnet",
    ["run", "--project", "src/FFOccamMcp.Core", "--", "--mcp-http", "--port", String(port)],
    {
      cwd: occamHome,
      env,
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
    },
  );
}

/**
 * @param {string} path
 * @param {RequestInit} init
 */
async function fetchJson(path, init = {}) {
  const res = await fetch(`${baseUrl}${path}`, init);
  const text = await res.text();
  return { status: res.status, text, headers: res.headers };
}

async function main() {
  const proc = spawnHttpHost();
  let stderr = "";
  proc.stderr.on("data", (chunk) => {
    stderr += chunk.toString();
  });

  try {
    await delay(8000);
    if (proc.exitCode !== null) {
      throw new Error(`host exited early code=${proc.exitCode}\n${stderr}`);
    }

    const health = await fetchJson("/health");
    if (health.status !== 200 || !health.text.includes('"ok":true')) {
      throw new Error(`health failed status=${health.status} body=${health.text.slice(0, 200)}`);
    }
    console.log("ok: GET /health");

    const initRes = await fetchJson("/mcp", {
      method: "POST",
      headers: {
        Accept: "application/json, text/event-stream",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: 1,
        method: "initialize",
        params: {
          protocolVersion: "2025-11-25",
          capabilities: {},
          clientInfo: { name: "occam-streamable-http-selftest", version: "1" },
        },
      }),
    });

    if (initRes.status !== 200) {
      throw new Error(`initialize failed status=${initRes.status} body=${initRes.text.slice(0, 400)}`);
    }

    const body = initRes.text;
    const hasServerInfo =
      body.includes("ff-occam") || body.includes("serverInfo") || body.includes("protocolVersion");
    if (!hasServerInfo) {
      throw new Error(`initialize body missing serverInfo markers: ${body.slice(0, 400)}`);
    }
    console.log(`ok: POST /mcp initialize status=${initRes.status}`);
    console.log("MCP_STREAMABLE_HTTP_SELFTEST_OK");
  } finally {
    proc.kill("SIGTERM");
    await delay(500);
    if (!proc.killed) {
      proc.kill("SIGKILL");
    }
  }
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
