#!/usr/bin/env node
/**
 * Dual-era MCP handshake selftest against the local host:
 *   - legacy initialize (2024-11-05 / 2025-11-25)
 *   - modern server/discover + per-request _meta (2026-07-28)
 *
 * Requires ModelContextProtocol >= 2.2.0 and a host built from this tree.
 * Prefer OCCAM_FORCE_DOTNET_RUN=1 so a stale AOT publish is not used.
 *
 * Usage:
 *   OCCAM_HOME=$PWD OCCAM_FORCE_DOTNET_RUN=1 node scripts/lib/mcp-dual-era.selftest.mjs
 */
import { spawn } from "node:child_process";
import { pathToFileURL } from "node:url";
import { McpStdioClient } from "./mcp-stdio-client.mjs";
import { resolveHostBinary } from "./resolve-host-binary.mjs";

const META_2026 = {
  "io.modelcontextprotocol/protocolVersion": "2026-07-28",
  "io.modelcontextprotocol/clientInfo": { name: "occam-dual-era-selftest", version: "1" },
  "io.modelcontextprotocol/clientCapabilities": {},
};

function spawnHost() {
  const occamHome = process.env.OCCAM_HOME || process.cwd();
  const forceDotnet =
    process.env.OCCAM_FORCE_DOTNET_RUN === "1" ||
    process.env.OCCAM_FORCE_DOTNET_RUN === "true";
  const env = {
    ...process.env,
    OCCAM_HOME: occamHome,
    OCCAM_BANNER: process.env.OCCAM_BANNER ?? "0",
    OCCAM_FORCE_DOTNET_RUN: process.env.OCCAM_FORCE_DOTNET_RUN ?? "1",
    OCCAM_PROFILE: process.env.OCCAM_PROFILE ?? "full",
  };
  const spawnOpts = {
    cwd: occamHome,
    env,
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true,
    // Process group on Unix so close() can SIGTERM/SIGKILL the whole tree (Core + workers).
    detached: process.platform !== "win32",
  };

  if (!forceDotnet) {
    const binary = resolveHostBinary(occamHome);
    if (binary) {
      return spawn(binary, [], spawnOpts);
    }
  }

  return spawn(process.execPath, ["scripts/launch-mcp-host.mjs"], spawnOpts);
}

/**
 * @param {(client: McpStdioClient) => Promise<void>} fn
 */
async function withClient(fn) {
  const proc = spawnHost();
  const client = new McpStdioClient(proc, { requestTimeoutMs: 90_000 });
  try {
    // Host may cold-start under dotnet run.
    await new Promise((r) => setTimeout(r, 1500));
    await fn(client);
  } finally {
    await client.close({ graceMs: 800 });
  }
}

async function assertLegacy(protocolVersion) {
  await withClient(async (client) => {
    const init = await client.request("initialize", {
      protocolVersion,
      capabilities: {},
      clientInfo: { name: "occam-dual-era-selftest", version: "1" },
    });
    if (!init || typeof init !== "object") {
      throw new Error(`legacy ${protocolVersion}: empty initialize result`);
    }
    const negotiated = /** @type {{ protocolVersion?: string, capabilities?: Record<string, unknown>, serverInfo?: { name?: string } }} */ (
      init
    ).protocolVersion;
    if (!negotiated) {
      throw new Error(`legacy ${protocolVersion}: missing negotiated protocolVersion`);
    }
    const caps = /** @type {{ capabilities?: any, serverInfo?: { name?: string } }} */ (init);
    if (caps.serverInfo?.name !== "ff-occam") {
      throw new Error(
        `legacy ${protocolVersion}: expected serverInfo.name=ff-occam got ${caps.serverInfo?.name}`,
      );
    }
    const toolsCap = caps.capabilities?.tools;
    if (toolsCap && toolsCap.listChanged === true) {
      throw new Error(
        `legacy ${protocolVersion}: tools.listChanged must not be advertised as true`,
      );
    }
    if (caps.capabilities?.logging != null) {
      throw new Error(
        `legacy ${protocolVersion}: logging capability must not be advertised (got ${JSON.stringify(caps.capabilities.logging)})`,
      );
    }
    // Server may negotiate down (e.g. request 2026-07-28 via initialize → 2025-11-25).
    client.notify("notifications/initialized");
    const list = await client.request("tools/list", {});
    const tools = /** @type {{ tools?: unknown[] }} */ (list)?.tools;
    if (!Array.isArray(tools) || tools.length < 1) {
      throw new Error(`legacy ${protocolVersion}: tools/list returned no tools`);
    }
    console.log(`ok: legacy initialize ${protocolVersion} → ${negotiated} tools=${tools.length}`);
  });
}

async function assertDiscover() {
  await withClient(async (client) => {
    const disc = await client.request("server/discover", { _meta: META_2026 });
    const supported = /** @type {{ supportedVersions?: string[] }} */ (disc)?.supportedVersions;
    if (!Array.isArray(supported) || !supported.includes("2026-07-28")) {
      throw new Error(
        `server/discover missing 2026-07-28 in supportedVersions: ${JSON.stringify(supported)}`,
      );
    }
    const list = await client.request("tools/list", { _meta: META_2026 });
    const tools = /** @type {{ tools?: unknown[] }} */ (list)?.tools;
    if (!Array.isArray(tools) || tools.length < 1) {
      throw new Error("server/discover path: tools/list returned no tools");
    }
    console.log(`ok: server/discover 2026-07-28 tools=${tools.length}`);
  });
}

async function main() {
  await assertLegacy("2024-11-05");
  await assertLegacy("2025-11-25");
  await assertDiscover();
  console.log("mcp-dual-era.selftest: OK");
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main()
    .then(() => {
      // CI gate-fast: lingering MCP host pipes keep the event loop open after OK.
      process.exit(0);
    })
    .catch((err) => {
      console.error(`error: ${err instanceof Error ? err.message : String(err)}`);
      process.exit(1);
    });
}
