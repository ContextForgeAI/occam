#!/usr/bin/env node
/**
 * Cross-platform MCP host launcher for Cursor / Hermes / generic stdio.
 * Prefers AOT publish output (same path as occam-doctor); no silent dotnet run fallback.
 *
 * PR-G: stamps OCCAM_RUNTIME_ID / OCCAM_SESSION_ID for identity diagnostics and forwards
 * termination signals to the exact child only (never a process-name-wide kill).
 */
import { randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  dotnetSdkMajor,
  exitInstallBlocked,
  formatInstallBlockerMessage,
} from "./lib/host-install-gate.mjs";
import { resolveHostBinary } from "./lib/resolve-host-binary.mjs";
import { mergeOnboardEnv } from "./lib/operator/onboard-config.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const hostBinary = resolveHostBinary(root);
const project = join(root, "src", "FFOccamMcp.Core", "FFOccamMcp.Core.csproj");

const runtimeId = process.env.OCCAM_RUNTIME_ID?.trim() || `rt-${randomUUID().replaceAll("-", "")}`;
const sessionId = process.env.OCCAM_SESSION_ID?.trim() || `sess-${randomUUID().replaceAll("-", "")}`;
const env = mergeOnboardEnv({
  ...process.env,
  OCCAM_HOME: root,
  OCCAM_RUNTIME_ID: runtimeId,
  OCCAM_SESSION_ID: sessionId,
  OCCAM_PARENT_PID: String(process.pid),
  OCCAM_PARENT_LABEL: process.env.OCCAM_PARENT_LABEL?.trim() || "launch-mcp-host",
});
const spawnOpts = { stdio: "inherit", env, cwd: root };

function runChild(command, args) {
  const child = spawn(command, args, spawnOpts);
  let shuttingDown = false;

  const forward = (signal) => {
    if (shuttingDown) {
      return;
    }
    shuttingDown = true;
    if (child.pid) {
      try {
        process.kill(child.pid, signal);
      } catch {
        // child already gone
      }
    }
  };

  for (const signal of ["SIGINT", "SIGTERM", "SIGHUP"]) {
    try {
      process.on(signal, () => forward(signal));
    } catch {
      // Windows may not support every signal name
    }
  }

  child.on("error", (err) => {
    console.error(`[ff-occam] failed to start MCP host: ${err.message}`);
    process.exit(1);
  });
  child.on("exit", (code, signal) => {
    if (signal) {
      process.kill(process.pid, signal);
      return;
    }
    process.exit(code ?? 1);
  });
}

if (hostBinary) {
  runChild(hostBinary, []);
} else if (process.env.OCCAM_FORCE_DOTNET_RUN === "1" && existsSync(project)) {
  const major = dotnetSdkMajor();
  if (major < 10) {
    console.error(
      `[ff-occam] OCCAM_FORCE_DOTNET_RUN=1 but .NET SDK ${major || "missing"}.x found — need .NET 10.\n` +
        formatInstallBlockerMessage(root),
    );
    process.exit(1);
  }
  console.error("[ff-occam] OCCAM_FORCE_DOTNET_RUN=1 — using dotnet run (dev only).");
  runChild("dotnet", ["run", "--project", project, "-c", "Release", "--no-launch-profile"]);
} else if (existsSync(project)) {
  exitInstallBlocked(root);
} else {
  console.error(
    `[ff-occam] MCP host binary not found and no SDK project at ${project}\n` +
      formatInstallBlockerMessage(root),
  );
  process.exit(1);
}
