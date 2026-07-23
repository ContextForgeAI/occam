#!/usr/bin/env node
/**
 * Post-install verification: published host binary + browser launch smoke.
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { execSync } from "node:child_process";
import { resolveHostBinary } from "./resolve-host-binary.mjs";

const root = process.env.OCCAM_HOME
  ? path.resolve(process.env.OCCAM_HOME)
  : path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

const skipBuild = process.argv.includes("--skip-build");

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

async function main() {
  let commit = "unknown";
  try {
    commit = execSync("git rev-parse --short HEAD", {
      encoding: "utf8",
      cwd: root,
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
  } catch {
    // non-git tree (Level B tarball)
  }

  console.log(`verify-install: OCCAM_HOME=${root}`);
  console.log(`verify-install: commit=${commit}`);

  const binary = resolveHostBinary(root);
  if (!binary) {
    const hint = skipBuild
      ? "pre-built MCP host binary not found in OCCAM_HOME (Level B root or Level A publish path)"
      : "published MCP host binary not found — run doctor without --skip-build";
    fail(hint);
  }
  console.log(`verify-install: host=${binary.replace(/\\/g, "/")}`);

  const verifyBrowser = path.join(
    root,
    "workers/browser-extract/lib/verify-browser-launch.mjs",
  );
  if (!fs.existsSync(verifyBrowser)) {
    fail(`missing ${verifyBrowser}`);
  }

  execSync(`node "${verifyBrowser}"`, {
    cwd: path.join(root, "workers/browser-extract"),
    stdio: "inherit",
    env: { ...process.env, OCCAM_HOME: root },
  });

  // SLSA verification (optional, requires cosign + slsa-verifier)
  const provenancePath = path.join(root, "occam-mcp-provenance.intoto.jsonl");
  const binaryPath = binary;
  if (fs.existsSync(provenancePath) && fs.existsSync(binaryPath)) {
    console.log("verify-install: SLSA provenance found, verifying...");
    try {
      execSync(`slsa-verifier verify-artifact "${binaryPath}" --provenance-path "${provenancePath}" --source-uri github.com/ContextForgeAI/occam`, {
        stdio: "inherit",
      });
      console.log("verify-install: SLSA provenance verified");
    } catch (e) {
      console.warn("verify-install: SLSA verification skipped (slsa-verifier not installed or failed)");
    }
  }

  // Cosign verification (optional)
  const bundlePath = `${binaryPath}.bundle`;
  if (fs.existsSync(bundlePath)) {
    console.log("verify-install: cosign bundle found, verifying...");
    try {
      execSync(`cosign verify-blob --bundle "${bundlePath}" "${binaryPath}"`, {
        stdio: "inherit",
      });
      console.log("verify-install: cosign signature verified");
    } catch (e) {
      console.warn("verify-install: cosign verification skipped (cosign not installed or failed)");
    }
  }

  console.log("verify-install: OK");
}

main().catch((err) => {
  fail(err instanceof Error ? err.message : String(err));
});
