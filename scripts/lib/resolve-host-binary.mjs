#!/usr/bin/env node
/**
 * Resolve the published Native AOT MCP host binary under OCCAM_HOME.
 *
 * Self-contained on purpose: this file ships inside the Level B release tarball
 * (scripts/lib/), which does NOT include packages/. It must not import from
 * ../../packages/ — doing so crashes launch-mcp-host.mjs on a tarball install
 * (ERR_MODULE_NOT_FOUND) and the MCP host never starts. Keep the candidate list
 * in sync with packages/occam-mcp/lib/resolve-host-binary.mjs.
 *
 * Accepts both OccamMcp.Core (dotnet publish) and FFOccamMcp.Core (legacy tarballs).
 */
import { existsSync } from "node:fs";
import { join } from "node:path";
import { resolveRid } from "./resolve-rid.mjs";

const BASE_NAMES = ["OccamMcp.Core", "FFOccamMcp.Core"];

function withExe(baseName) {
  return process.platform === "win32" ? `${baseName}.exe` : baseName;
}

/**
 * @param {string} root OCCAM_HOME
 * @param {string} [rid] linux-x64, win-x64, …
 * @returns {string[]}
 */
export function listHostBinaryCandidates(root, rid) {
  const names = BASE_NAMES.map(withExe);
  /** @type {string[]} */
  const paths = [];

  // Level B tarball layout: binary at the OCCAM_HOME root.
  for (const name of names) {
    paths.push(join(root, name));
  }

  // dotnet publish layouts (git clone + build).
  if (rid) {
    const publishDir = join(root, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish");
    for (const name of names) {
      paths.push(join(publishDir, name));
    }
  }
  const flatPublish = join(root, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", "publish");
  for (const name of names) {
    paths.push(join(flatPublish, name));
  }

  return paths;
}

/**
 * @param {string} root OCCAM_HOME
 * @param {string} [rid]
 * @returns {string | null}
 */
export function resolveHostBinary(root, rid) {
  let effectiveRid = rid;
  if (effectiveRid === undefined) {
    try {
      effectiveRid = resolveRid();
    } catch {
      effectiveRid = undefined;
    }
  }
  for (const candidate of listHostBinaryCandidates(root, effectiveRid)) {
    if (existsSync(candidate)) {
      return candidate;
    }
  }
  return null;
}
