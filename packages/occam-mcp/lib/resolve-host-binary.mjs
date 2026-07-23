/**
 * Resolve the published Native AOT MCP host binary under OCCAM_HOME.
 * Accepts both OccamMcp.Core (dotnet publish) and FFOccamMcp.Core (legacy tarballs).
 */
import { existsSync } from "node:fs";
import { join } from "node:path";

const BASE_NAMES = ["OccamMcp.Core", "FFOccamMcp.Core"];

/**
 * Resolve the .NET runtime identifier for the current Node platform.
 * @param {NodeJS.Platform} [platform]
 * @param {string} [arch]
 * @returns {string}
 */
export function resolveRid(platform = process.platform, arch = process.arch) {
  if (platform === "win32") return arch === "arm64" ? "win-arm64" : "win-x64";
  if (platform === "darwin") return arch === "arm64" ? "osx-arm64" : "osx-x64";
  if (platform === "linux") return arch === "arm64" ? "linux-arm64" : "linux-x64";
  throw new Error(`unsupported platform: ${platform}/${arch}`);
}

/**
 * @param {string} [rid]
 * @returns {string[]}
 */
export function hostBinaryBaseNames(rid) {
  return [...BASE_NAMES];
}

/**
 * @param {string} baseName
 * @returns {string}
 */
function withExe(baseName) {
  return process.platform === "win32" ? `${baseName}.exe` : baseName;
}

/**
 * @param {string} root OCCAM_HOME
 * @param {string} [rid] linux-x64, win-x64, …
 * @returns {string[]}
 */
export function listHostBinaryCandidates(root, rid) {
  const names = hostBinaryBaseNames(rid).map(withExe);
  /** @type {string[]} */
  const paths = [];

  for (const name of names) {
    paths.push(join(root, name));
  }

  if (rid) {
    const publishDir = join(
      root,
      "src",
      "FFOccamMcp.Core",
      "bin",
      "Release",
      "net10.0",
      rid,
      "publish",
    );
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
  for (const candidate of listHostBinaryCandidates(root, rid)) {
    if (existsSync(candidate)) {
      return candidate;
    }
  }
  return null;
}
