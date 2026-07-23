#!/usr/bin/env node
/**
 * Build per-RID release tarball + external manifest (sha256).
 * Usage: node build-release.mjs --rid win-x64 [--version 0.7.7-install] [--output-dir artifacts/releases]
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync, execSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { parseSemanticVersion } from "./release-version.mjs";

const SUPPORTED_RIDS = new Set(["win-x64", "linux-x64", "osx-arm64", "osx-x64"]);
const MIN_NODE_MAJOR = 20;

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
      console.log(`usage: node build-release.mjs --rid <rid> [--version VER] [--output-dir DIR]

Supported RIDs: ${[...SUPPORTED_RIDS].join(", ")}`);
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

function copyTreeFiltered(src, dest, filter) {
  fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (!filter(srcPath, entry)) {
      continue;
    }
    if (entry.isDirectory()) {
      copyTreeFiltered(srcPath, destPath, filter);
    } else if (entry.isFile()) {
      fs.mkdirSync(path.dirname(destPath), { recursive: true });
      fs.copyFileSync(srcPath, destPath);
    }
  }
}

function stageReleaseTree(version, rid, publishedBinary, stageExeName) {
  const stageName = `ff-occam-${version}-${rid}`;
  const stageRoot = path.join(repoRoot, "artifacts", ".release-stage", stageName);
  if (fs.existsSync(stageRoot)) {
    fs.rmSync(stageRoot, { recursive: true, force: true });
  }
  fs.mkdirSync(stageRoot, { recursive: true });

  if (!fs.existsSync(publishedBinary)) {
    fail(`published binary not found: ${publishedBinary}`);
  }
  fs.copyFileSync(publishedBinary, path.join(stageRoot, stageExeName));

  const workersSrc = path.join(repoRoot, "workers");
  const workersDest = path.join(stageRoot, "workers");
  copyTreeFiltered(workersSrc, workersDest, (p, entry) => {
    if (entry.name === "node_modules") {
      return false;
    }
    return !p.split(path.sep).includes("node_modules");
  });

  const scriptFiles = [
    "launch-mcp-host.mjs",
    "occam.mjs",
    "occam",
    "occam.ps1",
    "install.sh",
    "install.ps1",
    "occam-doctor.sh",
    "occam-doctor.ps1",
    "occam-onboard.mjs",
    "occam-help.mjs",
    "occam-skill-install.mjs",
    "sync-occam-skill-package.mjs",
    "occam-refresh-host.mjs",
    "occam-session.mjs",
    "hermes-smoke.mjs",
    "occam-wrapper.sh",
    "build-release.sh",
    "build-release.ps1",
  ];
  for (const name of scriptFiles) {
    const src = path.join(repoRoot, "scripts", name);
    if (fs.existsSync(src)) {
      const dest = path.join(stageRoot, "scripts", name);
      fs.mkdirSync(path.dirname(dest), { recursive: true });
      fs.copyFileSync(src, dest);
    }
  }

  const libSrc = path.join(repoRoot, "scripts", "lib");
  const libDest = path.join(stageRoot, "scripts", "lib");
  copyTreeFiltered(libSrc, libDest, (p, entry) => entry.name !== "node_modules");

  const profilesSrc = path.join(repoRoot, "profiles");
  const profilesDest = path.join(stageRoot, "profiles");
  copyTreeFiltered(profilesSrc, profilesDest, () => true);

  const skillsSrc = path.join(repoRoot, "skills", "occam");
  if (fs.existsSync(skillsSrc)) {
    const skillsDest = path.join(stageRoot, "skills", "occam");
    copyTreeFiltered(skillsSrc, skillsDest, () => true);
  }

  fs.writeFileSync(path.join(stageRoot, "VERSION"), `${version}\n`, "utf8");

  const innerManifest = {
    version,
    rid,
    nodeMajorMin: MIN_NODE_MAJOR,
    layout: "level-b",
  };
  fs.writeFileSync(
    path.join(stageRoot, "release-manifest.json"),
    `${JSON.stringify(innerManifest, null, 2)}\n`,
    "utf8",
  );

  return { stageRoot, stageName, exeName: stageExeName };
}

function createTarball(stageRoot, stageName, tarballPath) {
  fs.mkdirSync(path.dirname(tarballPath), { recursive: true });
  if (fs.existsSync(tarballPath)) {
    fs.unlinkSync(tarballPath);
  }
  const parent = path.dirname(stageRoot);
  const base = path.basename(stageRoot);
  if (process.platform === "win32") {
    execSync(`tar -czf "${tarballPath}" -C "${parent}" "${base}"`, {
      stdio: "inherit",
      cwd: repoRoot,
    });
  } else {
    execSync(`tar -czf "${tarballPath}" -C "${parent}" "${base}"`, {
      stdio: "inherit",
      cwd: repoRoot,
    });
  }
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
    fail(`unsupported RID: ${rid} (supported: ${[...SUPPORTED_RIDS].join(", ")})`);
  }

  const version = args.version ?? readLatestReleasedVersion();
  let versionMetadata;
  try {
    versionMetadata = parseSemanticVersion(version);
  } catch (error) {
    fail(error instanceof Error ? error.message : String(error));
  }
  const outputDir = path.resolve(args.outputDir ?? path.join(repoRoot, "artifacts", "releases"));
  fs.mkdirSync(outputDir, { recursive: true });

  console.log(`build-release: version=${version} rid=${rid}`);
  console.log("build-release: dotnet publish ...");
  execFileSync(
    "dotnet",
    [
      "publish",
      path.join(repoRoot, "src/FFOccamMcp.Core/FFOccamMcp.Core.csproj"),
      "-c",
      "Release",
      "-r",
      rid,
      `-p:Version=${version}`,
      `-p:AssemblyVersion=${versionMetadata.assemblyVersion}`,
      `-p:FileVersion=${versionMetadata.assemblyVersion}`,
      `-p:InformationalVersion=${version}`,
      "-p:IncludeSourceRevisionInInformationalVersion=false",
    ],
    { stdio: "inherit", cwd: repoRoot },
  );

  const publishDir = path.join(repoRoot, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish");
  const publishCandidates = ["OccamMcp.Core", "FFOccamMcp.Core"].map((base) =>
    path.join(publishDir, rid.startsWith("win-") ? `${base}.exe` : base),
  );
  const publishedBinary = publishCandidates.find((p) => fs.existsSync(p));
  if (!publishedBinary) {
    fail(`published binary not found under ${publishDir} (tried OccamMcp.Core, FFOccamMcp.Core)`);
  }

  const stageExeName = rid.startsWith("win-") ? "OccamMcp.Core.exe" : "OccamMcp.Core";
  const { stageRoot, stageName } = stageReleaseTree(version, rid, publishedBinary, stageExeName);

  const tarballName = `${stageName}.tar.gz`;
  const tarballPath = path.join(outputDir, tarballName);
  createTarball(stageRoot, stageName, tarballPath);

  const sha256 = sha256File(tarballPath);
  const manifestName = `${stageName}-manifest.json`;
  const manifestPath = path.join(outputDir, manifestName);
  const manifest = {
    version,
    rid,
    sha256,
    nodeMajorMin: MIN_NODE_MAJOR,
    tarball: tarballName,
    hostBinary: rid.startsWith("win-") ? "OccamMcp.Core.exe" : "OccamMcp.Core",
  };
  fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");

  const sizeBytes = fs.statSync(tarballPath).size;
  console.log(`build-release: tarball=${tarballPath}`);
  console.log(`build-release: manifest=${manifestPath}`);
  console.log(`build-release: sha256=${sha256}`);
  console.log(`build-release: sizeBytes=${sizeBytes}`);
  console.log("build-release: OK");
}

main();
