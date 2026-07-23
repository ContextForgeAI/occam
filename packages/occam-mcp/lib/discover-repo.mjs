import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

/** True when `dir` looks like an FFOccamMCP install root (Level A clone or Level B tarball). */
export function isOccamRepoRoot(dir) {
  const root = resolve(dir);
  return (
    existsSync(join(root, "workers", "http-extract", "extract.mjs")) &&
    existsSync(join(root, "scripts", "launch-mcp-host.mjs"))
  );
}

/**
 * Walk parents from `startDir` looking for an Occam install root.
 * Returns null when this tree is not a git clone / tarball (e.g. global npx cache).
 */
export function discoverRepoRoot(startDir = process.cwd()) {
  let dir = resolve(startDir);
  for (let depth = 0; depth < 8; depth++) {
    if (isOccamRepoRoot(dir)) {
      return dir;
    }
    const parent = dirname(dir);
    if (parent === dir) {
      break;
    }
    dir = parent;
  }
  return null;
}

/**
 * Try several starting directories (package dir, cwd, script path, OCCAM_HOME).
 * Returns unique install roots, most specific first.
 * @param {string[]} startDirs
 */
export function findInstallRoots(startDirs) {
  /** @type {string[]} */
  const roots = [];
  const seen = new Set();
  for (const start of startDirs) {
    if (!start) continue;
    const root = discoverRepoRoot(start);
    if (root && !seen.has(root)) {
      seen.add(root);
      roots.push(root);
    }
  }
  return roots;
}
