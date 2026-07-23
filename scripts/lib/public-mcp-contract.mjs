#!/usr/bin/env node
/**
 * Public MCP contract helpers — canonicalize tools/list and SHA-256 fingerprint.
 * Shared by check-public-mcp-contract.mjs (stdio / WS / tunnel launch path).
 */
import { createHash } from "node:crypto";

/** @param {unknown} typeEl */
export function typeToken(typeEl) {
  if (typeEl == null) return "any";
  if (typeof typeEl === "string") return typeEl;
  if (Array.isArray(typeEl)) return typeEl.map((x) => (typeof x === "string" ? x : "?")).join("|");
  return JSON.stringify(typeEl);
}

/** @param {unknown} def */
export function defaultToken(def) {
  if (def === undefined) return null;
  if (def === null) return "null";
  if (typeof def === "boolean" || typeof def === "number" || typeof def === "string") {
    return String(def);
  }
  return JSON.stringify(def);
}

/**
 * Canonical form used for fingerprinting — names + required + property types/defaults.
 * Descriptions are excluded so copy edits do not invalidate the corpus.
 * @param {{ name: string, inputSchema?: Record<string, unknown> }[]} tools
 */
export function canonicalizeToolsList(tools) {
  return [...tools]
    .map((t) => {
      const schema = t.inputSchema && typeof t.inputSchema === "object" ? t.inputSchema : {};
      const required = Array.isArray(schema.required)
        ? [...schema.required].filter((x) => typeof x === "string").sort()
        : [];
      const properties = {};
      const props = schema.properties && typeof schema.properties === "object" ? schema.properties : {};
      for (const name of Object.keys(props).sort()) {
        const p = props[name] || {};
        properties[name] = {
          type: typeToken(p.type),
          default: defaultToken(p.default),
        };
      }
      return { name: t.name, required, properties };
    })
    .sort((a, b) => a.name.localeCompare(b.name));
}

/** @param {{ name: string, inputSchema?: Record<string, unknown> }[]} tools */
export function schemaFingerprint(tools) {
  const canonical = JSON.stringify(canonicalizeToolsList(tools));
  return createHash("sha256").update(canonical).digest("hex");
}

/**
 * RC1 public contract assertions over a live tools/list payload.
 * @param {{ name: string, inputSchema?: Record<string, unknown> }[]} tools
 * @returns {{ ok: boolean, failures: string[] }}
 */
export function assertPublicMcpContract(tools) {
  const failures = [];
  const byName = Object.fromEntries(tools.map((t) => [t.name, t]));

  const digest = byName.occam_digest;
  if (!digest) {
    failures.push("occam_digest missing from tools/list");
  } else {
    const schema = digest.inputSchema || {};
    const required = Array.isArray(schema.required) ? schema.required : [];
    if (required.includes("urls")) {
      failures.push("occam_digest: urls must not be in required[]");
    }
    const props = schema.properties || {};
    if (!props.source_url) failures.push("occam_digest: source_url property missing");
    if (!props.urls) failures.push("occam_digest: urls property missing");
    if (!props.max_links) failures.push("occam_digest: max_links property missing");
    else if (props.max_links.default !== 8) {
      failures.push(`occam_digest: max_links default must be 8 (got ${JSON.stringify(props.max_links.default)})`);
    }
  }

  const transcode = byName.occam_transcode;
  if (!transcode) {
    failures.push("occam_transcode missing from tools/list");
  } else {
    const schema = transcode.inputSchema || {};
    const required = Array.isArray(schema.required) ? schema.required : [];
    if (!(required.length === 1 && required[0] === "url")) {
      failures.push(`occam_transcode: required must be ["url"] (got ${JSON.stringify(required)})`);
    }
    const props = schema.properties || {};
    if (props.auto_recover) {
      failures.push("occam_transcode: auto_recover must be absent");
    }
    for (const name of [
      "rank_blocks",
      "tag_trust",
      "delta_only",
      "emit_capsule",
      "json_blocks",
      "semantic_chunking",
      "diff_against",
      "if_none_match",
      "cache_ttl_s",
    ]) {
      if (!props[name]) failures.push(`occam_transcode: missing ${name}`);
    }
    for (const name of [
      "rank_blocks",
      "tag_trust",
      "delta_only",
      "emit_capsule",
      "json_blocks",
      "semantic_chunking",
    ]) {
      if (props[name] && props[name].default !== false) {
        failures.push(`occam_transcode: ${name} default must be false`);
      }
    }
  }

  return { ok: failures.length === 0, failures };
}

export const TRANSCODE_RC1_PARAMS = [
  "rank_blocks",
  "tag_trust",
  "delta_only",
  "emit_capsule",
  "json_blocks",
  "semantic_chunking",
  "diff_against",
  "if_none_match",
  "cache_ttl_s",
];
