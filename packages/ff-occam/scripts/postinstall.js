#!/usr/bin/env node
/**
 * Honest postinstall for the experimental npm MCP launcher.
 * Does not install the operator CLI and never fails the npm install.
 */
import { join } from "node:path";

function globalBinDir() {
  const prefix = process.env.npm_config_prefix;
  if (!prefix) return null;
  return process.platform === "win32" ? prefix : join(prefix, "bin");
}

function pathHas(dir) {
  if (!dir) return true;
  const sep = process.platform === "win32" ? ";" : ":";
  const norm = (value) => value.replace(/[/\\]+$/, "").toLowerCase();
  const target = norm(dir);
  return (process.env.PATH || "").split(sep).some((part) => norm(part) === target);
}

if (process.env.OCCAM_QUIET_POSTINSTALL === "1") {
  process.exit(0);
}

const lines = [
  "",
  "[ff-occam] This npm package starts the MCP host only (experimental).",
  "[ff-occam] It does not install the `occam` operator CLI (connect, doctor, …).",
  "",
  "Full install (host + PATH + connect):",
  "  Windows: irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex",
  "  Linux / macOS: curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash",
  "",
  "MCP-only trial (no operator CLI): npx ff-occam@1.0.1",
];

const bin = globalBinDir();
if (bin) {
  lines.push("", `[ff-occam] Global bin: ${bin}  (command: ff-occam)`);
  if (!pathHas(bin)) {
    lines.push("[ff-occam] That directory is not on PATH in this shell.");
    lines.push("[ff-occam] Use `npx ff-occam@1.0.1`, or add the directory and open a new terminal.");
  }
}

lines.push("");
console.error(lines.join("\n"));
