#!/usr/bin/env node
/**
 * Shared prerequisite checks for install.sh / install.ps1 / verify-install.mjs.
 * Exit 0 on success; stderr + exit 1 on failure.
 */
import { execSync } from "node:child_process";

const MIN_NODE_MAJOR = 20;
const MIN_DOTNET_MAJOR = 10;

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

function checkNode() {
  let version;
  try {
    version = process.versions.node;
  } catch {
    fail("node not available");
  }
  const major = Number.parseInt(version.split(".")[0], 10);
  if (!Number.isFinite(major) || major < MIN_NODE_MAJOR) {
    fail(`Node.js ${MIN_NODE_MAJOR}+ required (found ${version})`);
  }
  console.log(`node: ${version}`);
}

function checkDotnet() {
  let version;
  try {
    version = execSync("dotnet --version", { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
  } catch {
    fail(".NET SDK not found on PATH");
  }
  const major = Number.parseInt(version.split(".")[0], 10);
  if (!Number.isFinite(major) || major < MIN_DOTNET_MAJOR) {
    fail(`.NET SDK ${MIN_DOTNET_MAJOR}+ required (found ${version})`);
  }
  console.log(`dotnet: ${version}`);
}

function checkGit() {
  try {
    const version = execSync("git --version", { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
    console.log(version);
  } catch {
    fail("git not found on PATH");
  }
}

function checkRelease() {
  checkNode();
}

function main() {
  const only = process.argv[2];
  if (!only || only === "all") {
    checkGit();
    checkNode();
    checkDotnet();
    return;
  }
  if (only === "release") {
    checkRelease();
    return;
  }
  if (only === "node") {
    checkNode();
    return;
  }
  if (only === "dotnet") {
    checkDotnet();
    return;
  }
  if (only === "git") {
    checkGit();
    return;
  }
  fail(`unknown check: ${only} (use all|release|git|node|dotnet)`);
}

main();
