import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { OccamMcpClient } from "../lib/client.js";
import {
  assertPublicMcpContract,
  schemaFingerprint,
} from "../../../scripts/lib/public-mcp-contract.mjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const fingerprintPath = join(root, "corpora", "public-mcp-schema-fingerprint.txt");

describe("public MCP contract via TypeScript client", () => {
  it("listTools matches RC1 schema + fingerprint corpus (tunnel launch binary)", async () => {
    if (!process.env.OCCAM_HOME) {
      process.env.OCCAM_HOME = root;
    }
    if (!existsSync(fingerprintPath)) {
      assert.fail(`missing ${fingerprintPath}`);
    }

    const client = new OccamMcpClient({
      env: { OCCAM_HOME: root, OCCAM_BANNER: "0" },
      handshakeTimeoutMs: 20_000,
      requestTimeoutMs: 30_000,
    });

    try {
      await client.start();
      const listed = await client.listTools();
      const tools = listed.tools || [];
      const check = assertPublicMcpContract(tools);
      assert.equal(check.ok, true, check.failures.join("; "));

      const fp = schemaFingerprint(tools);
      const expected = readFileSync(fingerprintPath, "utf8").trim();
      assert.equal(fp, expected, "TypeScript client tools/list fingerprint must match corpus");
    } finally {
      await client.stop();
    }
  });
});
