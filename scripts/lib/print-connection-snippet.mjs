#!/usr/bin/env node
/**
 * Print connection snippet after install (used by get-ff-occam.sh).
 *
 *   node print-connection-snippet.mjs <OCCAM_HOME> <hostTarget|connectionKind>
 */
import { buildMcpSnippet, hostTargetToConnectionKind } from "./operator/mcp-snippet.mjs";
import { applyProfile } from "./operator/onboard-steps.mjs";

const home = process.argv[2];
const target = process.argv[3] ?? "hermes";

if (!home) {
  console.error("usage: node print-connection-snippet.mjs <OCCAM_HOME> [hostTarget]");
  process.exit(2);
}

const connectionKind = target.includes("-")
  ? /** @type {import("./operator/mcp-snippet.mjs").ConnectionKind} */ (target)
  : hostTargetToConnectionKind(
      /** @type {import("./operator/onboard-steps.mjs").HostTarget} */ (target),
    );

const profile = connectionKind === "hermes" ? "hermes-headless" : "default";
const env = {
  OCCAM_HOME: home.replace(/\\/g, "/"),
  ...applyProfile(profile),
};

const snippet = buildMcpSnippet({ occamHome: home, connectionKind, env });

if (snippet.format === "yaml" && snippet.hermesYaml) {
  console.log(snippet.hermesYaml);
} else if (snippet.mcpConfig) {
  console.log(JSON.stringify(snippet.mcpConfig, null, 2));
} else if (snippet.message) {
  console.log(snippet.message);
}
