#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const occamHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
const occamCli = join(scriptDir, "occam.mjs");

/**
 * @param {string[]} args
 */
function runOccamCli(args) {
  return spawnSync(process.execPath, [occamCli, ...args], {
    cwd: occamHome,
    env: { ...process.env, OCCAM_HOME: occamHome },
    encoding: "utf8",
  });
}

function testVersionFlag() {
  console.log("Testing --version flag...");
  const result = runOccamCli(["--version"]);
  
  if (result.status !== 0) {
    throw new Error(`--version exited with code ${result.status}, expected 0`);
  }

  const output = result.stdout.trim();
  if (!output) {
    throw new Error("--version produced no output");
  }

  if (output.includes("Usage:") || output.includes("Commands:")) {
    throw new Error(`--version printed usage instead of version: ${output}`);
  }

  console.log(`  ✓ --version exits 0 and prints: ${output}`);
}

function testVersionShortFlag() {
  console.log("Testing -V flag...");
  const result = runOccamCli(["-V"]);
  
  if (result.status !== 0) {
    throw new Error(`-V exited with code ${result.status}, expected 0`);
  }

  const output = result.stdout.trim();
  if (!output) {
    throw new Error("-V produced no output");
  }

  if (output.includes("Usage:") || output.includes("Commands:")) {
    throw new Error(`-V printed usage instead of version: ${output}`);
  }

  console.log(`  ✓ -V exits 0 and prints: ${output}`);
}

function testVersionJson() {
  console.log("Testing --json --version...");
  const result = runOccamCli(["--json", "--version"]);
  
  if (result.status !== 0) {
    throw new Error(`--json --version exited with code ${result.status}, expected 0`);
  }

  const output = result.stdout.trim();
  let parsed;
  try {
    parsed = JSON.parse(output);
  } catch {
    throw new Error(`--json --version did not produce valid JSON: ${output}`);
  }

  if (!parsed.version) {
    throw new Error(`--json --version missing version field: ${output}`);
  }

  console.log(`  ✓ --json --version exits 0 and returns JSON with version: ${parsed.version}`);
}

function testVersionConsistency() {
  console.log("Testing version consistency with status...");
  
  const versionResult = runOccamCli(["--version"]);
  const statusResult = runOccamCli(["--json", "status"]);
  
  if (versionResult.status !== 0 || statusResult.status !== 0) {
    throw new Error("Failed to run --version or status commands");
  }

  const versionOutput = versionResult.stdout.trim();
  const statusData = JSON.parse(statusResult.stdout.trim());
  
  if (versionOutput !== statusData.version) {
    throw new Error(
      `Version mismatch: --version="${versionOutput}", status.version="${statusData.version}"`
    );
  }

  console.log(`  ✓ --version matches status version: ${versionOutput}`);
}

function main() {
  console.log("occam CLI selftest\n");
  
  try {
    testVersionFlag();
    testVersionShortFlag();
    testVersionJson();
    testVersionConsistency();
    
    console.log("\nAll tests passed ✓");
    process.exit(0);
  } catch (err) {
    console.error(`\n✗ Test failed: ${err.message}`);
    process.exit(1);
  }
}

main();
