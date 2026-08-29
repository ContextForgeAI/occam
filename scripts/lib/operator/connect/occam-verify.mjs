/**
 * Occam-side MCP verification (Levels 2–4) via stable launcher.
 */
import { spawn } from "node:child_process";
import { buildStableLaunchSpec } from "./launch-spec.mjs";
import { VERIFICATION_LEVELS } from "./verification.mjs";

const REQUEST_TIMEOUT_MS = 60_000;

// Every supported OCCAM_PROFILE includes this reader baseline. Verify tool
// identity instead of a historical full-profile count: reader exposes 8 tools,
// while researcher, auditor, full, and opt-in surfaces legitimately expose more.
export const REQUIRED_BASELINE_TOOLS = Object.freeze([
  "occam_client_capabilities",
  "occam_transcode",
  "occam_probe",
  "occam_digest",
  "occam_map",
  "occam_search",
  "occam_extract_knowledge",
  "occam_verify",
]);

/**
 * @param {unknown[]} tools
 * @returns {{ ok: boolean, toolCount: number, missingTools: string[], duplicateTools: string[] }}
 */
export function evaluateOccamToolList(tools) {
  const list = Array.isArray(tools) ? tools : [];
  const counts = new Map();
  for (const tool of list) {
    const name = tool && typeof tool === "object" ? tool.name : null;
    if (typeof name !== "string" || name.length === 0) continue;
    counts.set(name, (counts.get(name) ?? 0) + 1);
  }
  const names = new Set(counts.keys());
  const missingTools = REQUIRED_BASELINE_TOOLS.filter((name) => !names.has(name));
  const duplicateTools = [...counts]
    .filter(([, count]) => count > 1)
    .map(([name]) => name)
    .sort();
  return {
    ok: missingTools.length === 0 && duplicateTools.length === 0,
    toolCount: list.length,
    missingTools,
    duplicateTools,
  };
}

/**
 * Wait for Node's asynchronous child-process launch result. `spawn()` may
 * return before an ENOENT/EPERM failure is emitted on the child.
 * @param {import('node:child_process').ChildProcess} proc
 * @returns {Promise<void>}
 */
export function waitForChildSpawn(proc) {
  return new Promise((resolve, reject) => {
    const cleanup = () => {
      proc.off("spawn", onSpawn);
      proc.off("error", onError);
    };
    const onSpawn = () => {
      cleanup();
      resolve();
    };
    const onError = (err) => {
      cleanup();
      reject(err);
    };
    proc.once("spawn", onSpawn);
    proc.once("error", onError);
  });
}

class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;
  #terminalError = null;

  /** @param {import('node:child_process').ChildProcessWithoutNullStreams} proc */
  constructor(proc) {
    this.#proc = proc;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", () => {});
    proc.on("error", (err) => this.#failPending(err));
    proc.on("exit", (code, signal) => {
      this.#failPending(
        new Error(
          `Occam MCP process exited before verification completed (code=${code ?? "null"}, signal=${signal ?? "none"})`,
        ),
      );
    });
    proc.stdin.on("error", (err) => this.#failPending(err));
  }

  /** @param {object} obj */
  #sendLine(obj) {
    if (this.#terminalError) throw this.#terminalError;
    if (!this.#proc.stdin.writable) throw new Error("Occam MCP stdin is not writable");
    this.#proc.stdin.write(`${JSON.stringify(obj)}\n`);
  }

  /** @param {unknown} err */
  #failPending(err) {
    if (this.#terminalError) return;
    this.#terminalError = err instanceof Error ? err : new Error(String(err));
    for (const [id, pending] of this.#pending) {
      this.#pending.delete(id);
      pending.reject(this.#terminalError);
    }
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
    if (this.#terminalError) return Promise.reject(this.#terminalError);
    const id = this.#id++;
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
      try {
        this.#sendLine({ jsonrpc: "2.0", id, method, params });
      } catch (err) {
        const pending = this.#pending.get(id);
        this.#pending.delete(id);
        if (pending) {
          pending.reject(err);
        } else {
          clearTimeout(timer);
          reject(err);
        }
      }
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

  let proc;
  try {
    proc = spawn(spec.command, spec.args, {
      cwd: spec.cwd,
      env: { ...process.env, ...spec.env },
      stdio: ["pipe", "pipe", "pipe"],
      windowsHide: true,
    });
  } catch (err) {
    return {
      ok: false,
      level: VERIFICATION_LEVELS.CONFIG_VALID,
      toolCount: 0,
      error: err instanceof Error ? err.message : String(err),
    };
  }

  try {
    await waitForChildSpawn(proc);
  } catch (err) {
    return {
      ok: false,
      level: VERIFICATION_LEVELS.CONFIG_VALID,
      toolCount: 0,
      error: err instanceof Error ? err.message : String(err),
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
    const evaluated = evaluateOccamToolList(tools);
    if (!evaluated.ok) {
      const problems = [];
      if (evaluated.missingTools.length > 0) {
        problems.push(`missing required Occam tools: ${evaluated.missingTools.join(", ")}`);
      }
      if (evaluated.duplicateTools.length > 0) {
        problems.push(`duplicate Occam tools: ${evaluated.duplicateTools.join(", ")}`);
      }
      return {
        ok: false,
        level: VERIFICATION_LEVELS.INITIALIZE_OK,
        toolCount: evaluated.toolCount,
        error: problems.join("; "),
      };
    }
    return {
      ok: true,
      level: VERIFICATION_LEVELS.TOOLS_LIST_OK,
      toolCount: evaluated.toolCount,
    };
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
