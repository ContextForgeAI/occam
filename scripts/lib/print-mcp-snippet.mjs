#!/usr/bin/env node
/**
 * Emit Cursor MCP JSON for a given OCCAM_HOME (stable paths, valid JSON).
 */
import { buildMcpSnippet } from "./operator/mcp-snippet.mjs";

const installDir = process.argv[2];
if (!installDir) {
  console.error("usage: node print-mcp-snippet.mjs <OCCAM_HOME>");
  process.exit(2);
}

const home = installDir;
/** @type {Record<string, string>} */
const env = { OCCAM_HOME: home.replace(/\\/g, "/") };

const channel = process.env.OCCAM_BROWSER_CHANNEL?.trim();
if (channel) {
  env.OCCAM_BROWSER_CHANNEL = channel;
}
const executablePath =
  process.env.OCCAM_BROWSER_EXECUTABLE_PATH?.trim() ||
  process.env.OCCAM_CHROME_PATH?.trim();
if (executablePath) {
  env.OCCAM_BROWSER_EXECUTABLE_PATH = executablePath.replace(/\\/g, "/");
}

const snippet = buildMcpSnippet({
  occamHome: home,
  connectionKind: "cursor-global",
  env,
});

if (!snippet.mcpConfig) {
  console.error("error: no snippet generated");
  process.exit(1);
}

console.log(JSON.stringify(snippet.mcpConfig, null, 2));
