/**
 * Structured MCP config engine (Wave 2+) — product-agnostic JSON mcpServers merge.
 *
 * Host profiles supply paths / rootKey / entry shape. Engine owns:
 * load → inspect → plan → backup → atomic write → validate → restore.
 *
 * Does not invent unknown keys on entries. Ownership uses existing
 * looksLikeOccamManagedEntry / OCCAM_CONNECT_MANAGED env marker only.
 */
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import { dirname, join } from "node:path";
import { looksLikeOccamManagedEntry } from "./ownership.mjs";
import { argsEqual, envEqual, normalizePathish } from "./process.mjs";

/**
 * @typedef {{
 *   command?: string,
 *   args?: string[],
 *   cwd?: string,
 *   env?: Record<string, string>,
 *   url?: string,
 *   headers?: Record<string, string>,
 * }} McpStdioOrUrlEntry
 *
 * @typedef {{
 *   encode: (desired: McpStdioOrUrlEntry) => Record<string, unknown>,
 *   decode: (raw: unknown) => McpStdioOrUrlEntry|null,
 * }} EntryCodec
 */

/**
 * @param {unknown} value
 * @returns {value is Record<string, unknown>}
 */
function isPlainObject(value) {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

/**
 * Default stdio entry writer — only known fields.
 * @param {McpStdioOrUrlEntry} desired
 * @returns {McpStdioOrUrlEntry}
 */
export function encodeStdioEntry(desired) {
  /** @type {McpStdioOrUrlEntry} */
  const entry = {
    command: desired.command,
    args: [...(desired.args || [])],
    env: { ...(desired.env || {}) },
  };
  if (desired.cwd) entry.cwd = desired.cwd;
  return entry;
}

/** Identity codec for mcpServers / servers / context_servers stdio shapes. */
export const STDIO_ENTRY_CODEC = Object.freeze({
  encode: encodeStdioEntry,
  /**
   * @param {unknown} raw
   * @returns {McpStdioOrUrlEntry|null}
   */
  decode(raw) {
    if (!raw || typeof raw !== "object" || Array.isArray(raw)) return null;
    const o = /** @type {Record<string, unknown>} */ (raw);
    if (typeof o.command !== "string") return null;
    return {
      command: o.command,
      args: Array.isArray(o.args) ? o.args.map(String) : [],
      env:
        o.env && typeof o.env === "object" && !Array.isArray(o.env)
          ? Object.fromEntries(
              Object.entries(/** @type {Record<string, unknown>} */ (o.env)).map(([k, v]) => [
                k,
                String(v),
              ]),
            )
          : {},
      cwd: typeof o.cwd === "string" ? o.cwd : undefined,
    };
  },
});

/**
 * Redact likely secrets for diagnostics (never log raw headers/API keys).
 * @param {McpStdioOrUrlEntry|null|undefined} entry
 */
export function redactMcpEntry(entry) {
  if (!entry) return null;
  /** @type {Record<string, unknown>} */
  const out = { ...entry };
  if (out.env && typeof out.env === "object") {
    /** @type {Record<string, string>} */
    const env = {};
    for (const [k, v] of Object.entries(/** @type {Record<string, string>} */ (out.env))) {
      env[k] = /key|token|secret|password|authorization/i.test(k) ? "***" : String(v);
    }
    out.env = env;
  }
  if (out.headers && typeof out.headers === "object") {
    /** @type {Record<string, string>} */
    const headers = {};
    for (const [k, v] of Object.entries(/** @type {Record<string, string>} */ (out.headers))) {
      headers[k] = "***";
    }
    out.headers = headers;
  }
  return out;
}

/**
 * @param {string} configPath
 * @param {{ rootKey?: string }} [opts]
 */
export function loadMcpConfig(configPath, opts = {}) {
  const rootKey = opts.rootKey || "mcpServers";
  if (!existsSync(configPath)) {
    return {
      path: configPath,
      rootKey,
      ok: true,
      exists: false,
      doc: { [rootKey]: {} },
      parseError: false,
    };
  }
  try {
    const raw = readFileSync(configPath, "utf8");
    const doc = JSON.parse(raw);
    if (!isPlainObject(doc)) {
      return {
        path: configPath,
        rootKey,
        ok: false,
        exists: true,
        doc: null,
        parseError: true,
        error: "config root is not an object",
      };
    }
    if (doc[rootKey] != null && !isPlainObject(doc[rootKey])) {
      return {
        path: configPath,
        rootKey,
        ok: false,
        exists: true,
        doc: null,
        parseError: true,
        error: `${rootKey} is not an object`,
      };
    }
    if (!doc[rootKey]) {
      doc[rootKey] = {};
    }
    return { path: configPath, rootKey, ok: true, exists: true, doc, parseError: false };
  } catch (err) {
    let jsonc = false;
    try {
      jsonc = looksLikeJsonc(readFileSync(configPath, "utf8"));
    } catch {
      /* keep jsonc false */
    }
    return {
      path: configPath,
      rootKey,
      ok: false,
      exists: true,
      doc: null,
      parseError: true,
      jsonc,
      error: jsonc
        ? "config contains comments (JSONC), which strict JSON writing would destroy"
        : err instanceof Error
          ? err.message
          : String(err),
    };
  }
}

/**
 * Cheap JSONC sniff: a `//` or block comment outside of string literals.
 * Used only to explain a parse failure honestly, never to enable writing.
 * @param {string} raw
 */
export function looksLikeJsonc(raw) {
  let inString = false;
  let escaped = false;
  for (let i = 0; i < raw.length; i += 1) {
    const ch = raw[i];
    if (inString) {
      if (escaped) escaped = false;
      else if (ch === "\\") escaped = true;
      else if (ch === '"') inString = false;
      continue;
    }
    if (ch === '"') {
      inString = true;
      continue;
    }
    if (ch === "/" && (raw[i + 1] === "/" || raw[i + 1] === "*")) return true;
  }
  return false;
}

/**
 * @param {ReturnType<typeof loadMcpConfig>} loaded
 * @param {string} serverName
 * @param {{ codec?: EntryCodec }} [opts]
 */
export function inspectManagedEntry(loaded, serverName, opts = {}) {
  if (!loaded.ok || !loaded.doc) {
    return {
      path: loaded.path,
      registered: false,
      entry: null,
      raw: null,
      parseError: loaded.parseError === true,
      error: loaded.error,
    };
  }
  const codec = opts.codec || STDIO_ENTRY_CODEC;
  const servers = /** @type {Record<string, unknown>} */ (loaded.doc[loaded.rootKey] || {});
  const raw = servers[serverName] ?? null;
  const entry = raw != null ? codec.decode(raw) : null;
  return {
    path: loaded.path,
    registered: Boolean(raw),
    entry,
    raw,
    parseError: false,
  };
}

/**
 * @param {McpStdioOrUrlEntry|null|undefined} a
 * @param {McpStdioOrUrlEntry} b
 */
export function mcpEntriesEqual(a, b) {
  if (!a) return false;
  if (normalizePathish(a.command || "") !== normalizePathish(b.command || "")) return false;
  if (!argsEqual(a.args || [], b.args || [])) return false;
  if (!envEqual(a.env || {}, b.env || {})) return false;
  if (normalizePathish(a.cwd || "") !== normalizePathish(b.cwd || "")) return false;
  return true;
}

/**
 * @param {{
 *   loaded: ReturnType<typeof loadMcpConfig>,
 *   serverName: string,
 *   desired: McpStdioOrUrlEntry,
 *   occamHome?: string,
 *   force?: boolean,
 *   codec?: EntryCodec,
 * }} opts
 */
export function planMcpMerge(opts) {
  const {
    loaded,
    serverName,
    desired,
    occamHome = "",
    force = false,
    codec = STDIO_ENTRY_CODEC,
  } = opts;
  if (!loaded.ok) {
    return {
      action: "refuse",
      reason: loaded.error || "malformed config",
      parseError: true,
    };
  }
  const current = inspectManagedEntry(loaded, serverName, { codec });
  const managed = looksLikeOccamManagedEntry(current.entry, occamHome);
  if (current.registered && mcpEntriesEqual(current.entry, desired)) {
    return {
      action: "noop",
      managed,
      current: current.entry,
      desired,
    };
  }
  if (current.registered && !managed && !force) {
    return {
      action: "skip-unmanaged",
      managed: false,
      current: current.entry,
      desired,
      reason: `Existing ${serverName} does not look Occam-managed; pass --force to overwrite`,
    };
  }
  return {
    action: current.registered ? "update" : "add",
    managed,
    current: current.entry,
    desired,
  };
}

/**
 * @param {string} configPath
 */
export function backupMcpConfig(configPath) {
  if (!existsSync(configPath)) {
    return { ok: true, backupPath: null, created: false };
  }
  const backupPath = `${configPath}.occam-bak`;
  copyFileSync(configPath, backupPath);
  return { ok: true, backupPath, created: true };
}

/**
 * Atomic write: temp → rename. Preserves sibling keys.
 * @param {string} configPath
 * @param {Record<string, unknown>} doc
 */
export function writeMcpConfigAtomic(configPath, doc) {
  const dir = dirname(configPath);
  mkdirSync(dir, { recursive: true });
  const tmp = join(dir, `.${Date.now()}-${Math.random().toString(16).slice(2)}.occam-tmp.json`);
  const body = `${JSON.stringify(doc, null, 2)}\n`;
  writeFileSync(tmp, body, "utf8");
  // Validate round-trip before replace.
  JSON.parse(readFileSync(tmp, "utf8"));
  renameSync(tmp, configPath);
  return { ok: true, path: configPath };
}

/**
 * Apply planned merge into a copy of the doc (does not write).
 * @param {ReturnType<typeof loadMcpConfig>} loaded
 * @param {string} serverName
 * @param {McpStdioOrUrlEntry} desired
 * @param {{ codec?: EntryCodec }} [opts]
 */
export function applyMergeToDoc(loaded, serverName, desired, opts = {}) {
  if (!loaded.ok || !loaded.doc) {
    throw new Error(loaded.error || "cannot merge into invalid config");
  }
  const codec = opts.codec || STDIO_ENTRY_CODEC;
  const doc = structuredClone(loaded.doc);
  const rootKey = loaded.rootKey;
  if (!isPlainObject(doc[rootKey])) {
    doc[rootKey] = {};
  }
  /** @type {Record<string, unknown>} */
  const servers = /** @type {Record<string, unknown>} */ (doc[rootKey]);
  servers[serverName] = codec.encode(desired);
  return doc;
}

/**
 * Remove managed server name from doc copy.
 * @param {ReturnType<typeof loadMcpConfig>} loaded
 * @param {string} serverName
 */
export function removeEntryFromDoc(loaded, serverName) {
  if (!loaded.ok || !loaded.doc) {
    throw new Error(loaded.error || "cannot remove from invalid config");
  }
  const doc = structuredClone(loaded.doc);
  const servers = /** @type {Record<string, unknown>} */ (doc[loaded.rootKey] || {});
  delete servers[serverName];
  doc[loaded.rootKey] = servers;
  return doc;
}

/**
 * Restore from backup path or previous raw snapshot string.
 * When previousMissing and we created a trivial single-root doc, remove the file.
 * Uses snap.rootKey (falls back to mcpServers only for legacy snapshots).
 * @param {string} configPath
 * @param {{
 *   backupPath?: string|null,
 *   previousRaw?: string|null,
 *   previousMissing?: boolean,
 *   rootKey?: string,
 * }} snap
 */
export function restoreMcpConfig(configPath, snap) {
  const rootKey = snap.rootKey || "mcpServers";
  if (snap.previousMissing) {
    if (snap.backupPath && existsSync(snap.backupPath)) {
      copyFileSync(snap.backupPath, configPath);
      return { ok: true, restored: "backup" };
    }
    if (snap.previousRaw != null) {
      writeFileSync(configPath, snap.previousRaw, "utf8");
      return { ok: true, restored: "raw" };
    }
    // File did not exist before our write — remove whatever we created.
    try {
      if (existsSync(configPath)) unlinkSync(configPath);
    } catch {
      /* leave file */
    }
    return { ok: true, restored: "absent" };
  }
  if (snap.backupPath && existsSync(snap.backupPath)) {
    copyFileSync(snap.backupPath, configPath);
    return { ok: true, restored: "backup" };
  }
  if (snap.previousRaw != null) {
    writeFileSync(configPath, snap.previousRaw, "utf8");
    return { ok: true, restored: "raw" };
  }
  return { ok: false, error: "no backup or previous snapshot to restore" };
}

/**
 * Full transaction helper for config-file adapters.
 * @param {{
 *   configPath: string,
 *   rootKey?: string,
 *   serverName: string,
 *   desired: McpStdioOrUrlEntry,
 *   occamHome?: string,
 *   force?: boolean,
 *   codec?: EntryCodec,
 * }} opts
 */
export function commitMcpRegistration(opts) {
  const rootKey = opts.rootKey || "mcpServers";
  const codec = opts.codec || STDIO_ENTRY_CODEC;
  const loaded = loadMcpConfig(opts.configPath, { rootKey });
  const plan = planMcpMerge({
    loaded,
    serverName: opts.serverName,
    desired: opts.desired,
    occamHome: opts.occamHome,
    force: opts.force,
    codec,
  });
  if (plan.action === "refuse") {
    return { ok: false, applied: false, action: "refuse", error: plan.reason, plan };
  }
  if (plan.action === "skip-unmanaged") {
    return {
      ok: false,
      applied: false,
      action: "skip-unmanaged",
      error: plan.reason,
      plan,
    };
  }
  if (plan.action === "noop") {
    return { ok: true, applied: false, action: "noop", plan };
  }

  const previousRaw = loaded.exists ? readFileSync(opts.configPath, "utf8") : null;
  const previousMissing = !loaded.exists;
  const backup = backupMcpConfig(opts.configPath);
  const nextDoc = applyMergeToDoc(loaded, opts.serverName, opts.desired, { codec });
  writeMcpConfigAtomic(opts.configPath, nextDoc);
  const after = loadMcpConfig(opts.configPath, { rootKey });
  const inspected = inspectManagedEntry(after, opts.serverName, { codec });
  return {
    ok: inspected.registered === true,
    applied: true,
    action: plan.action,
    plan,
    backupPath: backup.backupPath,
    previousRaw,
    previousMissing,
    rootKey,
    inspect: inspected,
  };
}

/**
 * @param {string} configPath
 * @param {{
 *   backupPath?: string|null,
 *   previousRaw?: string|null,
 *   previousMissing?: boolean,
 *   rootKey?: string,
 * }} snap
 */
export function rollbackMcpRegistration(configPath, snap) {
  return restoreMcpConfig(configPath, snap);
}
