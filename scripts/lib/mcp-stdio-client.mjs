/**
 * Minimal newline-delimited JSON-RPC MCP stdio client.
 * Extracted seam for hermes-smoke / connect verify / experimental local chat.
 * Do not expand into a full MCP SDK here.
 */
import { spawn, spawnSync } from "node:child_process";

const DEFAULT_TIMEOUT_MS = 60_000;

export class McpStdioClient {
  #proc;
  #buffer = "";
  #pending = new Map();
  #id = 1;
  #closed = false;
  #timeoutMs;

  /**
   * @param {import('node:child_process').ChildProcessWithoutNullStreams} proc
   * @param {{ requestTimeoutMs?: number, onStderr?: (chunk: string) => void }} [opts]
   */
  constructor(proc, opts = {}) {
    this.#proc = proc;
    this.#timeoutMs = opts.requestTimeoutMs ?? DEFAULT_TIMEOUT_MS;
    proc.stdout.on("data", (chunk) => this.#onData(chunk.toString()));
    proc.stderr.on("data", (chunk) => {
      if (typeof opts.onStderr === "function") opts.onStderr(chunk.toString());
    });
    proc.on("exit", () => {
      this.#rejectAll(new Error("MCP process exited"));
    });
  }

  get pid() {
    return this.#proc.pid ?? null;
  }

  get process() {
    return this.#proc;
  }

  /** @param {object} obj */
  #sendLine(obj) {
    if (this.#closed || !this.#proc.stdin.writable) {
      throw new Error("MCP stdin closed");
    }
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

  /** @param {Error} err */
  #rejectAll(err) {
    for (const [, entry] of this.#pending) {
      entry.reject(err);
    }
    this.#pending.clear();
  }

  /**
   * @param {string} method
   * @param {object} [params]
   */
  notify(method, params = {}) {
    this.#sendLine({ jsonrpc: "2.0", method, params });
  }

  /**
   * @param {string} method
   * @param {object} [params]
   * @param {number} [timeoutMs]
   */
  request(method, params = {}, timeoutMs = this.#timeoutMs) {
    const id = this.#id++;
    this.#sendLine({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.#pending.has(id)) {
          this.#pending.delete(id);
          reject(new Error(`MCP timeout ${timeoutMs}ms: ${method}`));
        }
      }, timeoutMs);
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

/**
 * Soft close: end stdin, then terminate the launcher process tree.
 * Windows: taskkill /T. Unix: kill process group when detached, else SIGTERM/SIGKILL + child sweep.
 * @param {{ graceMs?: number }} [opts]
 */
async close(opts = {}) {
  if (this.#closed) return;
  this.#closed = true;
  const graceMs = opts.graceMs ?? 2_000;
  const pid = this.#proc.pid;
  try {
    this.#proc.stdin.end();
  } catch {
    /* ignore */
  }
  if (this.#proc.killed || this.#proc.exitCode != null) {
    if (pid) killDescendants(pid);
    return;
  }

  await new Promise((resolve) => {
    const onExit = () => {
      clearTimeout(killTimer);
      if (pid) killDescendants(pid);
      resolve();
    };
    this.#proc.once("exit", onExit);
    try {
      if (process.platform !== "win32" && pid && this.#proc.pid) {
        // Negative PID = process group (spawned detached on Unix).
        try {
          process.kill(-pid, "SIGTERM");
        } catch {
          this.#proc.kill("SIGTERM");
        }
      } else {
        this.#proc.kill("SIGTERM");
      }
    } catch {
      /* ignore */
    }
    const killTimer = setTimeout(() => {
      try {
        if (process.platform === "win32" && pid) {
          spawnSync("taskkill", ["/PID", String(pid), "/T", "/F"], { stdio: "ignore" });
        } else if (pid) {
          try {
            process.kill(-pid, "SIGKILL");
          } catch {
            try {
              this.#proc.kill("SIGKILL");
            } catch {
              /* ignore */
            }
          }
          killDescendants(pid);
        }
      } catch {
        /* ignore */
      }
      resolve();
    }, graceMs);
  });
}
}

/**
 * Best-effort child sweep for Unix (http-daemon etc. reparented after parent exit).
 * @param {number} pid
 */
function killDescendants(pid) {
  if (process.platform === "win32" || !pid) return;
  try {
    const listed = spawnSync("pgrep", ["-P", String(pid)], { encoding: "utf8" });
    const kids = String(listed.stdout || "")
      .trim()
      .split(/\s+/)
      .map((s) => Number(s))
      .filter((n) => Number.isInteger(n) && n > 0);
    for (const child of kids) {
      killDescendants(child);
      try {
        process.kill(child, "SIGKILL");
      } catch {
        /* ignore */
      }
    }
  } catch {
    /* ignore */
  }
}

/**
 * Spawn Occam MCP via launch-mcp-host and return an initialized client.
 * @param {{
 *   occamHome: string,
 *   command: string,
 *   args: string[],
 *   env?: Record<string, string>,
 *   cwd?: string,
 *   clientInfo?: { name: string, version: string },
 *   requestTimeoutMs?: number,
 * }} opts
 */
export async function openOccamMcpSession(opts) {
  const proc = spawn(opts.command, opts.args, {
    cwd: opts.cwd ?? opts.occamHome,
    env: { ...process.env, ...(opts.env ?? {}) },
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true,
    // New process group on Unix so close() can signal -pid and reap Core + workers.
    detached: process.platform !== "win32",
  });

  if (!proc.pid) {
    throw new Error("failed to spawn Occam MCP process");
  }

  const client = new McpStdioClient(proc, {
    requestTimeoutMs: opts.requestTimeoutMs,
  });

  try {
    await client.request("initialize", {
      protocolVersion: "2024-11-05",
      capabilities: {},
      clientInfo: opts.clientInfo ?? { name: "occam-mcp-stdio", version: "1.0" },
    });
    client.notify("notifications/initialized");
  } catch (err) {
    await client.close({ graceMs: 500 });
    throw err;
  }

  return client;
}

/**
 * Extract text payload from a tools/call result (Occam returns JSON in text content).
 * @param {unknown} result
 */
export function mcpToolText(result) {
  const content = /** @type {{ content?: Array<{ type?: string, text?: string }> }} */ (result)?.content;
  if (!Array.isArray(content)) return null;
  const text = content.find((c) => c?.type === "text")?.text;
  return typeof text === "string" ? text : null;
}
