#!/usr/bin/env node
/**
 * Runtime identifier for `dotnet publish -r <RID>` — keep in sync with INSTALL.md
 */
import { fileURLToPath } from "node:url";

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
