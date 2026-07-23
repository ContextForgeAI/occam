import { mkdirSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";
import { buildOnboardFilePayload, loadOnboardConfig } from "./onboard-schema.mjs";

/**
 * Load onboard env keys from ~/.occam/onboard.json or OCCAM_CONFIG.
 * @returns {Record<string, string>}
 */
export function loadOnboardEnv() {
  return loadOnboardConfig().env;
}

/**
 * Merge onboard env into process env — explicit host env wins.
 * @param {NodeJS.ProcessEnv} base
 */
export function mergeOnboardEnv(base) {
  const onboard = loadOnboardEnv();
  /** @type {Record<string, string>} */
  const merged = { ...onboard };

  for (const [key, value] of Object.entries(base)) {
    if (typeof value === "string" && value.length > 0) {
      merged[key] = value;
    }
  }

  return merged;
}

/**
 * @param {ReturnType<import("./onboard-flow.mjs").buildOnboardResult>} result
 */
export function writeOnboardConfig(result) {
  const path = result.configPath;
  mkdirSync(dirname(path), { recursive: true });
  const payload = buildOnboardFilePayload(result);
  writeFileSync(path, `${JSON.stringify(payload, null, 2)}\n`, "utf8");
}
