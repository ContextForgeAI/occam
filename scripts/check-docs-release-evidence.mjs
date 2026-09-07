#!/usr/bin/env node
/**
 * G1: every public demonstration names its host build.
 * publicBuild=true is reserved for the published GitHub Release identity.
 */
import { existsSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const PUBLIC_TOOLCHAIN = "ff-occam/1.0.0";

/**
 * @param {string} [root]
 * @returns {string[]}
 */
export function checkReleaseEvidence(root = repoRoot) {
  const errors = [];
  const path = join(root, "docs/examples/release-evidence.json");
  if (!existsSync(path)) {
    return [`${rel(root, path)}: missing release-evidence ledger`];
  }

  let ledger;
  try {
    ledger = JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    return [`${rel(root, path)}: invalid JSON (${error instanceof Error ? error.message : error})`];
  }

  if (ledger.schema !== "occam.release-evidence.v1") {
    errors.push(`${rel(root, path)}: schema must be occam.release-evidence.v1`);
  }
  if (!Array.isArray(ledger.items) || ledger.items.length === 0) {
    errors.push(`${rel(root, path)}: items[] is required`);
    return errors;
  }

  const seen = new Set();
  for (const [index, item] of ledger.items.entries()) {
    const label = `${rel(root, path)} items[${index}]`;
    if (!item?.id || seen.has(item.id)) {
      errors.push(`${label}: unique id is required`);
    } else {
      seen.add(item.id);
    }
    if (!item.title) errors.push(`${label}: title is required`);
    if (!item.toolchain) errors.push(`${label}: toolchain (named build) is required`);
    if (!item.hostSource) errors.push(`${label}: hostSource is required`);
    if (!item.capturedAt) errors.push(`${label}: capturedAt is required`);
    if (!item.path) {
      errors.push(`${label}: path is required`);
    } else {
      const artifact = join(root, item.path);
      if (!existsSync(artifact)) {
        errors.push(`${label}: missing artifact ${item.path}`);
      }
    }
    if (item.publicBuild === true) {
      if (item.toolchain !== PUBLIC_TOOLCHAIN) {
        errors.push(
          `${label}: publicBuild=true requires toolchain ${PUBLIC_TOOLCHAIN} (got ${item.toolchain ?? "empty"})`,
        );
      }
      if (!item.publicBuildName) {
        errors.push(`${label}: publicBuild=true requires publicBuildName`);
      }
    } else if (!item.hostNote) {
      errors.push(`${label}: non-public items must include hostNote (do not imply a Release certification)`);
    }
  }

  return errors;
}

function rel(root, path) {
  return relative(root, path).split("\\").join("/");
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  const errors = checkReleaseEvidence();
  if (errors.length > 0) {
    console.error(`release-evidence: FAILED (${errors.length})`);
    for (const error of errors) console.error(`  - ${error}`);
    process.exit(1);
  }
  console.log("release-evidence: OK");
}
