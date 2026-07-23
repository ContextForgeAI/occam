#!/usr/bin/env node
/**
 * Lightweight release artifact check (CI) — no Playwright / doctor.
 * Verifies external manifest sha256 and tarball layout.
 */
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync, execSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const SUPPORTED_RIDS = new Set(["win-x64", "linux-x64", "osx-arm64", "osx-x64"]);

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../..");

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

function parseArgs(argv) {
  /** @type {{ rid?: string; version?: string; outputDir?: string }} */
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--rid") {
      out.rid = argv[++i];
    } else if (arg === "--version") {
      out.version = argv[++i];
    } else if (arg === "--output-dir") {
      out.outputDir = argv[++i];
    } else if (arg === "-h" || arg === "--help") {
      console.log(`usage: node verify-release-artifact.mjs --rid <rid> [--version VER] [--output-dir DIR]`);
      process.exit(0);
    } else {
      fail(`unknown argument: ${arg}`);
    }
  }
  return out;
}

function readLatestReleasedVersion() {
  const changelog = path.join(repoRoot, "CHANGELOG.md");
  const text = fs.readFileSync(changelog, "utf8");
  const matches = [...text.matchAll(/^## \[([^\]]+)\]/gm)];
  for (const match of matches) {
    if (match[1] !== "Unreleased") {
      return match[1];
    }
  }
  fail("could not read version from CHANGELOG.md — pass --version");
}

function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  hash.update(fs.readFileSync(filePath));
  return hash.digest("hex");
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const rid = args.rid;
  if (!rid) {
    fail("--rid is required");
  }
  if (!SUPPORTED_RIDS.has(rid)) {
    fail(`unsupported RID: ${rid}`);
  }

  const version = args.version ?? readLatestReleasedVersion();
  const outputDir = path.resolve(args.outputDir ?? path.join(repoRoot, "artifacts", "releases"));
  const stageName = `ff-occam-${version}-${rid}`;
  const tarballPath = path.join(outputDir, `${stageName}.tar.gz`);
  const manifestPath = path.join(outputDir, `${stageName}-manifest.json`);

  if (!fs.existsSync(tarballPath)) {
    fail(`tarball not found: ${tarballPath}`);
  }
  if (!fs.existsSync(manifestPath)) {
    fail(`manifest not found: ${manifestPath}`);
  }

  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  if (manifest.version !== version || manifest.rid !== rid) {
    fail(`manifest version/rid mismatch (expected ${version}/${rid})`);
  }

  const actualSha = sha256File(tarballPath);
  if (actualSha !== manifest.sha256) {
    fail(`sha256 mismatch (manifest=${manifest.sha256} actual=${actualSha})`);
  }

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "ff-occam-verify-"));
  try {
    execSync(`tar -xzf "${tarballPath}" -C "${tmp}"`, { stdio: "pipe" });
    const extractRoot = path.join(tmp, stageName);
    const exeCandidates = rid.startsWith("win-")
      ? ["OccamMcp.Core.exe", "FFOccamMcp.Core.exe"]
      : ["OccamMcp.Core", "FFOccamMcp.Core"];
    const hostBinary = exeCandidates
      .map((name) => path.join(extractRoot, name))
      .find((p) => fs.existsSync(p));
    if (!hostBinary) {
      fail(`missing host binary in tarball (tried ${exeCandidates.join(", ")})`);
    }

    const innerManifest = path.join(extractRoot, "release-manifest.json");
    if (!fs.existsSync(innerManifest)) {
      fail("missing release-manifest.json in tarball");
    }
    const inner = JSON.parse(fs.readFileSync(innerManifest, "utf8"));
    if (inner.version !== version || inner.rid !== rid) {
      fail("inner release-manifest.json version/rid mismatch");
    }

    const versionFile = path.join(extractRoot, "VERSION");
    if (!fs.existsSync(versionFile)) {
      fail("missing VERSION in tarball");
    }
    const packagedVersion = fs.readFileSync(versionFile, "utf8").trim();
    if (packagedVersion !== version) {
      fail(`VERSION mismatch (expected ${version}, got ${packagedVersion})`);
    }

    let hostVersion;
    try {
      const versionSurface = JSON.parse(
        execFileSync(hostBinary, ["version-surface"], {
          encoding: "utf8",
          stdio: ["ignore", "pipe", "pipe"],
        }),
      );
      hostVersion = versionSurface.hostVersion;
      if (hostVersion !== version || versionSurface.packageVersion !== version) {
        fail(
          `host version mismatch (expected ${version}, host=${hostVersion}, package=${versionSurface.packageVersion})`,
        );
      }
    } catch (error) {
      if (error instanceof SyntaxError) {
        fail(`host version-surface returned invalid JSON: ${error.message}`);
      }
      throw error;
    }

    const workersPkg = path.join(extractRoot, "workers", "package.json");
    if (!fs.existsSync(workersPkg)) {
      fail("missing workers/package.json in tarball");
    }

    const launchScript = path.join(extractRoot, "scripts", "launch-mcp-host.mjs");
    if (!fs.existsSync(launchScript)) {
      fail("missing scripts/launch-mcp-host.mjs in tarball");
    }

    const sizeBytes = fs.statSync(tarballPath).size;
    console.log(`verify-release-artifact: version=${version} rid=${rid}`);
    console.log(`verify-release-artifact: hostVersion=${hostVersion}`);
    console.log(`verify-release-artifact: tarball=${tarballPath}`);
    console.log(`verify-release-artifact: sha256=${actualSha}`);
    console.log(`verify-release-artifact: sizeBytes=${sizeBytes}`);
    console.log("verify-release-artifact: OK");
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

main();
