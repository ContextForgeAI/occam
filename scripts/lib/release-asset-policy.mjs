#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { parseSemanticVersion } from "./release-version.mjs";
import { verifyReleaseManifest } from "./verify-release-manifest.mjs";

export const RELEASE_RIDS = Object.freeze(["linux-x64", "osx-arm64", "win-x64"]);

export function expectedReleaseAssetNames(version, signed) {
  parseSemanticVersion(version);
  const unsigned = RELEASE_RIDS.flatMap((rid) => {
    const stem = `ff-occam-${version}-${rid}`;
    return [`${stem}.tar.gz`, `${stem}-manifest.json`];
  });
  if (!signed) return unsigned;
  return [...unsigned, ...RELEASE_RIDS.map((rid) => `ff-occam-${version}-${rid}.tar.gz.bundle`)];
}

function sorted(values) {
  return [...values].sort((left, right) => left.localeCompare(right));
}

function assertExactNames(actual, expected, label) {
  const actualSorted = sorted(actual);
  const expectedSorted = sorted(expected);
  const duplicates = actualSorted.filter((name, index) => index > 0 && name === actualSorted[index - 1]);
  if (duplicates.length > 0) {
    throw new Error(`${label} has duplicate names: ${[...new Set(duplicates)].join(", ")}`);
  }

  const missing = expectedSorted.filter((name) => !actualSorted.includes(name));
  const unexpected = actualSorted.filter((name) => !expectedSorted.includes(name));
  if (missing.length > 0 || unexpected.length > 0) {
    throw new Error(
      `${label} is not the exact release asset set` +
        `${missing.length > 0 ? `; missing: ${missing.join(", ")}` : ""}` +
        `${unexpected.length > 0 ? `; unexpected: ${unexpected.join(", ")}` : ""}`,
    );
  }
}

export function verifyReleaseDirectory({ directory, version, signed }) {
  const resolved = path.resolve(directory);
  const entries = fs.readdirSync(resolved, { withFileTypes: true });
  const unsafe = entries.filter((entry) => !entry.isFile()).map((entry) => entry.name);
  if (unsafe.length > 0) {
    throw new Error(`release directory contains non-regular entries: ${unsafe.join(", ")}`);
  }

  assertExactNames(
    entries.map((entry) => entry.name),
    expectedReleaseAssetNames(version, signed),
    "release directory",
  );

  for (const rid of RELEASE_RIDS) {
    const result = verifyReleaseManifest({ version, rid, outputDir: resolved });
    if (result.manifest.runtimeLayout !== "self-contained-v1") {
      throw new Error(
        `manifest runtime layout mismatch for ${rid} (expected self-contained-v1, got ${String(result.manifest.runtimeLayout)})`,
      );
    }
    if (
      result.manifest.signaturePolicy !== "required-cosign-v1" &&
      result.manifest.signaturePolicy !== "sha256-only"
    ) {
      throw new Error(
        `manifest signaturePolicy missing or unsupported for ${rid} (got ${String(result.manifest.signaturePolicy)})`,
      );
    }
  }
}

function readJson(file) {
  const parsed = JSON.parse(fs.readFileSync(file, "utf8"));
  return parsed;
}

function flattenReleaseList(value) {
  if (!Array.isArray(value)) {
    throw new Error("release list response must be an array");
  }
  const flattened = [];
  for (const item of value) {
    if (Array.isArray(item)) flattened.push(...item);
    else flattened.push(item);
  }
  return flattened;
}

export function assertReleaseTagAbsent(releases, tag) {
  const matches = flattenReleaseList(releases).filter((release) => release?.tag_name === tag);
  if (matches.length > 0) {
    const states = matches
      .map((release) => (release?.draft ? `draft:${release.id}` : `published:${release?.id}`))
      .join(", ");
    throw new Error(`release tag already exists and will not be mutated: ${tag} (${states})`);
  }
}

export function verifyReleaseRecord({ record, tag, version, draft, prerelease }) {
  if (!record || typeof record !== "object" || Array.isArray(record)) {
    throw new Error("release record must be an object");
  }
  if (!Number.isSafeInteger(record.id) || record.id <= 0) {
    throw new Error("release record has no valid numeric id");
  }
  if (record.tag_name !== tag) {
    throw new Error(`release tag mismatch (expected ${tag}, got ${String(record.tag_name)})`);
  }
  if (record.name !== tag) {
    throw new Error(`release title mismatch (expected ${tag}, got ${String(record.name)})`);
  }
  if (record.draft !== draft) {
    throw new Error(`release draft mismatch (expected ${draft}, got ${String(record.draft)})`);
  }
  if (record.prerelease !== prerelease) {
    throw new Error(
      `release prerelease mismatch (expected ${prerelease}, got ${String(record.prerelease)})`,
    );
  }
  if (!Array.isArray(record.assets)) {
    throw new Error("release assets must be an array");
  }
  assertExactNames(
    record.assets.map((asset) => asset?.name),
    expectedReleaseAssetNames(version, true),
    "GitHub Release",
  );
  return record.id;
}

export function verifyReleaseList({ releases, tag, version, draft, prerelease }) {
  const matches = flattenReleaseList(releases).filter((release) => release?.tag_name === tag);
  if (matches.length !== 1) {
    throw new Error(`expected exactly one release for ${tag}, found ${matches.length}`);
  }
  return verifyReleaseRecord({ record: matches[0], tag, version, draft, prerelease });
}

function parseBoolean(value, name) {
  if (value === "true") return true;
  if (value === "false") return false;
  throw new Error(`${name} must be true or false`);
}

function parseArgs(argv) {
  const command = argv[0];
  const options = {};
  for (let index = 1; index < argv.length; index += 1) {
    const arg = argv[index];
    if (!arg.startsWith("--") || !argv[index + 1]) {
      throw new Error(`invalid argument: ${arg}`);
    }
    options[arg.slice(2)] = argv[++index];
  }
  return { command, options };
}

function requireOption(options, name) {
  const value = options[name];
  if (!value) throw new Error(`--${name} is required`);
  return value;
}

function main() {
  const { command, options } = parseArgs(process.argv.slice(2));
  if (command === "verify-directory") {
    const version = requireOption(options, "version");
    const signed = parseBoolean(requireOption(options, "signed"), "--signed");
    verifyReleaseDirectory({ directory: requireOption(options, "directory"), version, signed });
    console.log(`release-asset-policy: exact ${signed ? "signed" : "unsigned"} directory OK`);
    return;
  }
  if (command === "assert-tag-absent") {
    assertReleaseTagAbsent(readJson(requireOption(options, "file")), requireOption(options, "tag"));
    console.log("release-asset-policy: tag absent OK");
    return;
  }
  if (command === "verify-release-list") {
    const id = verifyReleaseList({
      releases: readJson(requireOption(options, "file")),
      tag: requireOption(options, "tag"),
      version: requireOption(options, "version"),
      draft: parseBoolean(requireOption(options, "draft"), "--draft"),
      prerelease: parseBoolean(requireOption(options, "prerelease"), "--prerelease"),
    });
    process.stdout.write(`release_id=${id}\n`);
    return;
  }
  if (command === "verify-release-json") {
    const id = verifyReleaseRecord({
      record: readJson(requireOption(options, "file")),
      tag: requireOption(options, "tag"),
      version: requireOption(options, "version"),
      draft: parseBoolean(requireOption(options, "draft"), "--draft"),
      prerelease: parseBoolean(requireOption(options, "prerelease"), "--prerelease"),
    });
    process.stdout.write(`release_id=${id}\n`);
    return;
  }
  throw new Error(
    "usage: release-asset-policy.mjs verify-directory|assert-tag-absent|verify-release-list|verify-release-json [options]",
  );
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main();
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}
