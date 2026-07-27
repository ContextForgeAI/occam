/**
 * Canonical Node executable resolution for Occam install + MCP host + workers.
 *
 * Precedence (first existing wins):
 *   1. OCCAM_NODE_BIN (explicit operator override)
 *   2. {OCCAM_HOME}/runtime/node-bin (install-recorded absolute path)
 *   3. process.execPath / caller-supplied execPath (JS launcher that is already Node)
 *   4. well-known platform install locations
 *   5. bare "node" / "node.exe" (PATH fallback — not GUI-PATH safe)
 *
 * Mutating helpers stamp OCCAM_NODE_BIN + prepend Node's directory onto PATH so
 * AOT Core workers inherit a usable runtime under restricted GUI PATH.
 */
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { spawnSync } from "node:child_process";

export const RUNTIME_NODE_BIN_REL = join("runtime", "node-bin");

/**
 * @param {string} occamHome
 */
export function installNodeBinPath(occamHome) {
  const home = String(occamHome || "").trim();
  if (!home) return "";
  return join(home, RUNTIME_NODE_BIN_REL);
}

/**
 * @param {string} occamHome
 * @returns {string} absolute path or ""
 */
export function readInstallNodeBin(occamHome) {
  const file = installNodeBinPath(occamHome);
  if (!file || !existsSync(file)) return "";
  try {
    const line = readFileSync(file, "utf8")
      .split(/\r?\n/)
      .map((s) => s.trim())
      .find((s) => s && !s.startsWith("#"));
    return line || "";
  } catch {
    return "";
  }
}

/**
 * Persist the Node used at install/connect time for later GUI-PATH-safe launches.
 * @param {string} occamHome
 * @param {string} [nodeBin]
 * @returns {{ path: string, recorded: string }}
 */
export function writeInstallNodeBin(occamHome, nodeBin = process.execPath) {
  const home = String(occamHome || "").trim();
  const recorded = String(nodeBin || "").trim();
  if (!home || !recorded) {
    throw new Error("writeInstallNodeBin requires occamHome and nodeBin");
  }
  const file = installNodeBinPath(home);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, `${recorded}\n`, "utf8");
  return { path: file, recorded };
}

/**
 * @returns {string[]}
 */
export function wellKnownNodePaths() {
  if (process.platform === "darwin") {
    return ["/opt/homebrew/bin/node", "/usr/local/bin/node"];
  }
  if (process.platform === "linux") {
    return ["/usr/bin/node", "/usr/local/bin/node"];
  }
  if (process.platform === "win32") {
    const pf = process.env.ProgramFiles || "C:\\Program Files";
    const pf86 = process.env["ProgramFiles(x86)"] || "C:\\Program Files (x86)";
    return [join(pf, "nodejs", "node.exe"), join(pf86, "nodejs", "node.exe")];
  }
  return [];
}

/**
 * @param {string} candidate
 */
export function isUsableNodeBinary(candidate) {
  const path = String(candidate || "").trim();
  if (!path) return false;
  if (path === "node" || path === "node.exe") return true;
  return existsSync(path);
}

/**
 * @param {{
 *   env?: NodeJS.ProcessEnv,
 *   occamHome?: string,
 *   execPath?: string,
 *   requireExisting?: boolean,
 * }} [opts]
 * @returns {string}
 */
export function resolveNodeExecutable(opts = {}) {
  const env = opts.env || process.env;
  const requireExisting = opts.requireExisting !== false;

  const override = String(env.OCCAM_NODE_BIN || "").trim();
  if (override) {
    if (!requireExisting || existsSync(override)) return override;
  }

  const home = String(opts.occamHome || env.OCCAM_HOME || "").trim();
  if (home) {
    const recorded = readInstallNodeBin(home);
    if (recorded && (!requireExisting || existsSync(recorded))) return recorded;

    const bundled = join(home, "bin", process.platform === "win32" ? "node.exe" : "node");
    if (existsSync(bundled)) return bundled;
  }

  const execPath = String(opts.execPath || "").trim();
  if (execPath && (!requireExisting || existsSync(execPath))) {
    // Only treat as Node when the basename looks like node (avoid AOT host path mistakes).
    const base = execPath.replace(/\\/g, "/").split("/").pop()?.toLowerCase() || "";
    if (base === "node" || base === "node.exe") return execPath;
  }

  for (const candidate of wellKnownNodePaths()) {
    if (existsSync(candidate)) return candidate;
  }

  return process.platform === "win32" ? "node.exe" : "node";
}

/**
 * Clear, human error when a recorded/override Node path disappeared.
 * @param {string} nodeBin
 * @param {{ source?: string }} [meta]
 */
export function formatMissingNodeMessage(nodeBin, meta = {}) {
  const path = String(nodeBin || "").trim() || "(unknown)";
  const source = meta.source ? ` (${meta.source})` : "";
  return [
    `Occam's Node runtime is no longer available at:${source}`,
    `  ${path}`,
    "",
    "Reinstall Occam, or set OCCAM_NODE_BIN to a working Node 20+ executable.",
  ].join("\n");
}

/**
 * One-shot startup validation (not per tool call).
 * @param {string} nodeBin
 * @param {{ minMajor?: number }} [opts]
 * @returns {{ ok: true, version: string } | { ok: false, error: string }}
 */
export function validateNodeExecutable(nodeBin, opts = {}) {
  const minMajor = opts.minMajor ?? 20;
  const bin = String(nodeBin || "").trim();
  if (!bin) {
    return { ok: false, error: formatMissingNodeMessage("(empty)") };
  }
  if (bin !== "node" && bin !== "node.exe" && !existsSync(bin)) {
    return { ok: false, error: formatMissingNodeMessage(bin) };
  }

  const r = spawnSync(bin, ["-p", "process.versions.node"], {
    encoding: "utf8",
    timeout: 8_000,
    windowsHide: true,
  });
  if (r.error || r.status !== 0) {
    const detail = r.error?.message || (r.stderr || r.stdout || "").trim() || `exit ${r.status}`;
    return {
      ok: false,
      error: `${formatMissingNodeMessage(bin)}\n\nNode probe failed: ${detail}`,
    };
  }
  const version = String(r.stdout || "").trim();
  const major = Number.parseInt(version.split(".")[0] || "", 10);
  if (!Number.isFinite(major) || major < minMajor) {
    return {
      ok: false,
      error: `Occam requires Node.js ${minMajor}+ (found ${version || "unknown"} at ${bin}).`,
    };
  }
  return { ok: true, version };
}

/**
 * Stamp OCCAM_NODE_BIN + PATH for Core workers. Does not override an explicit OCCAM_NODE_BIN.
 * @param {NodeJS.ProcessEnv} env
 * @param {{ occamHome?: string, execPath?: string }} [opts]
 */
export function stampNodeRuntimeEnv(env, opts = {}) {
  if (!env || typeof env !== "object") {
    throw new TypeError("stampNodeRuntimeEnv: env must be an object");
  }

  // Back-compat: second arg used to be a bare execPath string.
  const options = typeof opts === "string" ? { execPath: opts } : opts || {};
  const resolved = resolveNodeExecutable({
    env,
    occamHome: options.occamHome || env.OCCAM_HOME,
    execPath: options.execPath || process.execPath,
    requireExisting: true,
  });

  if (!String(env.OCCAM_NODE_BIN || "").trim()) {
    if (resolved && resolved !== "node" && resolved !== "node.exe") {
      env.OCCAM_NODE_BIN = resolved;
    } else if (options.execPath && existsSync(options.execPath)) {
      env.OCCAM_NODE_BIN = options.execPath;
    }
  }

  const nodeBin = String(env.OCCAM_NODE_BIN || resolved || "").trim();
  const nodeDir = dirnameOf(nodeBin);
  if (!nodeDir || nodeBin === "node" || nodeBin === "node.exe") {
    return env;
  }

  const pathKey = Object.keys(env).find((k) => k.toLowerCase() === "path") || "PATH";
  const current = String(env[pathKey] || "");
  const parts = current.split(pathDelimiter()).filter(Boolean);
  if (!parts.some((p) => samePath(p, nodeDir))) {
    env[pathKey] = parts.length ? `${nodeDir}${pathDelimiter()}${current}` : nodeDir;
  }

  return env;
}

function dirnameOf(filePath) {
  const normalized = String(filePath || "").replace(/\\/g, "/");
  const idx = normalized.lastIndexOf("/");
  if (idx <= 0) return "";
  const dir = normalized.slice(0, idx);
  return String(filePath).includes("\\") ? dir.replace(/\//g, "\\") : dir;
}

function pathDelimiter() {
  return process.platform === "win32" ? ";" : ":";
}

function samePath(a, b) {
  const norm = (p) => String(p).replace(/\\/g, "/").replace(/\/+$/, "").toLowerCase();
  return norm(a) === norm(b);
}
