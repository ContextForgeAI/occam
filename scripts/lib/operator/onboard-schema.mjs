import { existsSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export const SUPPORTED_ONBOARD_SCHEMA = "1.0";
export const HELP_SCHEMA_VERSION = "1.0";
export const UI_CONTRACT_VERSION = "1.0";
export const HOST_MANIFEST_SCHEMA_VERSION = "1.0";

const moduleDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(moduleDir, "..", "..", "..");

/** @returns {string} */
export function defaultOnboardPath() {
  const override = process.env.OCCAM_CONFIG?.trim();
  if (override) {
    return override;
  }

  return join(homedir(), ".occam", "onboard.json");
}

/** @param {string} [root] */
export function readOccamVersion(root = repoRoot) {
  try {
    const text = readFileSync(join(root, "CHANGELOG.md"), "utf8");
    const matches = [...text.matchAll(/^## \[([^\]]+)\]/gm)];
    for (const match of matches) {
      if (match[1] !== "Unreleased") {
        return match[1];
      }
    }
  } catch {
    // fall through
  }

  return "unknown";
}

/**
 * @param {unknown} value
 * @returns {{ major: number, minor: number } | null}
 */
export function parseSchemaVersion(value) {
  if (typeof value === "number" && Number.isInteger(value)) {
    return { major: value, minor: 0 };
  }

  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  const match = /^(\d+)(?:\.(\d+))?$/.exec(trimmed);
  if (!match) {
    return null;
  }

  return {
    major: Number.parseInt(match[1], 10),
    minor: Number.parseInt(match[2] ?? "0", 10),
  };
}

/**
 * @param {{ major: number, minor: number }} supported
 * @param {{ major: number, minor: number }} file
 */
export function compareSchemaToSupported(supported, file) {
  if (file.major !== supported.major) {
    return "major_mismatch";
  }

  if (file.minor > supported.minor) {
    return "newer_minor";
  }

  return "ok";
}

/**
 * @param {string} code
 * @param {string} message
 */
export function warnOnboardSchema(code, message) {
  console.error(`[occam] ${code}: ${message}`);
}

/**
 * @param {unknown} parsed
 * @returns {string | null}
 */
function resolveFileSchemaVersion(parsed) {
  if (!parsed || typeof parsed !== "object") {
    return null;
  }

  const record = /** @type {Record<string, unknown>} */ (parsed);
  if (typeof record.schema_version === "string") {
    return record.schema_version;
  }

  if (typeof record.version === "number") {
    return `${record.version}.0`;
  }

  if (typeof record.version === "string") {
    return record.version;
  }

  return null;
}

/**
 * @param {unknown} parsed
 * @returns {Record<string, string>}
 */
function extractEnv(parsed) {
  if (!parsed || typeof parsed !== "object") {
    return {};
  }

  const env = /** @type {Record<string, unknown>} */ (parsed).env;
  if (!env || typeof env !== "object") {
    return {};
  }

  /** @type {Record<string, string>} */
  const out = {};
  for (const [key, value] of Object.entries(env)) {
    if (typeof value === "string" && value.length > 0) {
      out[key] = value;
    }
  }

  return out;
}

/**
 * @param {string} [path]
 * @returns {{ env: Record<string, string>, warnings: string[], ignored: boolean }}
 */
export function loadOnboardConfig(path = defaultOnboardPath()) {
  if (!existsSync(path)) {
    return { env: {}, warnings: [], ignored: false };
  }

  let parsed;
  try {
    parsed = JSON.parse(readFileSync(path, "utf8"));
  } catch {
    warnOnboardSchema("onboard_parse_error", `could not parse ${path} — using defaults`);
    return { env: {}, warnings: ["onboard_parse_error"], ignored: true };
  }

  const fileSchema = resolveFileSchemaVersion(parsed);
  const supported = parseSchemaVersion(SUPPORTED_ONBOARD_SCHEMA);
  if (!fileSchema || !supported) {
    warnOnboardSchema("onboard_schema_invalid", `missing schema_version in ${path} — using defaults`);
    return { env: {}, warnings: ["onboard_schema_invalid"], ignored: true };
  }

  const fileVersion = parseSchemaVersion(fileSchema);
  if (!fileVersion) {
    warnOnboardSchema("onboard_schema_invalid", `invalid schema_version in ${path} — using defaults`);
    return { env: {}, warnings: ["onboard_schema_invalid"], ignored: true };
  }

  const comparison = compareSchemaToSupported(supported, fileVersion);
  if (comparison === "major_mismatch") {
    warnOnboardSchema(
      "onboard_schema_unsupported",
      `schema_version ${fileSchema} not supported (expected major ${supported.major}) — ignoring ${path}`,
    );
    return { env: {}, warnings: ["onboard_schema_unsupported"], ignored: true };
  }

  /** @type {string[]} */
  const warnings = [];
  if (comparison === "newer_minor") {
    warnOnboardSchema(
      "onboard_schema_newer",
      `schema_version ${fileSchema} is newer than supported ${SUPPORTED_ONBOARD_SCHEMA} — applying known fields`,
    );
    warnings.push("onboard_schema_newer");
  }

  return { env: extractEnv(parsed), warnings, ignored: false };
}

/**
 * @param {ReturnType<import("./onboard-flow.mjs").buildOnboardResult>} result
 */
export function buildOnboardFilePayload(result) {
  return {
    schema_version: SUPPORTED_ONBOARD_SCHEMA,
    profile: result.profile,
    hostTarget: result.hostTarget,
    occamHome: result.occamHome,
    env: result.env,
    written_at: new Date().toISOString(),
    generator: readOccamVersion(),
  };
}
