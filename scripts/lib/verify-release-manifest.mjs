#!/usr/bin/env node
/**
 * Verify that one downloaded release manifest describes one exact archive.
 *
 * This is a local consistency/integrity check only. It does not establish build
 * provenance or prove that either file came from a trusted build.
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { parseSemanticVersion } from "./release-version.mjs";
import { isPublishedReleaseRid } from "./resolve-rid.mjs";

function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  hash.update(fs.readFileSync(filePath));
  return hash.digest("hex");
}

/**
 * @param {{ version: string, rid: string, outputDir: string }} options
 * @returns {{ actualSha: string, manifest: Record<string, unknown>, manifestPath: string, tarballPath: string }}
 */
export function verifyReleaseManifest({ version, rid, outputDir }) {
  parseSemanticVersion(version);
  if (!isPublishedReleaseRid(rid)) {
    throw new Error(`unsupported RID: ${rid}`);
  }

  const resolvedOutputDir = path.resolve(outputDir);
  const stageName = `ff-occam-${version}-${rid}`;
  const tarballName = `${stageName}.tar.gz`;
  const tarballPath = path.join(resolvedOutputDir, tarballName);
  const manifestPath = path.join(resolvedOutputDir, `${stageName}-manifest.json`);

  if (!fs.existsSync(tarballPath)) {
    throw new Error(`tarball not found: ${tarballPath}`);
  }
  if (!fs.existsSync(manifestPath)) {
    throw new Error(`manifest not found: ${manifestPath}`);
  }

  let manifest;
  try {
    manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  } catch (error) {
    throw new Error(
      `invalid release manifest JSON: ${error instanceof Error ? error.message : String(error)}`,
    );
  }
  if (!manifest || typeof manifest !== "object" || Array.isArray(manifest)) {
    throw new Error("release manifest must be a JSON object");
  }
  if (manifest.version !== version) {
    throw new Error(`manifest version mismatch (expected ${version}, got ${String(manifest.version)})`);
  }
  if (manifest.rid !== rid) {
    throw new Error(`manifest RID mismatch (expected ${rid}, got ${String(manifest.rid)})`);
  }
  if (manifest.tarball !== tarballName) {
    throw new Error(`manifest tarball mismatch (expected ${tarballName}, got ${String(manifest.tarball)})`);
  }
  if (typeof manifest.sha256 !== "string" || !/^[0-9a-f]{64}$/.test(manifest.sha256)) {
    throw new Error("manifest sha256 must be 64 lowercase hexadecimal characters");
  }

  const actualSha = sha256File(tarballPath);
  if (actualSha !== manifest.sha256) {
    throw new Error(`sha256 mismatch (manifest=${manifest.sha256} actual=${actualSha})`);
  }

  return { actualSha, manifest, manifestPath, tarballPath };
}

function parseArgs(argv) {
  const options = {};
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--version") {
      options.version = argv[++index];
    } else if (arg === "--rid") {
      options.rid = argv[++index];
    } else if (arg === "--output-dir") {
      options.outputDir = argv[++index];
    } else if (arg === "-h" || arg === "--help") {
      console.log(
        "usage: node verify-release-manifest.mjs --version VER --rid RID --output-dir DIR",
      );
      process.exit(0);
    } else {
      throw new Error(`unknown argument: ${arg}`);
    }
  }
  if (!options.version || !options.rid || !options.outputDir) {
    throw new Error("--version, --rid, and --output-dir are required");
  }
  return options;
}

function main() {
  try {
    const options = parseArgs(process.argv.slice(2));
    const result = verifyReleaseManifest(options);
    console.log(`verify-release-manifest: version=${options.version} rid=${options.rid}`);
    console.log(`verify-release-manifest: tarball=${result.tarballPath}`);
    console.log(`verify-release-manifest: sha256=${result.actualSha}`);
    console.log("verify-release-manifest: OK");
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
