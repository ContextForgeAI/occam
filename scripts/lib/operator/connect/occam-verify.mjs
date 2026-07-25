/**
 * Occam-side MCP verification (Levels 2–4) via stable launcher.
 */
import { spawn } from "node:child_process";
import { buildStableLaunchSpec } from "./launch-spec.mjs";
import { VERIFICATION_LEVELS } from "./verification.mjs";

const REQUEST_TIMEOUT_MS = 60_000;
const EXPECTED_MIN_TOOLS = 15;

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;

  /** @param {import('node:child_process').ChildProcessWithoutNullStreams} proc */
  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", () => {});
  }

  /** @param {object} obj */
  #sendLine(obj) {
    this.#proc.stdin.write(`${JSON.stringify(obj)}\n`);
  }

  /** @param {string} chunk */
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
    try {
      this.#proc.stdin.end();
    } catch {
      /* ignore */
    }
    try {
      this.#proc.kill();
    } catch {
      /* ignore */
    }
  }
}

/**
 * @param {string} occamHome
 * @returns {Promise<{ ok: boolean, level: import('./verification.mjs').VerificationLevel, toolCount: number, error?: string }>}
 */
export async function verifyOccamMcp(occamHome) {
  let spec;
  try {
    spec = buildStableLaunchSpec(occamHome);
  } catch (err) {
    return {
      ok: false,
      level: VERIFICATION_LEVELS.INSTALLED,
      toolCount: 0,
      error: err instanceof Error ? err.message : String(err),
    };
  }

  const proc = spawn(spec.command, spec.args, {
    cwd: spec.cwd,
    env: { ...process.env, ...spec.env },
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true,
  });

  if (!proc.pid) {
    return {
      ok: false,
      level: VERIFICATION_LEVELS.CONFIG_VALID,
      toolCount: 0,
      error: "failed to spawn Occam MCP process",
    };
  }

  const client = new McpStdioClient(proc);
  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: { name: "occam-connect-verify", version: "1.0.0" },
    });
    client.notify("notifications/initialized");
    const listed = await client.request("tools/list", {});
    const tools = Array.isArray(listed?.tools) ? listed.tools : [];
    const toolCount = tools.length;
    if (toolCount < EXPECTED_MIN_TOOLS) {
      return {
        ok: false,
        level: VERIFICATION_LEVELS.INITIALIZE_OK,
        toolCount,
        error: `expected >= ${EXPECTED_MIN_TOOLS} tools, got ${toolCount}`,
      };
    }
    return { ok: true, level: VERIFICATION_LEVELS.TOOLS_LIST_OK, toolCount };
  } catch (err) {
    const launched = Boolean(proc.pid);
    return {
      ok: false,
      level: launched ? VERIFICATION_LEVELS.PROCESS_LAUNCHES : VERIFICATION_LEVELS.CONFIG_VALID,
      toolCount: 0,
      error: err instanceof Error ? err.message : String(err),
    };
  } finally {
    client.close();
  }
}
