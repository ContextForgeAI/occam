#!/usr/bin/env node
/**
 * Streamable HTTP smoke: POST /mcp initialize + GET /health.
 *
 * Prefers published AOT host when present (CI gate-fast places OccamMcp.Core at OCCAM_HOME).
 * Falls back to `dotnet run` when OCCAM_FORCE_DOTNET_RUN=1 or no binary is found.
 *
 * Usage:
 *   OCCAM_HOME=$PWD OCCAM_FORCE_DOTNET_RUN=1 node scripts/lib/mcp-streamable-http.selftest.mjs
 */
import { spawn } from "node:child_process";
import { createServer } from "node:net";
import { setTimeout as delay } from "node:timers/promises";
import { resolveHostBinary } from "./resolve-host-binary.mjs";

const occamHome = process.env.OCCAM_HOME || process.cwd();

async function freePort() {
  const preferred = Number(process.env.OCCAM_HTTP_PORT || 0);
  if (preferred > 0) {
    return preferred;
  }
  return await new Promise((resolve, reject) => {
    const srv = createServer();
    srv.listen(0, "127.0.0.1", () => {
      const addr = srv.address();
      const port = typeof addr === "object" && addr ? addr.port : 0;
      srv.close((err) => (err ? reject(err) : resolve(port)));
    });
    srv.on("error", reject);
  });
}

function spawnHttpHost(port) {
  const forceDotnet =
    process.env.OCCAM_FORCE_DOTNET_RUN === "1" ||
    process.env.OCCAM_FORCE_DOTNET_RUN === "true";
  const env = {
    ...process.env,
    OCCAM_HOME: occamHome,
    OCCAM_BANNER: process.env.OCCAM_BANNER ?? "0",
    OCCAM_PROFILE: process.env.OCCAM_PROFILE ?? "full",
  };
  const hostArgs = ["--mcp-http", "--port", String(port)];

  if (!forceDotnet) {
    const binary = resolveHostBinary(occamHome);
    if (binary) {
      return spawn(binary, hostArgs, {
        cwd: occamHome,
        env,
        stdio: ["ignore", "pipe", "pipe"],
        windowsHide: true,
      });
    }
  }

  return spawn(
    "dotnet",
    ["run", "--project", "src/FFOccamMcp.Core", "--no-launch-profile", "--", ...hostArgs],
    {
      cwd: occamHome,
      env: { ...env, OCCAM_FORCE_DOTNET_RUN: "1" },
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
    },
  );
}

/**
 * @param {string} baseUrl
 * @param {string} path
 * @param {RequestInit} init
 */
async function fetchJson(baseUrl, path, init = {}) {
  const res = await fetch(`${baseUrl}${path}`, init);
  const text = await res.text();
  return { status: res.status, text, headers: res.headers };
}

async function waitForHealth(baseUrl, proc, stderrBag, timeoutMs = 90_000) {
  const started = Date.now();
  let lastErr = "not started";
  while (Date.now() - started < timeoutMs) {
    if (proc.exitCode !== null) {
      throw new Error(`host exited early code=${proc.exitCode}\n${stderrBag()}`);
    }
    try {
      const health = await fetchJson(baseUrl, "/health");
      if (health.status === 200 && health.text.includes('"ok":true')) {
        return health;
      }
      lastErr = `status=${health.status} body=${health.text.slice(0, 200)}`;
    } catch (err) {
      lastErr = err instanceof Error ? err.message : String(err);
    }
    await delay(1000);
  }
  throw new Error(`health timeout after ${timeoutMs}ms (${lastErr})\n${stderrBag()}`);
}

async function main() {
  const port = await freePort();
  const baseUrl = `http://127.0.0.1:${port}`;
  const proc = spawnHttpHost(port);
  let stderr = "";
  proc.stderr.on("data", (chunk) => {
    stderr += chunk.toString();
  });
  proc.stdout?.on("data", () => {});

  try {
    await waitForHealth(baseUrl, proc, () => stderr);
    console.log("ok: GET /health");

    const initRes = await fetchJson(baseUrl, "/mcp", {
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
      throw new Error(
        `initialize failed status=${initRes.status} body=${initRes.text.slice(0, 400)}\n${stderr}`,
      );
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
    if (proc.exitCode === null) {
      proc.kill("SIGKILL");
    }
  }
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
