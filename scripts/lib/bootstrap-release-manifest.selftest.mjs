#!/usr/bin/env node
/**
 * Negative integration tests for bootstrap release-manifest validation.
 * Each case must fail before the archive URL is requested.
 */
import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:http";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..");
const version = "9.8.7";
const rid =
  process.platform === "win32"
    ? "win-x64"
    : process.platform === "darwin"
      ? "osx-arm64"
      : "linux-x64";
const tarball = `ff-occam-${version}-${rid}.tar.gz`;
const sha256 = "0".repeat(64);
const runtimeLayout = "self-contained-v1";

const cases = Object.freeze({
  version: {
    manifest: { version: "9.8.6", rid, tarball, sha256, runtimeLayout },
    expected: /release manifest version mismatch/,
  },
  rid: {
    manifest: {
      version,
      rid: rid === "win-x64" ? "linux-x64" : "win-x64",
      tarball,
      sha256,
      runtimeLayout,
    },
    expected: /release manifest RID mismatch/,
  },
  tarball: {
    manifest: { version, rid, tarball: "wrong.tar.gz", sha256, runtimeLayout },
    expected: /release manifest tarball mismatch/,
  },
  sha: {
    manifest: { version, rid, tarball, sha256: "not-a-sha", runtimeLayout },
    expected: /release manifest sha256 must be 64 hexadecimal characters/,
  },
  unknownLayout: {
    manifest: { version, rid, tarball, sha256, runtimeLayout: "experimental-v9" },
    expected: /unsupported release runtimeLayout/,
  },
});

function runServeMode() {
  let archiveRequests = 0;
  const server = createServer((req, res) => {
    if (req.url === "/stats") {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(`${JSON.stringify({ archiveRequests })}\n`);
      return;
    }
    if (req.url === "/archive.tar.gz") {
      archiveRequests += 1;
      res.writeHead(409, { "Content-Type": "text/plain" });
      res.end("archive must not be requested after an invalid manifest");
      return;
    }
    const name = String(req.url || "").replace(/^\//, "").replace(/\.json$/, "");
    const fixture = cases[name];
    if (!fixture) {
      res.writeHead(409, { "Content-Type": "text/plain" });
      res.end("archive must not be requested after an invalid manifest");
      return;
    }
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(`${JSON.stringify(fixture.manifest)}\n`);
  });
  server.listen(0, "127.0.0.1", () => {
    process.stdout.write(`READY ${server.address().port}\n`);
  });
}

function startFixtureServer() {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [fileURLToPath(import.meta.url), "--serve"], {
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
    });
    let output = "";
    const timeout = setTimeout(() => {
      child.kill();
      reject(new Error(`fixture server timeout: ${output}`));
    }, 15_000);
    timeout.unref();
    child.stdout.on("data", (chunk) => {
      output += String(chunk);
      const match = output.match(/READY (\d+)/);
      if (!match) return;
      clearTimeout(timeout);
      resolve({
        baseUrl: `http://127.0.0.1:${match[1]}`,
        close: () => child.kill(),
      });
    });
    child.stderr.on("data", (chunk) => {
      output += String(chunk);
    });
    child.on("error", reject);
  });
}

function runBootstrap(baseUrl, caseName, installDir) {
  const env = {
    ...process.env,
    OCCAM_VERSION: version,
    OCCAM_RID: rid,
    OCCAM_RELEASE_ALLOW_HTTP: "1",
    OCCAM_RELEASE_MANIFEST_URL: `${baseUrl}/${caseName}.json`,
    OCCAM_RELEASE_URL: `${baseUrl}/archive.tar.gz`,
    OCCAM_INSTALL_DIR: installDir,
    OCCAM_SETUP: "auto",
  };
  if (process.platform === "win32") {
    return spawnSync(
      "powershell.exe",
      [
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        join(repoRoot, "scripts", "get-ff-occam.ps1"),
      ],
      { cwd: repoRoot, env, encoding: "utf8", timeout: 30_000 },
    );
  }
  return spawnSync("bash", [join(repoRoot, "scripts", "get-ff-occam.sh")], {
    cwd: repoRoot,
    env,
    encoding: "utf8",
    timeout: 30_000,
  });
}

function testPowerShellSourceGuards() {
  const ps1 = readFileSync(join(repoRoot, "scripts", "get-ff-occam.ps1"), "utf8");
  const versionCheck = ps1.indexOf("[string]$manifest.version -cne $Version");
  const ridCheck = ps1.indexOf("[string]$manifest.rid -cne $Rid");
  const layoutFailClosed = ps1.indexOf("unsupported release runtimeLayout");
  const contractLegacy = ps1.indexOf('$script:InstallContract = "legacy"');
  const contractSelf = ps1.indexOf('$script:InstallContract = "self-contained-v1"');
  const shaCheck = ps1.indexOf("$expectedSha -notmatch '^[0-9A-Fa-f]{64}$'");
  const archiveDownload = ps1.indexOf("Download-File $ReleaseUrl $tarballPath");
  assert.ok(archiveDownload >= 0, "PowerShell bootstrap archive download call not found");
  for (const [label, index] of [
    ["version", versionCheck],
    ["rid", ridCheck],
    ["runtime layout fail-closed", layoutFailClosed],
    ["legacy contract", contractLegacy],
    ["self-contained contract", contractSelf],
    ["sha256", shaCheck],
  ]) {
    assert.ok(index >= 0, `PowerShell bootstrap is missing ${label} manifest validation`);
    assert.ok(index < archiveDownload, `PowerShell ${label} validation must precede archive download`);
  }
  assert.match(ps1, /1\.0\.0/, "public default release must be published 1.0.0");
  console.log("ok: PowerShell source guards cover identity/layout/sha before download");
}

async function main() {
  if (process.argv[2] === "--serve") {
    runServeMode();
    return;
  }

  testPowerShellSourceGuards();
  const fixture = await startFixtureServer();
  const installDir = mkdtempSync(join(tmpdir(), "occam-invalid-manifest-"));
  try {
    for (const [name, testCase] of Object.entries(cases)) {
      const result = runBootstrap(fixture.baseUrl, name, installDir);
      const output = `${result.stdout || ""}\n${result.stderr || ""}`;
      assert.notEqual(result.status, 0, `${name} mismatch unexpectedly passed:\n${output}`);
      assert.match(output, testCase.expected, `${name} mismatch did not fail explicitly`);
      assert.doesNotMatch(output, /download failed/);
      assert.doesNotMatch(output, /archive must not be requested/);
      const statsResponse = await fetch(`${fixture.baseUrl}/stats`);
      assert.equal(statsResponse.status, 200);
      const stats = await statsResponse.json();
      assert.equal(stats.archiveRequests, 0, `${name} mismatch requested the archive`);
      console.log(`ok: ${name} mismatch rejected`);
    }
    console.log("bootstrap-release-manifest.selftest: OK");
  } finally {
    rmSync(installDir, { recursive: true, force: true });
    fixture.close();
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
