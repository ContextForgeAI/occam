#!/usr/bin/env node
/**
 * Default Playwright browser cache paths — keep in sync with
 * src/FFOccamMcp.Core/Workers/PlaywrightEnvironment.cs
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export function resolveDefaultBrowsersPath() {
  const occamOverride = process.env.OCCAM_PLAYWRIGHT_BROWSERS_PATH?.trim();
  if (occamOverride) {
    const full = path.resolve(occamOverride);
    return fs.existsSync(full) ? full : null;
  }

  const playwrightEnv = process.env.PLAYWRIGHT_BROWSERS_PATH?.trim();
  if (playwrightEnv) {
    const full = path.resolve(playwrightEnv);
    return fs.existsSync(full) ? full : null;
  }

  if (process.platform === "win32") {
    const localAppData = process.env.LOCALAPPDATA;
    return localAppData ? path.join(localAppData, "ms-playwright") : null;
  }

  const home = process.env.HOME;
  if (!home) {
    return null;
  }

  const candidate =
    process.platform === "darwin"
      ? path.join(home, "Library", "Caches", "ms-playwright")
      : path.join(home, ".cache", "ms-playwright");

  return candidate;
}

function main() {
  const command = process.argv[2] ?? "path";
  const cacheRoot = resolveDefaultBrowsersPath();

  switch (command) {
    case "path":
      if (cacheRoot) {
        console.log(cacheRoot);
      }
      break;
    default:
      console.error(`Usage: node playwright-cache.mjs [path]`);
      process.exit(2);
  }
}

const isMain =
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main();
}
