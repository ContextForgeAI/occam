#!/usr/bin/env node
/**
 * @ff-occam/mcp — npm entry point (ESM).
 * Downloads the .NET AOT host from GitHub Releases (verifying sha256), then starts
 * the MCP server over stdio (default) or WebSocket.
 *
 * Usage:
 *   npx @ff-occam/mcp                    # stdio MCP (default — Cursor, Claude, any MCP client)
 *   npx @ff-occam/mcp --mcp-server       # WebSocket MCP on 127.0.0.1:5050
 *   npx @ff-occam/mcp --help             # show help
 *   npx @ff-occam/mcp --version          # show version
 *
 * Set OCCAM_HOME to a local build to skip the download entirely.
 *
 * When run from a git clone (…/FFOccamMCP/packages/occam-mcp/bin/…), auto-detects the
 * repo root and delegates to scripts/launch-mcp-host.mjs instead of downloading from
 * GitHub Releases — the path Hermes/Cursor clone installs should use.
 */

import { spawn } from "node:child_process";
import { discoverRepoRoot, findInstallRoots } from "../lib/discover-repo.mjs";
import { resolveHostBinary } from "../lib/resolve-host-binary.mjs";
import { formatInstallBlockerMessage } from "../../../scripts/lib/host-install-gate.mjs";
import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  chmodSync,
  createReadStream,
  createWriteStream,
} from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { platform, arch } from "node:os";
import { createHash } from "node:crypto";
import { createGunzip } from "node:zlib";
import { pipeline } from "node:stream/promises";
import { Readable } from "node:stream";
import { extract as createExtract } from "tar";

const __dirname = dirname(fileURLToPath(import.meta.url));
const PACKAGE_ROOT = resolve(__dirname, "..");
const VERSION = JSON.parse(readFileSync(join(PACKAGE_ROOT, "package.json"), "utf8")).version;

const RELEASE_BASE_URL =
  process.env.OCCAM_RELEASE_BASE_URL ||
  "https://github.com/ContextForgeAI/occam/releases/download";

const RID_MAP = {
  "win32-x64": "win-x64",
  "linux-x64": "linux-x64",
  "darwin-x64": "osx-x64",
  "darwin-arm64": "osx-arm64",
};

const BINARY_NAMES = {
  "win-x64": "OccamMcp.Core.exe",
  "linux-x64": "OccamMcp.Core",
  "osx-x64": "OccamMcp.Core",
  "osx-arm64": "OccamMcp.Core",
};

function getRid() {
  const key = `${platform()}-${arch()}`;
  const rid = RID_MAP[key];
  if (!rid) {
    console.error(`[ff-occam/mcp] Unsupported platform/arch: ${key}`);
    console.error("[ff-occam/mcp] Supported: win32-x64, linux-x64, darwin-x64, darwin-arm64");
    process.exit(1);
  }
  return rid;
}

function getBinaryName(rid) {
  return BINARY_NAMES[rid];
}

function getCacheDir() {
  // OCCAM_HOME wins (points at a local build); otherwise a platform-specific cache dir.
  if (process.env.OCCAM_HOME) {
    return resolve(process.env.OCCAM_HOME);
  }
  const home = process.env.HOME || process.env.USERPROFILE;
  if (platform() === "win32") {
    return join(process.env.LOCALAPPDATA || join(home, "AppData", "Local"), "ff-occam");
  }
  return join(home, ".cache", "ff-occam");
}

function getInstallDir(rid) {
  return join(getCacheDir(), "bin", VERSION, rid);
}

function getBinaryPath(rid) {
  return join(getInstallDir(rid), getBinaryName(rid));
}

function getManifestPath(rid) {
  return join(getInstallDir(rid), "release-manifest.json");
}

// Fetch to a file, following redirects (GitHub release assets 302 to a CDN).
async function downloadFile(url, destPath) {
  const res = await fetch(url, { redirect: "follow" });
  if (!res.ok || !res.body) {
    throw new Error(`HTTP ${res.status} ${res.statusText}: ${url}`);
  }
  await pipeline(Readable.fromWeb(res.body), createWriteStream(destPath));
}

async function extractTarball(tarballPath, destDir) {
  await pipeline(
    createReadStream(tarballPath),
    createGunzip(),
    createExtract({ cwd: destDir, strip: 1 }),
  );
}

function sha256OfFile(filePath) {
  return createHash("sha256").update(readFileSync(filePath)).digest("hex");
}

function findCachedBinary(installDir, rid) {
  const names = rid.startsWith("win-")
    ? ["OccamMcp.Core.exe", "FFOccamMcp.Core.exe"]
    : ["OccamMcp.Core", "FFOccamMcp.Core"];
  for (const name of names) {
    const candidate = join(installDir, name);
    if (existsSync(candidate)) {
      return candidate;
    }
  }
  return null;
}

// A local build ships the binary directly — never touch the network for it.
function tryLocalBinaryAt(home, rid) {
  return resolveHostBinary(resolve(home), rid);
}

/** Starting points for local install discovery (clone / tarball — any MCP host). */
function installDiscoveryStarts() {
  /** @type {string[]} */
  const starts = [];
  const explicit = process.env.OCCAM_HOME?.trim();
  if (explicit) starts.push(explicit);
  starts.push(PACKAGE_ROOT);
  starts.push(process.cwd());
  const entry = process.argv[1];
  if (entry) starts.push(dirname(resolve(entry)));
  return starts;
}

function spawnChild(command, args, options) {
  const child = spawn(command, args, options);
  child.on("error", (err) => {
    console.error(`[ff-occam/mcp] Failed to start: ${err.message}`);
    process.exit(1);
  });
  child.on("exit", (code) => process.exit(code ?? 0));
  process.on("SIGINT", () => child.kill("SIGINT"));
  process.on("SIGTERM", () => child.kill("SIGTERM"));
  process.on("exit", () => child.kill());
  return child;
}

/** Block in-repo occam-mcp.js on git clone — use occam-wrapper.sh / launch-mcp-host.mjs. */
function rejectInRepoNpmEntry() {
  if (process.env.OCCAM_NPX_ON_CLONE === "1") {
    return;
  }
  const entry = process.argv[1];
  if (!entry) {
    return;
  }
  const repoRoot = discoverRepoRoot(dirname(resolve(entry)));
  if (
    repoRoot &&
    existsSync(join(repoRoot, "packages", "occam-mcp", "bin", "occam-mcp.js"))
  ) {
    failCloneWithoutBinary(repoRoot);
  }
}

/** Git clone — refuse (see INSTALL.md). */
function failCloneWithoutBinary(repoRoot) {
  console.error(formatInstallBlockerMessage(repoRoot, { prefix: "[ff-occam/mcp]" }));
  process.exit(1);
}

async function ensureBinary(rid) {
  const explicitHome = process.env.OCCAM_HOME?.trim();
  if (explicitHome) {
    const local = tryLocalBinaryAt(explicitHome, rid);
    if (local) {
      return { binaryPath: local, home: resolve(explicitHome) };
    }
  }

  const binaryPath = getBinaryPath(rid);
  const manifestPath = getManifestPath(rid);
  const installDir = getInstallDir(rid);

  // Cache hit — the extracted inner manifest records version + rid.
  const cachedBinary = findCachedBinary(installDir, rid);
  if (cachedBinary && existsSync(manifestPath)) {
    try {
      const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
      if (manifest.version === VERSION && manifest.rid === rid) {
        return { binaryPath: cachedBinary, home: installDir };
      }
    } catch {
      // fall through and re-download
    }
  }

  console.error(`[ff-occam/mcp] Downloading ${VERSION} for ${rid}...`);

  if (existsSync(installDir)) {
    rmSync(installDir, { recursive: true, force: true });
  }
  mkdirSync(installDir, { recursive: true });

  const stem = `ff-occam-${VERSION}-${rid}`;
  const tarballName = `${stem}.tar.gz`;
  const tarballUrl = `${RELEASE_BASE_URL}/v${VERSION}/${tarballName}`;
  const manifestUrl = `${RELEASE_BASE_URL}/v${VERSION}/${stem}-manifest.json`;

  const tarballPath = join(installDir, tarballName);
  const dlManifestPath = join(installDir, "download-manifest.json");

  try {
    await downloadFile(manifestUrl, dlManifestPath);
    await downloadFile(tarballUrl, tarballPath);

    const manifest = JSON.parse(readFileSync(dlManifestPath, "utf8"));
    const actualSha = sha256OfFile(tarballPath);
    if (actualSha !== String(manifest.sha256).toLowerCase()) {
      throw new Error(`SHA256 mismatch: expected ${manifest.sha256}, got ${actualSha}`);
    }

    await extractTarball(tarballPath, installDir);
    rmSync(tarballPath, { force: true });

    const extractedBinary = findCachedBinary(installDir, rid);
    if (!extractedBinary) {
      throw new Error(`extracted tarball missing OccamMcp.Core under ${installDir}`);
    }

    if (rid !== "win-x64") {
      chmodSync(extractedBinary, 0o755);
    }

    console.error(`[ff-occam/mcp] Installed to ${extractedBinary}`);
    return { binaryPath: extractedBinary, home: installDir };
  } catch (err) {
    console.error(`[ff-occam/mcp] Download failed: ${err.message}`);
    const localRoots = findInstallRoots(installDiscoveryStarts());
    if (localRoots.length > 0) {
      console.error(
        `[ff-occam/mcp] Found a local install at ${localRoots[0]} but release download was attempted.\n` +
          "[ff-occam/mcp] Wire your MCP host to scripts/launch-mcp-host.mjs with OCCAM_HOME set, or run occam doctor.",
      );
    } else {
      console.error(
        "[ff-occam/mcp] npx / global install needs release assets for this version.\n" +
          "[ff-occam/mcp] Git clone / tarball: clone, set OCCAM_HOME, run ./scripts/occam-doctor.sh,\n" +
          "[ff-occam/mcp] then wire MCP to scripts/launch-mcp-host.mjs (see docs/getting-started.md).\n" +
          "[ff-occam/mcp] Override download base with OCCAM_RELEASE_BASE_URL if needed.",
      );
    }
    process.exit(1);
  }
}

/**
 * Resolve how to start the host: local AOT binary, repo launcher (clone), or release download.
 * @returns {Promise<{ kind: "binary", binaryPath: string, home: string } | { kind: "launcher", repoRoot: string }>}
 */
async function resolveHost(rid) {
  const installRoots = findInstallRoots(installDiscoveryStarts());

  for (const repoRoot of installRoots) {
    if (!process.env.OCCAM_HOME?.trim()) {
      process.env.OCCAM_HOME = repoRoot;
    }
    const local = tryLocalBinaryAt(repoRoot, rid);
    if (local) {
      return { kind: "binary", binaryPath: local, home: repoRoot };
    }
    if (existsSync(join(repoRoot, "scripts", "launch-mcp-host.mjs"))) {
      return { kind: "launcher", repoRoot };
    }
  }

  const { binaryPath, home } = await ensureBinary(rid);
  return { kind: "binary", binaryPath, home };
}

function printHelp() {
  console.log(`
@ff-occam/mcp v${VERSION} — FF-Occam MCP Server

USAGE:
  npx @ff-occam/mcp [OPTIONS]

OPTIONS:
  --mcp-server           Start WebSocket MCP server on 127.0.0.1:5050
  --port <number>        WebSocket port (default: 5050)
  --host <address>       WebSocket host (default: 127.0.0.1)
  --help                 Show this help
  --version              Show version

ENVIRONMENT:
  OCCAM_HOME             Repo root (clone/tarball) — skips GitHub download
  OCCAM_RELEASE_BASE_URL Custom release base URL (npx / global install only)
  OCCAM_RECEIPTS=off     Disable signed extraction receipts
  OCCAM_BANNER=0         Disable stderr banner
  OCCAM_LOG=1            Enable stderr profiler

GIT CLONE / LOCAL INSTALL:
  Do NOT use this script on a git clone. Use scripts/occam-wrapper.sh (Hermes) or
  node scripts/launch-mcp-host.mjs with OCCAM_HOME. Run ./scripts/occam-doctor.sh first.
  See INSTALL.md at repo root.

EXAMPLES:
  npx @ff-occam/mcp                    # stdio mode (for Cursor, Claude)
  npx @ff-occam/mcp --mcp-server       # WebSocket mode
  npx @ff-occam/mcp --mcp-server --port 5051

MCP TOOLS (14):
  occam_transcode, occam_probe, occam_digest, occam_map, occam_search,
  occam_playbook_resolve, occam_playbook_heal, occam_playbook_save,
  occam_extract_knowledge, occam_verify, occam_claim_check, occam_attest,
  occam_playbook_lint, occam_dataset_export

DOCS: https://github.com/ContextForgeAI/occam/tree/main/docs
`);
}

async function main() {
  const args = process.argv.slice(2);

  if (args.includes("--help") || args.includes("-h")) {
    printHelp();
    process.exit(0);
  }
  if (args.includes("--version") || args.includes("-v")) {
    console.log(VERSION);
    process.exit(0);
  }

  rejectInRepoNpmEntry();

  const useWebSocket = args.includes("--mcp-server");
  const portIndex = args.indexOf("--port");
  const port = portIndex !== -1 && args[portIndex + 1] ? parseInt(args[portIndex + 1], 10) : 5050;
  const wsHostIndex = args.indexOf("--host");
  const wsHost = wsHostIndex !== -1 && args[wsHostIndex + 1] ? args[wsHostIndex + 1] : "127.0.0.1";

  const rid = getRid();

  const dotnetArgs = [];
  if (useWebSocket) {
    dotnetArgs.push("--mcp-server", "--port", port.toString(), "--host", wsHost);
  }
  const consumed = new Set(["--mcp-server", "--port", "--host", port.toString(), wsHost]);
  dotnetArgs.push(...args.filter((a) => !consumed.has(a)));

  const resolved = await resolveHost(rid);
  if (resolved.kind === "launcher") {
    failCloneWithoutBinary(resolved.repoRoot);
    return;
  }

  const env = { ...process.env, OCCAM_HOME: resolved.home };
  if (!env.OCCAM_BANNER) {
    env.OCCAM_BANNER = "1";
  }

  spawnChild(resolved.binaryPath, dotnetArgs, {
    stdio: ["inherit", "inherit", "inherit"],
    env,
    windowsHide: true,
  });
}

main().catch((err) => {
  console.error(`[ff-occam/mcp] Fatal error: ${err.message}`);
  process.exit(1);
});
