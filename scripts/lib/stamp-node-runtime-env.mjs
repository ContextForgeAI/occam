/**
 * Ensure the MCP host child can spawn Node workers when the GUI MCP host
 * strips Homebrew / nvm / system Node from PATH.
 *
 * Mutates `env` in place. Does not override an already-set OCCAM_NODE_BIN.
 *
 * @param {NodeJS.ProcessEnv} env
 * @param {string} execPath absolute path to the node that launched the script
 * @returns {NodeJS.ProcessEnv}
 */
export function stampNodeRuntimeEnv(env, execPath) {
  if (!env || typeof env !== "object") {
    throw new TypeError("stampNodeRuntimeEnv: env must be an object");
  }

  const nodeBin = typeof execPath === "string" ? execPath.trim() : "";
  if (!nodeBin) {
    return env;
  }

  if (!String(env.OCCAM_NODE_BIN || "").trim()) {
    env.OCCAM_NODE_BIN = nodeBin;
  }

  const nodeDir = dirnameOf(nodeBin);
  if (!nodeDir) {
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
  const normalized = filePath.replace(/\\/g, "/");
  const idx = normalized.lastIndexOf("/");
  if (idx <= 0) {
    return "";
  }
  const dir = normalized.slice(0, idx);
  return filePath.includes("\\") ? dir.replace(/\//g, "\\") : dir;
}

function pathDelimiter() {
  return process.platform === "win32" ? ";" : ":";
}

function samePath(a, b) {
  const norm = (p) => p.replace(/\\/g, "/").replace(/\/+$/, "").toLowerCase();
  return norm(a) === norm(b);
}
