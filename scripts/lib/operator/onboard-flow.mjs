import { buildSnippetFromOnboardResult, buildMcpSnippet } from "./mcp-snippet.mjs";

export { SUPPORTED_ONBOARD_SCHEMA as ONBOARD_SCHEMA_VERSION, defaultOnboardPath } from "./onboard-schema.mjs";
export { applyBrowserChoice, applyProfile, applyProxyChoice, normalizeAnswers } from "./onboard-steps.mjs";

import { defaultOnboardPath, SUPPORTED_ONBOARD_SCHEMA } from "./onboard-schema.mjs";
import { applyBrowserChoice, applyProfile, applyProxyChoice, normalizeAnswers } from "./onboard-steps.mjs";

/**
 * @param {ReturnType<typeof normalizeAnswers>} normalized
 */
export function buildOnboardResult(normalized) {
  /** @type {Record<string, string>} */
  const env = {
    OCCAM_HOME: normalized.occamHome.replace(/\\/g, "/"),
    ...applyProfile(normalized.profile),
    ...applyBrowserChoice(normalized.browser),
    ...applyProxyChoice(normalized.useProxy),
  };

  if (normalized.hostTarget === "hermes" || normalized.profile === "hermes-headless") {
    env.OCCAM_BANNER = "0";
    env.WT_OCCAM_BANNER = "0";
  }

  return {
    schema_version: SUPPORTED_ONBOARD_SCHEMA,
    profile: normalized.profile,
    hostTarget: normalized.hostTarget,
    occamHome: normalized.occamHome,
    env,
    configPath: defaultOnboardPath(),
    skipped: false,
  };
}

/**
 * @param {Record<string, string>} answers
 */
export function runFlow(answers) {
  const normalized = normalizeAnswers(answers);
  if (!normalized.occamHome) {
    throw new Error("OCCAM_HOME is required");
  }

  return buildOnboardResult(normalized);
}

/**
 * @param {ReturnType<typeof buildOnboardResult>} result
 * @param {{ workspace?: boolean }} [options]
 */
export function buildMcpSnippetConfig(result, options = {}) {
  const snippet = buildSnippetFromOnboardResult(result, options);
  if (snippet.mcpConfig) {
    return snippet.mcpConfig;
  }
  if (snippet.mcpServers) {
    return { mcpServers: snippet.mcpServers };
  }
  return { mcpServers: {} };
}

/**
 * Full snippet payload for renderers (JSON or Hermes YAML).
 * @param {ReturnType<typeof buildOnboardResult>} result
 * @param {{ workspace?: boolean }} [options]
 */
export function buildConnectionSnippet(result, options = {}) {
  return buildSnippetFromOnboardResult(result, options);
}

/**
 * @param {string} occamHome
 * @param {import("./mcp-snippet.mjs").ConnectionKind} connectionKind
 * @param {Record<string, string>} [env]
 */
export function buildSnippetForKind(occamHome, connectionKind, env = {}) {
  return buildMcpSnippet({ occamHome, connectionKind, env });
}
