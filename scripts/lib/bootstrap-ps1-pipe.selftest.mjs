#!/usr/bin/env node
/**
 * Regression: get-ff-occam.ps1 must work under real `irm | iex` semantics where
 * $PSScriptRoot / $PSCommandPath are empty. File-mode (-File) alone is insufficient.
 *
 * The static fixture server runs in a child process so spawnSync(powershell) cannot
 * deadlock the HTTP event loop.
 */
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:http";
import { extname } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, "..", "..");

function contentType(filePath) {
  switch (extname(filePath)) {
    case ".ps1":
      // Match python -m http.server / typical static hosts: no charset.
      // PS 5.1 irm then mis-decodes UTF-8 literals — bootstrap must use codepoints.
      return "text/plain";
    case ".mjs":
    case ".js":
      return "text/javascript; charset=utf-8";
    default:
      return "application/octet-stream";
  }
}

/** Child-process entry: node bootstrap-ps1-pipe.selftest.mjs --serve <repoRoot> */
function runServeMode() {
  const root = process.argv[3] || repoRoot;
  const server = createServer((req, res) => {
    try {
      const urlPath = decodeURIComponent((req.url || "/").split("?")[0]);
      const rel = urlPath.replace(/^\/+/, "").replace(/\.\./g, "");
      const abs = join(root, rel);
      if (!abs.startsWith(root)) {
        res.writeHead(403);
        res.end("forbidden");
        return;
      }
      const body = readFileSync(abs);
      res.writeHead(200, { "Content-Type": contentType(abs) });
      res.end(body);
    } catch {
      res.writeHead(404);
      res.end("not found");
    }
  });
  server.listen(0, "127.0.0.1", () => {
    const { port } = server.address();
    process.stdout.write(`READY ${port}\n`);
  });
}

function startRepoServerChild() {
  return new Promise((resolve, reject) => {
    let settled = false;
    const child = spawn(process.execPath, [fileURLToPath(import.meta.url), "--serve", repoRoot], {
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
    });
    let buf = "";
    const onData = (chunk) => {
      buf += String(chunk);
      const m = buf.match(/READY (\d+)/);
      if (!m || settled) return;
      settled = true;
      child.stdout.off("data", onData);
      const port = Number(m[1]);
      resolve({
        base: `http://127.0.0.1:${port}`,
        close: () => {
          try {
            child.kill();
          } catch {
            /* ignore */
          }
        },
      });
    };
    child.stdout.on("data", onData);
    child.stderr.on("data", (c) => {
      buf += String(c);
    });
    child.on("error", (err) => {
      if (!settled) {
        settled = true;
        reject(err);
      }
    });
    child.on("exit", (code) => {
      if (!settled) {
        settled = true;
        reject(new Error(`fixture server exited early (code=${code}): ${buf}`));
      }
    });
    setTimeout(() => {
      if (!settled) {
        settled = true;
        try {
          child.kill();
        } catch {
          /* ignore */
        }
        reject(new Error(`fixture server timeout: ${buf}`));
      }
    }, 15_000).unref();
  });
}

function powershell(args, envExtra = {}) {
  return spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", ...args], {
    encoding: "utf8",
    env: { ...process.env, ...envExtra },
    cwd: repoRoot,
    timeout: 90_000,
  });
}

function assertPsOk(r, label) {
  const out = `${r.stdout || ""}\n${r.stderr || ""}`;
  if (r.error) {
    throw new Error(`${label} spawn error: ${r.error.message}\n${out}`);
  }
  if (r.status !== 0) {
    throw new Error(`${label} failed (status=${r.status}):\n${out}`);
  }
  return out;
}

function extractBootstrapHead(src) {
  const m = src.match(/\r?\nResolve-SetupMode\r?\n/);
  assert.ok(m, "get-ff-occam.ps1 must call Resolve-SetupMode at top level");
  return src.slice(0, m.index);
}

function testSourceGuards() {
  const ps1 = readFileSync(join(repoRoot, "scripts", "get-ff-occam.ps1"), "utf8");
  assert.doesNotMatch(
    ps1,
    /\$candidates\s*=\s*@\(\s*\r?\n\s*\(Join-Path \$PSScriptRoot/,
  );
  assert.match(ps1, /IsNullOrWhiteSpace\(\$PSScriptRoot\)/);
  assert.match(ps1, /Assert-SafeInstallPath/);
  assert.match(ps1, /OCCAM_OVERLAY_BASE_URL/);
  // Pipe-safe Unicode: no raw checkmark/ellipsis in executable strings.
  assert.doesNotMatch(ps1, /[✓✗•…]/);
  assert.match(ps1, /\[char\]0x2713/);
  assert.match(ps1, /\[char\]0x2026/);
  assert.match(ps1, /\$script:OccamOk/);
}

function testFileModePrepare() {
  const runner = join(repoRoot, "scripts", "_tmp-file-prepare-probe.ps1");
  const probeRoot = mkdtempSync(join(tmpdir(), "occam-file-probe-"));
  try {
    writeFileSync(join(probeRoot, "OccamMcp.Core.exe"), "fake");
    const boot = readFileSync(join(repoRoot, "scripts", "get-ff-occam.ps1"), "utf8");
    const head = extractBootstrapHead(boot);
    writeFileSync(
      runner,
      `${head}
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($PSScriptRoot)) { throw "expected non-empty PSScriptRoot under -File" }
$probeDir = '${probeRoot.replace(/'/g, "''")}'
$result = Invoke-PrepareInstallReplace $probeDir
if (-not $result) { throw "Invoke-PrepareInstallReplace returned false" }
Write-Output ("FILE_PSScriptRoot=[" + $PSScriptRoot + "]")
Write-Output "FILE_PREPARE_OK"
`,
      "utf8",
    );
    const r = powershell(["-File", runner]);
    const out = assertPsOk(r, "file-mode prepare probe");
    assert.match(out, /FILE_PREPARE_OK/);
    assert.match(out, /FILE_PSScriptRoot=\[.+/);
  } finally {
    rmSync(probeRoot, { recursive: true, force: true });
    try {
      rmSync(runner, { force: true });
    } catch {
      /* ignore */
    }
  }
}

/**
 * Product path: fetch bootstrap over HTTP and Invoke-Expression it (empty PSScriptRoot).
 * Truncate before top-level install body; exercise Invoke-PrepareInstallReplace only.
 */
async function testProductStyleIrmIex() {
  const srv = await startRepoServerChild();
  const probeRoot = mkdtempSync(join(tmpdir(), "occam-product-pipe-"));
  const probePs1 = join(probeRoot, "probe.ps1");
  try {
    writeFileSync(join(probeRoot, "OccamMcp.Core.exe"), "fake");
    writeFileSync(
      probePs1,
      `
$ErrorActionPreference = 'Stop'
$env:OCCAM_OVERLAY_BASE_URL = '${srv.base}'
$raw = (Invoke-WebRequest -Uri ($env:OCCAM_OVERLAY_BASE_URL + '/scripts/get-ff-occam.ps1') -UseBasicParsing).Content
$m = [regex]::Match($raw, '(?m)^Resolve-SetupMode\\s*$')
if (-not $m.Success) { throw 'Resolve-SetupMode call not found' }
$probeDir = '${probeRoot.replace(/'/g, "''")}'
$tail = @'
Write-Output ("PSScriptRoot=[" + $PSScriptRoot + "]")
Write-Output ("PSCommandPath=[" + $PSCommandPath + "]")
if (-not [string]::IsNullOrEmpty($PSScriptRoot)) { throw "PSScriptRoot not empty under iex" }
if (-not [string]::IsNullOrEmpty($PSCommandPath)) { throw "PSCommandPath not empty under iex" }
$r = Invoke-PrepareInstallReplace 'REPLACE_PROBE_DIR'
if (-not $r) { throw "prepare returned false" }
Write-Output "PIPE_PREPARE_OK"
'@
$tail = $tail.Replace('REPLACE_PROBE_DIR', $probeDir)
Invoke-Expression ($raw.Substring(0, $m.Index) + [Environment]::NewLine + $tail)
`.trimStart(),
      "utf8",
    );

    const r = powershell([
      "-Command",
      `Invoke-Expression (Get-Content -LiteralPath '${probePs1.replace(/'/g, "''")}' -Raw)`,
    ]);
    const out = assertPsOk(r, "product-style irm|iex");
    assert.match(out, /PSScriptRoot=\[\]/);
    assert.match(out, /PSCommandPath=\[\]/);
    assert.match(out, /PIPE_PREPARE_OK/);
    assert.doesNotMatch(out, /Cannot bind argument to parameter 'Path'/);
  } finally {
    rmSync(probeRoot, { recursive: true, force: true });
    srv.close();
  }
}

/**
 * Early install progress glyphs must survive irm|iex under PS 5.1 without charset.
 * Assert in-memory codepoints (not console encoding) and reject mojibake fragments.
 */
async function testUnicodeProgressUnderIrmIex() {
  const srv = await startRepoServerChild();
  const probeRoot = mkdtempSync(join(tmpdir(), "occam-unicode-pipe-"));
  const outFile = join(probeRoot, "progress-utf8.txt");
  const probePs1 = join(probeRoot, "unicode-probe.ps1");
  try {
    writeFileSync(
      probePs1,
      `
$ErrorActionPreference = 'Stop'
$env:OCCAM_OVERLAY_BASE_URL = '${srv.base}'
$raw = (Invoke-WebRequest -Uri ($env:OCCAM_OVERLAY_BASE_URL + '/scripts/get-ff-occam.ps1') -UseBasicParsing).Content
$m = [regex]::Match($raw, '(?m)^Resolve-SetupMode\\s*$')
if (-not $m.Success) { throw 'Resolve-SetupMode call not found' }
Invoke-Expression $raw.Substring(0, $m.Index)
if ($script:OccamOk -ne [string][char]0x2713) { throw 'OccamOk codepoint lost after irm|iex' }
if ($script:OccamEllipsis -ne [string][char]0x2026) { throw 'OccamEllipsis codepoint lost after irm|iex' }
$lines = @(
  ($script:OccamOk + ' Download verified'),
  ('  Installing runtime' + $script:OccamEllipsis),
  ($script:OccamOk + ' Runtime installed'),
  ($script:OccamOk + ' Browser ready'),
  ('  Running self-check' + $script:OccamEllipsis),
  ($script:OccamOk + ' Self-check passed')
)
foreach ($line in $lines) {
  if ($line.IndexOf([char]0x00E2) -ge 0) { throw ('mojibake a-circumflex in: ' + $line) }
  if ($line.IndexOf([char]0x00C3) -ge 0) { throw ('mojibake A-tilde in: ' + $line) }
  if ($line.IndexOf([char]0x00C2) -ge 0) { throw ('mojibake A-circumflex in: ' + $line) }
}
if ($lines[0][0] -ne [char]0x2713) { throw 'checkmark missing on Download verified' }
if (-not $lines[1].EndsWith([char]0x2026)) { throw 'ellipsis missing on Installing runtime' }
[System.IO.File]::WriteAllLines('${outFile.replace(/\\/g, "\\\\")}', $lines, (New-Object System.Text.UTF8Encoding $false))
Write-Output 'UNICODE_PROGRESS_OK'
`.trimStart(),
      "utf8",
    );

    const r = powershell([
      "-Command",
      `Invoke-Expression (Get-Content -LiteralPath '${probePs1.replace(/'/g, "''")}' -Raw)`,
    ]);
    const out = assertPsOk(r, "unicode progress irm|iex");
    assert.match(out, /UNICODE_PROGRESS_OK/);
    assert.doesNotMatch(out, /mojibake/);
    const progress = readFileSync(outFile, "utf8");
    assert.match(progress, /^✓ Download verified$/m);
    assert.match(progress, /^ {2}Installing runtime…$/m);
    assert.match(progress, /^✓ Self-check passed$/m);
    assert.doesNotMatch(progress, /â/);
    assert.doesNotMatch(progress, /Ã/);
    assert.doesNotMatch(progress, /Â/);
  } finally {
    rmSync(probeRoot, { recursive: true, force: true });
    srv.close();
  }
}

async function main() {
  if (process.argv[2] === "--serve") {
    runServeMode();
    return;
  }
  if (process.platform !== "win32") {
    console.log("bootstrap-ps1-pipe.selftest: SKIP (Windows only)");
    return;
  }
  testSourceGuards();
  console.log("  source guards OK");
  testFileModePrepare();
  console.log("  file-mode OK");
  await testProductStyleIrmIex();
  console.log("  irm|iex OK");
  await testUnicodeProgressUnderIrmIex();
  console.log("  unicode progress OK");
  console.log("bootstrap-ps1-pipe.selftest: OK");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
