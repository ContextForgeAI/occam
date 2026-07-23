import { renderHelpJson, renderOnboardJson } from "./json-renderer.mjs";
import { renderOnboardComplete } from "./onboard-tty-renderer.mjs";
import { renderHelpPlain } from "./plain-renderer.mjs";
import { renderHelpTty } from "./tty-renderer.mjs";

/** @typedef {'tty'|'json'|'plain'} RenderFormat */

/** @param {RenderFormat} format */
export function selectRenderer(format) {
  switch (format) {
    case "json":
      return { renderHelp: renderHelpJson, renderOnboard: renderOnboardJson };
    case "plain":
      return { renderHelp: renderHelpPlain, renderOnboard: renderOnboardPlain };
    case "tty":
    default:
      return { renderHelp: renderHelpTty, renderOnboard: renderOnboardTty };
  }
}

/** @param {ReturnType<import("../onboard-flow.mjs").buildOnboardResult>} result */
function renderOnboardPlain(result, mcpConfig) {
  const lines = [
    "FF-Occam onboard complete",
    `profile: ${result.profile}`,
    `host: ${result.hostTarget}`,
    `config: ${result.configPath}`,
    "",
    "MCP snippet:",
    JSON.stringify(mcpConfig, null, 2),
  ];
  return lines.join("\n");
}

/** @param {ReturnType<import("../onboard-flow.mjs").buildOnboardResult>} result */
function renderOnboardTty(result, mcpConfig) {
  return renderOnboardComplete(result, mcpConfig);
}
