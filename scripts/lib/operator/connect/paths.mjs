/**
 * Shared helpers for desktop config path resolution (product-agnostic).
 */
import { existsSync, readdirSync } from "node:fs";
import { homedir, platform } from "node:os";
import { join } from "node:path";

/**
 * @returns {string}
 */
export function appDataDir() {
  if (platform() === "win32") {
    return process.env.APPDATA?.trim() || join(homedir(), "AppData", "Roaming");
  }
  if (platform() === "darwin") {
    return join(homedir(), "Library", "Application Support");
  }
  return process.env.XDG_CONFIG_HOME?.trim() || join(homedir(), ".config");
}

/**
 * @returns {string}
 */
export function localAppDataDir() {
  if (platform() === "win32") {
    return process.env.LOCALAPPDATA?.trim() || join(homedir(), "AppData", "Local");
  }
  return appDataDir();
}

/**
 * True only when the directory exists and is not empty.
 * Leftover empty app folders must not count as "installed".
 * @param {string} dir
 */
export function dirHasEntries(dir) {
  if (!dir || !existsSync(dir)) return false;
  try {
    return readdirSync(dir).length > 0;
  } catch {
    return false;
  }
}

/**
 * Pick a single active config path among candidates.
 * @param {string[]} candidates
 * @returns {{
 *   path: string|null,
 *   ambiguous: boolean,
 *   candidates: string[],
 *   existing: string[],
 * }}
 */
export function resolveUniqueConfigPath(candidates) {
  const unique = [...new Set(candidates.filter(Boolean))];
  const existing = unique.filter((p) => existsSync(p));
  if (existing.length === 1) {
    return { path: existing[0], ambiguous: false, candidates: unique, existing };
  }
  if (existing.length > 1) {
    return { path: null, ambiguous: true, candidates: unique, existing };
  }
  // Prefer first candidate when none exist yet (safe default write target).
  return {
    path: unique[0] || null,
    ambiguous: false,
    candidates: unique,
    existing,
  };
}

/**
 * Enumerate Windows MSIX Claude package config candidates.
 * @returns {string[]}
 */
export function listWindowsClaudeMsixConfigs() {
  if (platform() !== "win32") return [];
  const packages = join(localAppDataDir(), "Packages");
  if (!existsSync(packages)) return [];
  /** @type {string[]} */
  const out = [];
  try {
    for (const name of readdirSync(packages)) {
      if (!/^Claude_/i.test(name)) continue;
      out.push(
        join(packages, name, "LocalCache", "Roaming", "Claude", "claude_desktop_config.json"),
      );
    }
  } catch {
    /* ignore */
  }
  return out;
}
