#!/usr/bin/env node
/**
 * ff-occam — primary npm CLI (aliases: ff-occam, occam).
 * Delegates to @ff-occam/mcp launcher (release download or OCCAM_HOME local build).
 */
import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const require = createRequire(import.meta.url);
const __dirname = dirname(fileURLToPath(import.meta.url));

function resolveMcpEntry() {
  try {
    return require.resolve("@ff-occam/mcp/bin/occam-mcp.js");
  } catch {
    // Monorepo dev: sibling package before npm link/publish.
    const sibling = join(__dirname, "..", "occam-mcp", "bin", "occam-mcp.js");
    return sibling;
  }
}

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
