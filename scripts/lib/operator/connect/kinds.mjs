/**
 * Host / runtime taxonomy for Occam auto-connect.
 * @typedef {'MCP_HOST'|'AI_AGENT'|'IDE_EXTENSION'|'MODEL_RUNTIME'|'REMOTE_AGENT'|'UNSUPPORTED'} ProductKind
 * @typedef {'NATIVE_CLI'|'CONFIG_FILE'|'ASSISTED'|'MANUAL_ONLY'|'DETECT_ONLY'} ConnectionMethod
 * @typedef {'A'|'B'|'C'|'D'} SupportTier
 * @typedef {'high'|'medium'|'low'} Confidence
 */

export const PRODUCT_KINDS = Object.freeze([
  "MCP_HOST",
  "AI_AGENT",
  "IDE_EXTENSION",
  "MODEL_RUNTIME",
  "REMOTE_AGENT",
  "UNSUPPORTED",
]);

export const CONNECTION_METHODS = Object.freeze([
  "NATIVE_CLI",
  "CONFIG_FILE",
  "ASSISTED",
  "MANUAL_ONLY",
  "DETECT_ONLY",
]);

/** Canonical managed MCP registration name owned by Occam. */
export const OCCAM_MCP_SERVER_NAME = "ff-occam";

/** Env key written into managed stdio registrations (Wave 1 ownership signal). */
export const OCCAM_MANAGED_ENV_KEY = "OCCAM_CONNECT_MANAGED";

/** Marker written into managed metadata where hosts allow custom fields. */
export const OCCAM_MANAGED_MARKER = "occam-managed:v1";
