#!/usr/bin/env node
/**
 * Hermes / generic MCP host smoke — subprocess stdio MCP.
 * Usage: node scripts/hermes-smoke.mjs
 * Exit 0 on PASS, 1 on FAIL. JSON report on stdout (last line).
 */
import { spawn } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const PROBE_URL = "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide";
const REQUEST_TIMEOUT_MS = 60_000;
const EXPECTED_TOOLS = 15;

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;

  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", () => {});
  }

  #sendLine(obj) {
    this.#proc.stdin.write(`${JSON.stringify(obj)}\n`);
  }

  #onData(chunk) {
    this.#buffer += chunk;
    for (;;) {
      const nl = this.#buffer.indexOf("\n");
      if (nl === -1) break;
      const line = this.#buffer.slice(0, nl).trim();
      this.#buffer = this.#buffer.slice(nl + 1);
      if (!line) continue;
      let msg;
      try {
        msg = JSON.parse(line);
      } catch {
        continue;
      }
      if (msg.id != null && this.#pending.has(msg.id)) {
        const { resolve, reject } = this.#pending.get(msg.id);
        this.#pending.delete(msg.id);
        if (msg.error) reject(new Error(JSON.stringify(msg.error)));
        else resolve(msg.result);
      }
    }
  }

  notify(method, params = {}) {
    this.#sendLine({ jsonrpc: "2.0", method, params });
  }

  request(method, params = {}) {
    const id = this.#id++;
    this.#sendLine({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.#pending.has(id)) {
          this.#pending.delete(id);
          reject(new Error(`MCP timeout ${REQUEST_TIMEOUT_MS}ms: ${method}`));
        }
      }, REQUEST_TIMEOUT_MS);
      this.#pending.set(id, {
        resolve: (v) => {
          clearTimeout(timer);
          resolve(v);
        },
        reject: (e) => {
          clearTimeout(timer);
          reject(e);
        },
      });
    });
  }

  close() {
    this.#proc.stdin.end();
  }
}

function parseToolJson(result) {
  const text = result?.content?.find((c) => c.type === "text")?.text;
  if (!text) return { parsed: null, raw: result };
  try {
    return { parsed: JSON.parse(text), raw: text };
  } catch {
    return { parsed: null, raw: text };
  }
}

async function main() {
  const report = {
    ok: false,
    startedAt: new Date().toISOString(),
    occamHome: root,
    steps: {},
    errors: [],
  };

  const launcher = join(root, "scripts", "launch-mcp-host.mjs");
  const proc = spawn(process.execPath, [launcher], {
    cwd: root,
    env: {
      ...process.env,
      OCCAM_HOME: root,
      Logging__LogLevel__Default: "None",
      WT_OCCAM_BANNER: "0",
    },
    stdio: ["pipe", "pipe", "pipe"],
  });

  const client = new McpStdioClient(proc);
  const killTimer = setTimeout(() => proc.kill(), 120_000);

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "hermes-smoke", version: "1.0" },
    });
    client.notify("notifications/initialized");

    const toolsResult = await client.request("tools/list", {});
    const occamTools = (toolsResult?.tools ?? []).filter((t) => t.name?.startsWith("occam_"));
    report.steps.toolsList = {
      total: toolsResult?.tools?.length ?? 0,
      occamCount: occamTools.length,
      names: occamTools.map((t) => t.name),
    };

    if (occamTools.length !== EXPECTED_TOOLS) {
      report.errors.push(`expected ${EXPECTED_TOOLS} occam_* tools, got ${occamTools.length}`);
    }

    const probeResult = parseToolJson(
      await client.request("tools/call", {
        name: "occam_probe",
        arguments: { url: PROBE_URL },
      }),
    );
    report.steps.probe = {
      url: PROBE_URL,
      ok: probeResult.parsed?.ok === true,
      suggestedNextTool: probeResult.parsed?.agentHints?.suggestedNextTool ?? null,
      pageClass: probeResult.parsed?.classification?.pageClass ?? null,
    };

    if (!probeResult.parsed?.ok) {
      report.errors.push(`occam_probe failed: ${probeResult.raw}`);
    }
    if (!probeResult.parsed?.agentHints?.suggestedNextTool) {
      report.errors.push("occam_probe missing agentHints.suggestedNextTool");
    }

    report.ok = report.errors.length === 0;
    report.finishedAt = new Date().toISOString();
    console.log(JSON.stringify(report));
    process.exit(report.ok ? 0 : 1);
  } catch (err) {
    report.errors.push(String(err?.message ?? err));
    report.finishedAt = new Date().toISOString();
    console.log(JSON.stringify(report));
    process.exit(1);
  } finally {
    clearTimeout(killTimer);
    client.close();
    proc.kill();
  }
}

main();
