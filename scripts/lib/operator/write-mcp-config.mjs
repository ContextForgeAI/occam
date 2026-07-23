/**
 * Optional merge-write of MCP host config (--write-config on occam-onboard).
 * Never overwrites entire host config — merges ff-occam entry only.
 */
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { buildConnectionSnippet } from "./onboard-flow.mjs";

/**
 * @param {string} configPath
 */
function cursorConfigPath(configPath) {
  if (configPath) {
    return configPath;
  }
  return path.join(os.homedir(), ".cursor", "mcp.json");
}

/**
 * @param {string} filePath
 * @param {object} mcpConfig
 */
function mergeCursorConfig(filePath, mcpConfig) {
  /** @type {{ mcpServers?: Record<string, unknown> }} */
  let existing = { mcpServers: {} };
  if (fs.existsSync(filePath)) {
    try {
      existing = JSON.parse(fs.readFileSync(filePath, "utf8"));
    } catch {
      throw new Error(`invalid JSON at ${filePath}`);
    }
  }
  if (!existing.mcpServers || typeof existing.mcpServers !== "object") {
    existing.mcpServers = {};
  }
  const entry = mcpConfig.mcpServers?.["ff-occam"];
  if (!entry) {
    throw new Error("no ff-occam entry in snippet");
  }
  existing.mcpServers["ff-occam"] = entry;
  return existing;
}

/**
 * @param {string} prompt
 */
async function confirmWrite(prompt) {
  if (!process.stdin.isTTY) {
    console.error("error: --write-config requires a TTY for confirmation (or use --force)");
    process.exit(2);
  }
  const rl = createInterface({ input, output });
  try {
    const answer = await rl.question(`${prompt}\nType YES to write: `);
    return answer.trim() === "YES";
  } finally {
    rl.close();
  }
}

/**
 * @param {ReturnType<import("./onboard-flow.mjs").buildOnboardResult>} result
 * @param {{ force?: boolean, cursorConfigPath?: string }} options
 */
export async function writeHostConfig(result, options = {}) {
  const snippet = buildConnectionSnippet(result);
  const kind = snippet.connectionKind;

  if (kind === "cli-only") {
    console.log("cli-only — no host config written.");
    return { written: false, path: null };
  }

  if (kind === "hermes") {
    const hermesPath = path.join(os.homedir(), ".hermes", "config.yaml");
    console.log("\nHermes YAML (merge manually into ~/.hermes/config.yaml):\n");
    console.log(snippet.hermesYaml ?? "");
    if (!options.force) {
      console.log("\nHermes YAML merge is manual — automatic write not enabled for YAML v1.");
    }
    return { written: false, path: hermesPath, manual: true };
  }

  if (!snippet.mcpConfig) {
    throw new Error("no JSON snippet to write");
  }

  const target = cursorConfigPath(options.cursorConfigPath ?? "");
  const merged = mergeCursorConfig(target, snippet.mcpConfig);

  console.log("\nPreview (merged ff-occam entry):\n");
  console.log(JSON.stringify(merged, null, 2));

  if (!options.force) {
    const ok = await confirmWrite(`Write merged config to ${target}?`);
    if (!ok) {
      console.log("Write cancelled.");
      return { written: false, path: target };
    }
  }

  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, `${JSON.stringify(merged, null, 2)}\n`, "utf8");
  console.log(`Wrote: ${target}`);
  return { written: true, path: target };
}
