/**
 * Fail-fast checks before starting the MCP host (agent-friendly install gate).
 */
import { execSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { resolveHostBinary } from "./resolve-host-binary.mjs";

/**
 * @returns {number} Major SDK version, or 0 if dotnet missing / unreadable.
 */
export function dotnetSdkMajor() {
  try {
    const version = execSync("dotnet --version", {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
    const major = Number.parseInt(version.split(".")[0], 10);
    return Number.isFinite(major) ? major : 0;
  } catch {
    return 0;
  }
}

/**
 * @param {string} root OCCAM_HOME
 */
export function isWorkersInstalled(root) {
  return existsSync(join(root, "workers", "http-extract", "node_modules"));
}

/**
 * @param {string} root
 * @returns {{ ready: boolean, binary: string | null, workers: boolean, dotnetMajor: number }}
 */
export function assessHostInstall(root) {
  const binary = resolveHostBinary(root);
  const workers = isWorkersInstalled(root);
  const dotnetMajor = dotnetSdkMajor();
  return {
    ready: Boolean(binary),
    binary,
    workers,
    dotnetMajor,
  };
}

/**
 * @param {string} root OCCAM_HOME
 * @param {{ prefix?: string }} [options]
 */
export function formatInstallBlockerMessage(root, options = {}) {
  const prefix = options.prefix ?? "[ff-occam]";
  const home = root.replace(/\\/g, "/");
  const wrapper = join(home, "scripts/occam-wrapper.sh").replace(/\\/g, "/");
  const launcher = join(home, "scripts/launch-mcp-host.mjs").replace(/\\/g, "/");
  const { workers, dotnetMajor } = assessHostInstall(root);

  /** @type {string[]} */
  const lines = [
    `${prefix} MCP host is not installed — refusing to start (no silent dotnet run fallback).`,
    `${prefix} OCCAM_HOME=${home}`,
    "",
  ];

  if (!workers) {
    lines.push(`${prefix} Missing: worker npm install (run occam-doctor).`);
  }
  lines.push(`${prefix} Missing: OccamMcp.Core AOT binary at repo root or publish path.`);
  if (dotnetMajor > 0 && dotnetMajor < 10) {
    lines.push(
      `${prefix} Found .NET SDK ${dotnetMajor}.x — this project requires .NET 10 (do not install .NET 8 for Occam).`,
    );
  } else if (dotnetMajor === 0) {
    lines.push(`${prefix} No .NET SDK on PATH — use release tarball (doctor --skip-build), not dotnet run.`);
  }

  lines.push(
    `${prefix} Do not edit FFOccamMcp.Core.csproj (must stay net10.0). Do not install .NET 8.`,
    "",
    "Fix (run once):",
    `  cd ${home}`,
    `  curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash`,
    "  # or with .NET 10 SDK: ./scripts/occam-doctor.sh (no --skip-build)",
    "",
    "Verify:",
    `  node ${join(home, "scripts/hermes-smoke.mjs").replace(/\\/g, "/")}`,
    "",
    "Wire MCP (Hermes) — do NOT use packages/occam-mcp/bin/occam-mcp.js on a git clone:",
    `  command: ${wrapper}`,
    `  env.OCCAM_HOME: ${home}`,
    "",
    "Other hosts:",
    `  command: node`,
    `  args: ["${launcher}"]`,
    `  env.OCCAM_HOME: ${home}`,
    "",
    "Read: INSTALL.md",
  );

  return lines.join("\n");
}

/**
 * @param {string} root
 * @returns {never}
 */
export function exitInstallBlocked(root) {
  console.error(formatInstallBlockerMessage(root));
  process.exit(1);
}

/**
 * @param {string} root OCCAM_HOME
 * @param {{ skipBuild?: boolean }} [options]
 */
export function formatDoctorBinaryMissing(root, options = {}) {
  const home = root.replace(/\\/g, "/");
  const getScript =
    process.env.OCCAM_GET_URL?.trim() ||
    "https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh";
  const { dotnetMajor } = assessHostInstall(root);

  /** @type {string[]} */
  const lines = [
    "error: OccamMcp.Core AOT binary not found — doctor cannot complete.",
    `OCCAM_HOME=${home}`,
    "",
  ];

  if (options.skipBuild) {
    lines.push(
      "You passed --skip-build but no prebuilt OccamMcp.Core is present.",
      "Git clone alone does NOT ship the MCP host binary.",
      "",
      "Hermes / production without .NET 10 SDK — use the release tarball:",
      `  curl -fsSL ${getScript} | bash`,
      "",
      "Alternatives:",
      "  • Install .NET 10 SDK, then ./scripts/occam-doctor.sh (no --skip-build)",
      "  • Copy OccamMcp.Core from a machine that ran dotnet publish",
      "",
    );
  } else {
    if (dotnetMajor > 0 && dotnetMajor < 10) {
      lines.push(
        `Found .NET SDK ${dotnetMajor}.x on PATH — cannot publish net10.0.`,
        "Do not downgrade csproj to net8.0. Install .NET 10 SDK or use get-ff-occam.sh.",
        "",
      );
    } else {
      lines.push("dotnet publish did not produce OccamMcp.Core (see errors above).", "");
    }
  }

  lines.push("See INSTALL.md");
  return lines.join("\n");
}

/**
 * @param {string} root OCCAM_HOME
 * @param {{ skipBuild?: boolean }} [options]
 * @returns {string} path to binary
 */
export function assertHostBinaryPresent(root, options = {}) {
  const binary = resolveHostBinary(root);
  if (binary) {
    return binary;
  }
  console.error(formatDoctorBinaryMissing(root, options));
  process.exit(1);
}
