#!/usr/bin/env node
/**
 * Selftest for occam CLI entry point.
 */
import assert from "node:assert/strict";
import { execSync } from "node:child_process";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const occamCli = join(scriptDir, "occam.mjs");

function execOccam(args = []) {
  try {
    const stdout = execSync(`node "${occamCli}" ${args.join(" ")}`, {
      encoding: "utf8",
      stdio: ["pipe", "pipe", "pipe"],
      env: { ...process.env, OCCAM_HOME: join(scriptDir, "..") },
    });
    return { stdout: stdout.trim(), exitCode: 0 };
  } catch (err) {
    return {
      stdout: err.stdout?.toString().trim() || "",
      stderr: err.stderr?.toString().trim() || "",
      exitCode: err.status || 1,
    };
  }
}

{
  const result = execOccam(["--version"]);
  assert.equal(result.exitCode, 0, "occam --version should exit 0");
  assert.match(result.stdout, /^\d+\.\d+\.\d+/, "occam --version should print semver");
}

{
  const result = execOccam(["-V"]);
  assert.equal(result.exitCode, 0, "occam -V should exit 0");
  assert.match(result.stdout, /^\d+\.\d+\.\d+/, "occam -V should print semver");
}

{
  const withVersion = execOccam(["--version"]);
  const withStatus = execOccam(["--json", "status"]);
  const statusData = JSON.parse(withStatus.stdout);
  assert.equal(withVersion.stdout, statusData.version, "--version and status should agree on version");
}

console.log("occam.selftest: OK");
