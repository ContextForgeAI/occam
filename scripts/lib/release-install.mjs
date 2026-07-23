#!/usr/bin/env node
/**
 * Download (HTTPS) or use local release tarball; verify sha256 manifest; extract to install dir.
 *
 * Usage:
 *   node release-install.mjs --url https://.../ff-occam-VER-RID.tar.gz --install-dir PATH
 *   node release-install.mjs --file PATH.tar.gz --install-dir PATH [--manifest PATH.json]
 */
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { fileURLToPath } from "node:url";

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

function parseArgs(argv) {
  /** @type {{ url?: string; file?: string; installDir?: string; manifestUrl?: string; manifestFile?: string }} */
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--url") out.url = argv[++i];
    else if (arg === "--file") out.file = argv[++i];
    else if (arg === "--install-dir") out.installDir = argv[++i];
    else if (arg === "--manifest-url") out.manifestUrl = argv[++i];
    else if (arg === "--manifest") out.manifestFile = argv[++i];
    else if (arg === "-h" || arg === "--help") {
      console.log(`usage:
  node release-install.mjs --url <https-tarball> --install-dir <dir> [--manifest-url <url>]
  node release-install.mjs --file <tarball> --install-dir <dir> [--manifest <json>]`);
      process.exit(0);
    } else {
      fail(`unknown argument: ${arg}`);
    }
  }
  return out;
}

/**
 * @param {string} url
 */
function assertHttps(url) {
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    fail(`invalid URL: ${url}`);
  }
  if (parsed.protocol === "https:") {
    return;
  }
  if (parsed.protocol === "http:" && process.env.OCCAM_RELEASE_ALLOW_HTTP === "1") {
    console.warn("warning: OCCAM_RELEASE_ALLOW_HTTP=1 — using HTTP release URL (LAN/trusted forge only)");
    return;
  }
  fail(`release URL must use HTTPS (got ${parsed.protocol}); set OCCAM_RELEASE_ALLOW_HTTP=1 for trusted LAN HTTP`);
}

/**
 * @param {string} url
 * @returns {Promise<Buffer>}
 */
async function fetchBuffer(url) {
  assertHttps(url);
  const res = await fetch(url);
  if (!res.ok) {
    fail(`download failed: ${url} (${res.status})`);
  }
  return Buffer.from(await res.arrayBuffer());
}

/**
 * @param {string} filePath
 */
function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  hash.update(fs.readFileSync(filePath));
  return hash.digest("hex");
}

/**
 * @param {string} tarballPath
 * @param {string} installDir
 */
function extractTarball(tarballPath, installDir) {
  const parent = path.dirname(installDir);
  fs.mkdirSync(parent, { recursive: true });
  if (fs.existsSync(installDir)) {
    fs.rmSync(installDir, { recursive: true, force: true });
  }
  fs.mkdirSync(installDir, { recursive: true });
  if (process.platform === "win32") {
    execSync(`tar -xzf "${tarballPath}" -C "${installDir}" --strip-components=1`, {
      stdio: "inherit",
    });
  } else {
    execSync(`tar -xzf "${tarballPath}" -C "${installDir}" --strip-components=1`, {
      stdio: "inherit",
    });
  }
}

/**
 * @param {string} tarballUrl
 */
function defaultManifestUrl(tarballUrl) {
  if (tarballUrl.endsWith(".tar.gz")) {
    return tarballUrl.slice(0, -".tar.gz".length) + "-manifest.json";
  }
  return `${tarballUrl}-manifest.json`;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.installDir) {
    fail("--install-dir is required");
  }
  if (!args.url && !args.file) {
    fail("pass --url or --file");
  }
  if (args.url && args.file) {
    fail("pass only one of --url or --file");
  }

  const installDir = path.resolve(args.installDir);
  const tmpDir = path.join(installDir, "..", ".occam-release-tmp");
  fs.mkdirSync(tmpDir, { recursive: true });

  let tarballPath;
  let manifest;

  if (args.url) {
    const manifestUrl = args.manifestUrl ?? defaultManifestUrl(args.url);
    console.log(`release-install: manifest=${manifestUrl}`);
    const manifestBuf = await fetchBuffer(manifestUrl);
    manifest = JSON.parse(manifestBuf.toString("utf8"));

    console.log(`release-install: download=${args.url}`);
    const tarballBuf = await fetchBuffer(args.url);
    tarballPath = path.join(tmpDir, manifest.tarball ?? path.basename(new URL(args.url).pathname));
    fs.writeFileSync(tarballPath, tarballBuf);
  } else {
    tarballPath = path.resolve(args.file);
    if (!fs.existsSync(tarballPath)) {
      fail(`tarball not found: ${tarballPath}`);
    }
    const manifestPath = args.manifestFile
      ? path.resolve(args.manifestFile)
      : tarballPath.endsWith(".tar.gz")
        ? tarballPath.slice(0, -".tar.gz".length) + "-manifest.json"
        : `${tarballPath}-manifest.json`;
    if (!fs.existsSync(manifestPath)) {
      fail(`manifest not found: ${manifestPath}`);
    }
    manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  }

  if (!manifest.sha256 || typeof manifest.sha256 !== "string") {
    fail("manifest missing sha256");
  }
  if (!manifest.version || !manifest.rid) {
    fail("manifest missing version or rid");
  }

  const actualSha = sha256File(tarballPath);
  if (actualSha !== manifest.sha256.toLowerCase()) {
    fail(`sha256 mismatch: expected ${manifest.sha256}, got ${actualSha}`);
  }
  console.log(`release-install: sha256 OK (${actualSha})`);

  const nodeMajor = Number.parseInt(process.versions.node.split(".")[0], 10);
  const minNode = manifest.nodeMajorMin ?? 20;
  if (!Number.isFinite(nodeMajor) || nodeMajor < minNode) {
    fail(`Node.js ${minNode}+ required for install (found ${process.versions.node})`);
  }

  console.log(`release-install: extract -> ${installDir}`);
  extractTarball(tarballPath, installDir);

  const versionPath = path.join(installDir, "VERSION");
  if (fs.existsSync(versionPath)) {
    const onDisk = fs.readFileSync(versionPath, "utf8").trim();
    if (onDisk && onDisk !== manifest.version) {
      fail(`VERSION mismatch: manifest=${manifest.version}, tarball=${onDisk}`);
    }
  }

  try {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  } catch {
    // non-fatal
  }

  console.log(`release-install: version=${manifest.version} rid=${manifest.rid}`);
  console.log("release-install: OK");
}

main().catch((err) => {
  fail(err instanceof Error ? err.message : String(err));
});
