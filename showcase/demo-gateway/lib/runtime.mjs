import { join } from "node:path";
import {
  mcpToolText,
  openOccamMcpSession,
} from "../../../scripts/lib/mcp-stdio-client.mjs";

export function createOccamDemoRuntime({ repoRoot, timeoutMs = 40_000, maxTokens = 2048 }) {
  let client = null;
  let opening = null;

  async function ensureClient() {
    if (client) return client;
    if (!opening) {
      opening = openOccamMcpSession({
        occamHome: repoRoot,
        command: process.execPath,
        args: [join(repoRoot, "scripts", "launch-mcp-host.mjs")],
        cwd: repoRoot,
        env: {
          OCCAM_HOME: repoRoot,
          OCCAM_ALLOW_PRIVATE_URLS: "",
          OCCAM_BROWSER_AUTOINSTALL: "0",
          OCCAM_RECEIPTS: "",
        },
        clientInfo: { name: "occam-demo-gateway", version: "0.1" },
        requestTimeoutMs: timeoutMs,
      }).then((opened) => {
        client = opened;
        return opened;
      }).finally(() => {
        opening = null;
      });
    }
    return opening;
  }

  return {
    async transcode(url) {
      const active = await ensureClient();
      try {
        const result = await active.request("tools/call", {
          name: "occam_transcode",
          arguments: {
            url,
            backend_policy: "http",
            max_tokens: maxTokens,
            playbook_policy: "off",
          },
        }, timeoutMs);
        const text = mcpToolText(result);
        if (!text) throw new Error("Occam returned no text payload");
        return JSON.parse(text);
      } catch (error) {
        const stale = client;
        client = null;
        if (stale) await stale.close({ graceMs: 500 });
        throw error;
      }
    },
    async close() {
      const active = client;
      client = null;
      if (active) await active.close();
    },
  };
}
