/**
 * Small entry codecs for non-stdio-identical config hosts.
 * Canonical form remains McpStdioOrUrlEntry (command/args/env/cwd).
 */
import { STDIO_ENTRY_CODEC, encodeStdioEntry } from "./config-engine.mjs";

export { STDIO_ENTRY_CODEC, encodeStdioEntry };

/**
 * VS Code mcp.json stdio entry — the documented schema requires `type: "stdio"`.
 */
export const VSCODE_ENTRY_CODEC = Object.freeze({
  /**
   * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }} desired
   */
  encode(desired) {
    return { type: "stdio", ...encodeStdioEntry(desired) };
  },
  // Decoding back to canonical form drops `type` with every other extra key.
  decode: STDIO_ENTRY_CODEC.decode,
});

/**
 * OpenCode local MCP entry:
 * { type: "local", command: [bin, ...args], environment, enabled }
 */
export const OPENCODE_ENTRY_CODEC = Object.freeze({
  /**
   * @param {{ command?: string, args?: string[], env?: Record<string, string>, cwd?: string }} desired
   */
  encode(desired) {
    /** @type {Record<string, unknown>} */
    const out = {
      type: "local",
      command: [desired.command, ...(desired.args || [])].filter((x) => x != null && x !== ""),
      enabled: true,
    };
    if (desired.env && Object.keys(desired.env).length) {
      out.environment = { ...desired.env };
    }
    if (desired.cwd) out.cwd = desired.cwd;
    return out;
  },
  /**
   * @param {unknown} raw
   */
  decode(raw) {
    if (!raw || typeof raw !== "object" || Array.isArray(raw)) return null;
    const o = /** @type {Record<string, unknown>} */ (raw);
    const cmd = o.command;
    if (!Array.isArray(cmd) || cmd.length === 0) return null;
    const envSrc =
      (o.environment && typeof o.environment === "object" && !Array.isArray(o.environment)
        ? o.environment
        : null) ||
      (o.env && typeof o.env === "object" && !Array.isArray(o.env) ? o.env : null) ||
      {};
    return {
      command: String(cmd[0]),
      args: cmd.slice(1).map(String),
      env: Object.fromEntries(
        Object.entries(/** @type {Record<string, unknown>} */ (envSrc)).map(([k, v]) => [
          k,
          String(v),
        ]),
      ),
      cwd: typeof o.cwd === "string" ? o.cwd : undefined,
    };
  },
});
