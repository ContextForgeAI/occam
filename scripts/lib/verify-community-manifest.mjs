#!/usr/bin/env node
/**
 * Verify profiles/playbooks/community/manifest.json sha256 rows match on-disk JSON files.
 * Exit 0 when every manifest row matches and every community playbook has a row.
 */
import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));

function sha256Hex(content) {
  return createHash("sha256").update(content, "utf8").digest("hex");
}

/** LF-normalized UTF-8 — matches git eol=lf blobs and Linux CI checkout. */
function sha256FileHex(filePath) {
  const raw = readFileSync(filePath, "utf8");
  return sha256Hex(raw.replace(/\r\n/g, "\n"));
}

function resolveCommunityDir() {
  const occamHome = process.env.OCCAM_HOME || join(scriptDir, "..", "..");
  return join(resolve(occamHome), "profiles", "playbooks", "community");
}

function printHelp() {
  process.stdout.write(`Usage: verify-community-manifest.mjs

Verifies manifest.json sha256 rows against community playbook files under OCCAM_HOME.
`);
}

function main() {
  if (process.argv.includes("--help") || process.argv.includes("-h")) {
    printHelp();
    process.exit(0);
  }

  const communityDir = resolveCommunityDir();
  const manifestPath = join(communityDir, "manifest.json");
  if (!existsSync(manifestPath)) {
    process.stderr.write(`error: manifest not found: ${manifestPath}\n`);
    process.exit(1);
  }

  let manifest;
  try {
    manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  } catch (err) {
    process.stderr.write(`error: invalid manifest.json: ${err.message}\n`);
    process.exit(1);
  }

  if (!Array.isArray(manifest.playbooks) || manifest.playbooks.length === 0) {
    process.stderr.write("error: manifest.playbooks must be a non-empty array\n");
    process.exit(1);
  }

  let errors = 0;
  const listedFiles = new Set();

  for (const row of manifest.playbooks) {
    const label = row.id ?? row.file ?? "(unknown)";
    if (!row.file || typeof row.file !== "string") {
      process.stderr.write(`error: row ${label} missing file\n`);
      errors += 1;
      continue;
    }
    if (!row.sha256 || typeof row.sha256 !== "string" || row.sha256.length !== 64) {
      process.stderr.write(`error: row ${label} missing or invalid sha256\n`);
      errors += 1;
      continue;
    }

    const filePath = join(communityDir, row.file);
    if (!existsSync(filePath)) {
      process.stderr.write(`error: missing playbook file for ${label}: ${row.file}\n`);
      errors += 1;
      continue;
    }

    const actual = sha256FileHex(filePath);
    const expected = row.sha256.toLowerCase();
    if (actual !== expected) {
      process.stderr.write(
        `error: sha256 mismatch for ${row.file}: expected ${expected}, got ${actual}\n`,
      );
      errors += 1;
      continue;
    }

    listedFiles.add(row.file);
  }

  const onDisk = readdirSync(communityDir).filter(
    (name) => name.endsWith(".json") && name !== "manifest.json",
  );
  for (const file of onDisk) {
    if (!listedFiles.has(file)) {
      process.stderr.write(`error: orphan community playbook not listed in manifest: ${file}\n`);
      errors += 1;
    }
  }

  if (errors > 0) {
    process.stderr.write(`verify-community-manifest: FAIL (${errors} error(s))\n`);
    process.exit(1);
  }

  process.stdout.write(
    `verify-community-manifest: OK (${manifest.playbooks.length} playbook(s))\n`,
  );
}

main();
