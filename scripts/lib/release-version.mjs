#!/usr/bin/env node

import fs from "node:fs";
import { pathToFileURL } from "node:url";

// Core SemVer without build metadata (+…). Build metadata is rejected for release tags.
const SEMVER_PATTERN =
  /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/;

export function parseSemanticVersion(version) {
  if (typeof version !== "string" || version.length === 0) {
    throw new Error("invalid semantic version: (empty)");
  }
  if (version.includes("+")) {
    throw new Error(
      `release versions must not include SemVer build metadata (+…): ${version}`,
    );
  }

  const match = SEMVER_PATTERN.exec(version);
  if (!match) {
    throw new Error(`invalid semantic version: ${version}`);
  }

  const prereleaseRaw = match[4] ?? "";
  const prereleaseIdentifiers = prereleaseRaw.length > 0 ? prereleaseRaw.split(".") : [];
  for (const identifier of prereleaseIdentifiers) {
    if (/^\d+$/.test(identifier) && identifier.length > 1 && identifier.startsWith("0")) {
      throw new Error(`invalid numeric prerelease identifier: ${identifier}`);
    }
  }

  // Any non-empty SemVer prerelease component → GitHub prerelease.
  // Only versions with no prerelease component are stable.
  return {
    version,
    assemblyVersion: `${match[1]}.${match[2]}.${match[3]}.0`,
    prerelease: prereleaseIdentifiers.length > 0,
  };
}

export function parseReleaseTagRef(ref) {
  const prefix = "refs/tags/v";
  if (!ref.startsWith(prefix)) {
    throw new Error(`release ref must match refs/tags/v*: ${ref}`);
  }
  return parseSemanticVersion(ref.slice(prefix.length));
}

export function parseLatestReleasedVersion(changelog) {
  if (typeof changelog !== "string") {
    throw new Error("changelog must be a string");
  }
  const headings = changelog.matchAll(/^## \[([^\]]+)\]/gm);
  for (const heading of headings) {
    if (heading[1] !== "Unreleased") {
      return parseSemanticVersion(heading[1]);
    }
  }
  throw new Error("changelog has no released version heading");
}

export function resolveWorkflowVersion(ref, changelog) {
  if (typeof ref !== "string" || ref.length === 0) {
    throw new Error("workflow ref is required");
  }
  if (ref.startsWith("refs/tags/")) {
    return parseReleaseTagRef(ref);
  }
  return parseLatestReleasedVersion(changelog);
}

export function assertReleaseAlignment(ref, versionFile, changelog) {
  const tagMetadata = parseReleaseTagRef(ref);
  const sourceMetadata = parseSemanticVersion(String(versionFile).trim());
  const changelogMetadata = parseLatestReleasedVersion(changelog);
  if (sourceMetadata.version !== tagMetadata.version) {
    throw new Error(
      `release tag/version file mismatch (tag=${tagMetadata.version}, VERSION=${sourceMetadata.version})`,
    );
  }
  if (changelogMetadata.version !== tagMetadata.version) {
    throw new Error(
      `release tag/changelog mismatch (tag=${tagMetadata.version}, changelog=${changelogMetadata.version})`,
    );
  }
  return tagMetadata;
}

function printMetadata(metadata) {
  process.stdout.write(
    [
      `version=${metadata.version}`,
      `assembly_version=${metadata.assemblyVersion}`,
      `prerelease=${metadata.prerelease}`,
    ].join("\n") + "\n",
  );
}

function main() {
  const assertAlignment = process.argv.includes("--assert-alignment");
  const refIndex = process.argv.indexOf("--ref");
  const workflowRefIndex = process.argv.indexOf("--workflow-ref");
  const changelogIndex = process.argv.indexOf("--changelog");
  const versionFileIndex = process.argv.indexOf("--version-file");

  try {
    if (assertAlignment) {
      if (
        refIndex < 0 ||
        !process.argv[refIndex + 1] ||
        versionFileIndex < 0 ||
        !process.argv[versionFileIndex + 1] ||
        changelogIndex < 0 ||
        !process.argv[changelogIndex + 1]
      ) {
        throw new Error(
          "--assert-alignment requires --ref, --version-file, and --changelog",
        );
      }
      const versionFile = fs.readFileSync(process.argv[versionFileIndex + 1], "utf8");
      const changelog = fs.readFileSync(process.argv[changelogIndex + 1], "utf8");
      printMetadata(assertReleaseAlignment(process.argv[refIndex + 1], versionFile, changelog));
      return;
    }

    if (refIndex >= 0 && process.argv[refIndex + 1]) {
      printMetadata(parseReleaseTagRef(process.argv[refIndex + 1]));
      return;
    }

    if (
      workflowRefIndex >= 0 &&
      process.argv[workflowRefIndex + 1] &&
      changelogIndex >= 0 &&
      process.argv[changelogIndex + 1]
    ) {
      const changelog = fs.readFileSync(process.argv[changelogIndex + 1], "utf8");
      printMetadata(resolveWorkflowVersion(process.argv[workflowRefIndex + 1], changelog));
      return;
    }

    throw new Error(
      "usage: node release-version.mjs --ref refs/tags/v<semver> | --workflow-ref REF --changelog CHANGELOG.md | --assert-alignment --ref REF --version-file VERSION --changelog CHANGELOG.md",
    );
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
