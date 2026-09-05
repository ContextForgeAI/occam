#!/usr/bin/env node
/**
 * ff-occam — primary npm MCP launcher (command name: ff-occam only).
 * Delegates to @ff-occam/mcp launcher (release download or OCCAM_HOME local build).
 *
 * Operator verbs (connect, doctor, …) are NOT supported here — refuse before
 * spawning the MCP host so they do not look like a silent hang / junk argv.
 */
import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const __dirname = dirname(fileURLToPath(import.meta.url));

function resolveMcpEntry() {
  try {
    return require.resolve("@ff-occam/mcp/bin/occam-mcp.js");
  } catch {
    // Monorepo: packages/ff-occam/bin → packages/occam-mcp
    return join(__dirname, "..", "..", "occam-mcp", "bin", "occam-mcp.js");
  }
}

function resolveOperatorGuard() {
  try {
    return require.resolve("@ff-occam/mcp/lib/operator-cli-guard.mjs");
  } catch {
    return join(__dirname, "..", "..", "occam-mcp", "lib", "operator-cli-guard.mjs");
  }
}

const { refuseOperatorCliVerbOrContinue } = await import(
  pathToFileURL(resolveOperatorGuard()).href,
);
refuseOperatorCliVerbOrContinue(process.argv.slice(2), { prefix: "[ff-occam]" });

const entry = resolveMcpEntry();
const child = spawn(process.execPath, [entry, ...process.argv.slice(2)], {
  stdio: "inherit",
  env: process.env,
  windowsHide: true,
});

child.on("exit", (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }
  process.exit(code ?? 0);
});

child.on("error", (err) => {
  console.error(`[ff-occam] Failed to start MCP host: ${err.message}`);
  process.exit(1);
});
