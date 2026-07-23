import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createAgentClient, OccamAgentClient } from "../dist/client.js";

const packageRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const fakeHost = join(
  packageRoot,
  "..",
  "occam-mcp",
  "test",
  "fixtures",
  "fake-mcp-server.mjs",
);

describe("OccamAgentClient", () => {
  it("preserves the extended prototype over the initialized base client", async () => {
    const tempRoot = mkdtempSync(join(tmpdir(), "occam-agent-client-"));
    const marker = join(tempRoot, "closed.txt");
    const client = await createAgentClient({
      binaryPath: process.execPath,
      args: [fakeHost, marker, "normal"],
      handshakeTimeoutMs: 500,
      requestTimeoutMs: 500,
      shutdownTimeoutMs: 500,
    });

    try {
      assert.equal(client instanceof OccamAgentClient, true);
      const tools = await client.listTools();
      assert.equal(tools.tools[0]?.name, "occam_probe");
      const result = await client.callTool("occam_probe", { url: "https://example.com" });
      assert.equal(result.ok, true);
      assert.equal(result.initialized, true);
    } finally {
      await client.stop();
      rmSync(tempRoot, { recursive: true, force: true });
    }
  });
});
