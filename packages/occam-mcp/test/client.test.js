import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  MCP_PROTOCOL_VERSION,
  OccamMcpClient,
} from "../lib/client.js";

const testDir = dirname(fileURLToPath(import.meta.url));
const fixture = join(testDir, "fixtures", "fake-mcp-server.mjs");

function createFixtureClient(mode = "normal") {
  const tempRoot = mkdtempSync(join(tmpdir(), "occam-client-"));
  const marker = join(tempRoot, "closed.txt");
  const client = new OccamMcpClient({
    binaryPath: process.execPath,
    args: [fixture, marker, mode],
    handshakeTimeoutMs: 500,
    requestTimeoutMs: 100,
    shutdownTimeoutMs: 500,
  });
  return { client, marker, tempRoot };
}

describe("OccamMcpClient lifecycle", () => {
  it("initializes, lists tools, unwraps tool JSON, and shuts down idempotently", async () => {
    const { client, marker, tempRoot } = createFixtureClient();
    try {
      await Promise.all([client.start(), client.start()]);
      assert.equal(client.negotiatedProtocolVersion, MCP_PROTOCOL_VERSION);

      const listed = await client.listTools();
      assert.deepEqual(listed.tools.map((tool) => tool.name), ["occam_probe"]);

      const result = await client.callTool("occam_probe", { url: "https://example.com" });
      assert.deepEqual(result, {
        ok: true,
        tool: "occam_probe",
        arguments: { url: "https://example.com" },
        initialized: true,
        requestedProtocolVersion: MCP_PROTOCOL_VERSION,
      });

      await Promise.all([client.stop(), client.stop()]);
      assert.equal(existsSync(marker), true, "the fixture observed stdin EOF before exit");
    } finally {
      await client.stop();
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });

  it("accepts a server-selected supported legacy revision", async () => {
    const { client, tempRoot } = createFixtureClient("legacy-protocol");
    try {
      await client.start();
      assert.equal(client.negotiatedProtocolVersion, "2024-11-05");
    } finally {
      await client.stop();
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });

  it("disconnects when the server selects an unsupported revision", async () => {
    const { client, marker, tempRoot } = createFixtureClient("unsupported-protocol");
    try {
      await assert.rejects(client.start(), /Unsupported MCP protocol version.*2099-01-01/);
      assert.equal(client.negotiatedProtocolVersion, null);
      assert.equal(existsSync(marker), true);
    } finally {
      await client.stop();
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });

  it("rejects quickly when the host exits during initialization", async () => {
    const { client, tempRoot } = createFixtureClient("exit-immediately");
    try {
      await assert.rejects(client.start(), /exited|EPIPE|write/i);
      await client.stop();
    } finally {
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });

  it("expires a stalled request without leaving a recurring timer", async () => {
    const { client, marker, tempRoot } = createFixtureClient("ignore-tool");
    try {
      await client.start();
      await assert.rejects(
        client.callTool("occam_probe", { url: "https://example.com" }),
        /MCP request timeout: tools\/call/,
      );
      await client.stop();
      assert.equal(existsSync(marker), true);
    } finally {
      await client.stop();
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });
});
