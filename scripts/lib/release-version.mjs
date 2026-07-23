#!/usr/bin/env node

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

function main() {
  const refIndex = process.argv.indexOf("--ref");
  if (refIndex < 0 || !process.argv[refIndex + 1]) {
    console.error("usage: node release-version.mjs --ref refs/tags/v<semver>");
    process.exit(1);
  }

  try {
    const metadata = parseReleaseTagRef(process.argv[refIndex + 1]);
    process.stdout.write(
      [
        `version=${metadata.version}`,
        `assembly_version=${metadata.assemblyVersion}`,
        `prerelease=${metadata.prerelease}`,
      ].join("\n") + "\n",
    );
  } catch (error) {
    console.error(`error: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
