#!/usr/bin/env node
/**
 * Runtime identifier for `dotnet publish -r <RID>` — keep in sync with INSTALL.md
 */
import { fileURLToPath } from "node:url";

/** RIDs that the public GitHub release workflow actually publishes. */
export const PUBLISHED_RELEASE_RIDS = Object.freeze(["win-x64", "linux-x64", "osx-arm64"]);

/** @param {string} rid */
export function isPublishedReleaseRid(rid) {
  return PUBLISHED_RELEASE_RIDS.includes(String(rid || ""));
}

/**
 * Runtime identifier for the public binary release channel.
 * Unlike resolveRid(), this must fail instead of inventing an unpublished asset.
 */
export function resolvePublishedRid(platform = process.platform, arch = process.arch) {
  const normalizedArch = String(arch || "").toLowerCase();
  if (platform === "win32" && ["x64", "amd64", "x86_64"].includes(normalizedArch)) {
    return "win-x64";
  }
  if (platform === "linux" && ["x64", "amd64", "x86_64"].includes(normalizedArch)) {
    return "linux-x64";
  }
  if (platform === "darwin" && ["arm64", "aarch64"].includes(normalizedArch)) {
    return "osx-arm64";
  }
  throw new Error(
    `unsupported public release platform: ${platform}/${arch} ` +
      `(published RIDs: ${PUBLISHED_RELEASE_RIDS.join(", ")})`,
  );
}

export function resolveRid(platform = process.platform, arch = process.arch) {
  if (platform === "win32") {
    return arch === "arm64" ? "win-arm64" : "win-x64";
  }
  if (platform === "darwin") {
    return arch === "arm64" ? "osx-arm64" : "osx-x64";
  }
  if (platform === "linux") {
    return arch === "arm64" ? "linux-arm64" : "linux-x64";
  }
  throw new Error(`unsupported platform: ${platform}/${arch}`);
}

const __filename = fileURLToPath(import.meta.url);
if (process.argv[1] === __filename) {
  console.log(resolveRid());
}
