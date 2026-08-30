#Requires -Version 5.1
<#
.SYNOPSIS
  Occam — one-liner bootstrap for Windows (irm | iex).

.DESCRIPTION
  Node 20+ only — NO git, NO .NET SDK on the install machine.
  Mirrors scripts/get-ff-occam.sh.
  Quiet by default. Set OCCAM_VERBOSE=1 for doctor/smoke internals.

.EXAMPLE
  irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex

.EXAMPLE
  $env:OCCAM_SETUP='manual'
  irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
#>
$ErrorActionPreference = "Stop"

# User-facing glyphs via codepoints only. Windows PowerShell 5.1 `irm | iex` often
# mis-decodes UTF-8 script bodies when the HTTP response omits charset=utf-8
# (literal U+2713 becomes mojibake "â" / U+2026 becomes "â¦"). Node later prints
# UTF-8 correctly — do not put raw multi-byte UTF-8 in Write-Host string literals.
$script:OccamOk = [string][char]0x2713       # check mark
$script:OccamFail = [string][char]0x2717     # ballot X
$script:OccamEllipsis = [string][char]0x2026 # horizontal ellipsis
$script:OccamBullet = [string][char]0x2022   # bullet
$script:OccamEmDash = [string][char]0x2014   # em dash

function Assert-PublishedRid([string]$Value) {
  if ($Value -notin @("win-x64", "linux-x64", "osx-arm64")) {
    throw "unsupported OCCAM_RID: $Value (published RIDs: win-x64, linux-x64, osx-arm64)"
  }
}

function Resolve-PublishedRid([string]$Os = "Windows_NT", [string]$Architecture = "") {
  if ([string]::IsNullOrWhiteSpace($Architecture)) {
    $Architecture = if ($env:PROCESSOR_ARCHITEW6432) {
      $env:PROCESSOR_ARCHITEW6432
    } elseif ($env:PROCESSOR_ARCHITECTURE) {
      $env:PROCESSOR_ARCHITECTURE
    } else {
      try { [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() }
      catch { "unknown" }
    }
  }
  $normalized = $Architecture.Trim().ToLowerInvariant()
  if ($Os -eq "Windows_NT" -and $normalized -in @("amd64", "x64", "x86_64")) {
    return "win-x64"
  }
  throw "no public Occam release for $Os/$Architecture (published RIDs: win-x64, linux-x64, osx-arm64)"
}

$Version = if ($env:OCCAM_VERSION) { $env:OCCAM_VERSION } else { "1.0.0" }
$Rid = if ($env:OCCAM_RID) { $env:OCCAM_RID } else { Resolve-PublishedRid }
Assert-PublishedRid $Rid
$InstallDir = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else {
  Join-Path $env:USERPROFILE ".local\share\ff-occam"
}
# Set from manifest runtimeLayout during install (never from version string).
$script:InstallContract = ""
# Legacy fallback for connection snippet only — never printed as a selected host before connect.
$HostTarget = if ($env:OCCAM_HOST) { $env:OCCAM_HOST } else { "" }
$AllowHttp = if ($env:OCCAM_RELEASE_ALLOW_HTTP) { $env:OCCAM_RELEASE_ALLOW_HTTP } else { "0" }
$SetupMode = if ($env:OCCAM_SETUP) { $env:OCCAM_SETUP.Trim().ToLowerInvariant() } else { "" }
$VerboseInstall = ($env:OCCAM_VERBOSE -eq "1" -or $env:OCCAM_VERBOSE -eq "true" -or
                   $env:OCCAM_DEBUG -eq "1" -or $env:OCCAM_DEBUG -eq "true")

$ReleaseBase = if ($env:OCCAM_RELEASE_BASE) {
  $env:OCCAM_RELEASE_BASE
} else {
  "https://github.com/ContextForgeAI/occam/releases/download/v$Version"
}
$ReleaseUrl = if ($env:OCCAM_RELEASE_URL) {
  $env:OCCAM_RELEASE_URL
} else {
  "$ReleaseBase/ff-occam-$Version-$Rid.tar.gz"
}
$ManifestUrl = if ($env:OCCAM_RELEASE_MANIFEST_URL) {
  $env:OCCAM_RELEASE_MANIFEST_URL
} else {
  "$ReleaseBase/ff-occam-$Version-$Rid-manifest.json"
}

$MinNodeMajor = 20
$script:InstallTransactionTarget = $null
$script:InstallTransactionBackup = $null
$script:InstallTransactionActive = $false
$script:InstallTransactionCommitted = $false

function Assert-UrlScheme([string]$Url) {
  if ($Url -match '^https://') { return }
  if ($Url -match '^http://') {
    if ($AllowHttp -eq "1") {
      Write-Warning ("OCCAM_RELEASE_ALLOW_HTTP=1 " + $script:OccamEmDash + " HTTP release URL")
      return
    }
    throw "release URL must be HTTPS, or set OCCAM_RELEASE_ALLOW_HTTP=1"
  }
  throw "invalid release URL: $Url"
}

function Test-NodeVersion {
  $node = Get-Command node -ErrorAction SilentlyContinue
  if (-not $node) { throw "required command not found: node (Node.js $MinNodeMajor+ required)" }
  $major = [int]((node -p "process.versions.node.split('.')[0]").Trim())
  if ($major -lt $MinNodeMajor) {
    throw "Node.js $MinNodeMajor+ required (found $(node -v))"
  }
  if ($VerboseInstall) { Write-Host "node: $(node -v)" }
}

function Assert-ReleaseArchivePreflight {
  param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$ExpectedRoot
  )

  $modulePath = $null
  if ($env:OCCAM_ARCHIVE_PREFLIGHT_PATH -and (Test-Path -LiteralPath $env:OCCAM_ARCHIVE_PREFLIGHT_PATH)) {
    $modulePath = $env:OCCAM_ARCHIVE_PREFLIGHT_PATH
  } elseif ($PSScriptRoot) {
    $candidate = Join-Path $PSScriptRoot "lib\archive-preflight.mjs"
    if (Test-Path -LiteralPath $candidate) { $modulePath = $candidate }
  }
  if (-not $modulePath -and $script:BootstrapTmpDir) {
    $dest = Join-Path $script:BootstrapTmpDir "archive-preflight.mjs"
    if (Test-Path -LiteralPath $dest) {
      $modulePath = $dest
    } else {
      $url = if ($env:OCCAM_ARCHIVE_PREFLIGHT_URL) {
        $env:OCCAM_ARCHIVE_PREFLIGHT_URL
      } else {
        "https://raw.githubusercontent.com/ContextForgeAI/occam/v$Version/scripts/lib/archive-preflight.mjs"
      }
      try {
        Download-File $url $dest
        $modulePath = $dest
      } catch {
        Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
      }
    }
  }
  if ($modulePath) {
    & node $modulePath --archive $ArchivePath --expected-root $ExpectedRoot
    if ($LASTEXITCODE -ne 0) { throw "archive preflight failed" }
    return
  }

  Write-Warning "archive-preflight.mjs unavailable; using tar listing fallback"
  $listing = Join-Path ([System.IO.Path]::GetTempPath()) ("ff-occam-tarlist-" + [guid]::NewGuid().ToString("N") + ".txt")
  try {
    $tarOut = & tar.exe -tvzf $ArchivePath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "unable to list archive members before extract" }
    Set-Content -LiteralPath $listing -Value ($tarOut | Out-String) -Encoding utf8

    if ($PSScriptRoot) {
      $listingModule = Join-Path $PSScriptRoot "lib\archive-preflight-listing.mjs"
      if (Test-Path -LiteralPath $listingModule) {
        & node $listingModule $listing $ExpectedRoot
        if ($LASTEXITCODE -ne 0) { throw "archive preflight failed" }
        return
      }
    }

    $nodeScript = @'
const fs = require("fs");
const [listingPath, expectedRoot] = process.argv.slice(2);
const lines = fs.readFileSync(listingPath, "utf8").split(/\r?\n/).filter(Boolean);
function nameStartIndex(parts) {
  if (parts.length >= 6 && String(parts[1] || "").includes("/")) return 5;
  if (parts.length >= 9 && /^\d+$/.test(String(parts[1] || ""))) return 8;
  if (parts.length >= 6) return 5;
  return -1;
}
function parseLine(line) {
  const type = line[0] || "";
  const arrow = line.indexOf(" -> ");
  if (arrow !== -1 && (type === "l" || type === "h")) {
    const left = line.slice(0, arrow).trim().split(/\s+/);
    const start = nameStartIndex(left);
    return { type, name: start >= 0 ? left.slice(start).join(" ") : "" };
  }
  const parts = line.trim().split(/\s+/);
  const start = nameStartIndex(parts);
  if (start < 0) return null;
  return { type, name: parts.slice(start).join(" ") };
}
function unsafePath(p) {
  const n = String(p || "").replace(/\\/g, "/");
  if (!n) return "empty archive member path";
  if (n.startsWith("/") || n.startsWith("~")) return `absolute archive member path: ${p}`;
  if (/^[A-Za-z]:(\/|$)/.test(n)) return `windows drive archive member path: ${p}`;
  if (n.startsWith("//")) return `unc archive member path: ${p}`;
  if (n.split("/").includes("..")) return `path traversal in archive member: ${p}`;
  return null;
}
const names = [];
for (const line of lines) {
  const parsed = parseLine(line);
  if (!parsed || !parsed.name) {
    console.error(`error: unable to parse archive member listing line: ${line}`);
    process.exit(1);
  }
  if (parsed.type === "l" || parsed.type === "h") {
    console.error(`error: ${parsed.type === "l" ? "symlink" : "hardlink"} archive members are not allowed: ${parsed.name}`);
    process.exit(1);
  }
  names.push(parsed.name.replace(/\\/g, "/"));
}
const roots = new Set();
for (const name of names) {
  const reason = unsafePath(name);
  if (reason) {
    console.error(`error: ${reason}`);
    process.exit(1);
  }
  const root = name.split("/").filter(Boolean)[0];
  if (root) roots.add(root);
}
if (!roots.has(expectedRoot)) {
  console.error(`error: missing expected archive root directory: ${expectedRoot}`);
  process.exit(1);
}
for (const root of roots) {
  if (root !== expectedRoot) {
    console.error(`error: unexpected archive root entries: ${root}`);
    process.exit(1);
  }
}
'@
    $inline = Join-Path ([System.IO.Path]::GetTempPath()) ("ff-occam-preflight-" + [guid]::NewGuid().ToString("N") + ".cjs")
    Set-Content -LiteralPath $inline -Value $nodeScript -Encoding utf8
    try {
      & node $inline $listing $ExpectedRoot
      if ($LASTEXITCODE -ne 0) { throw "archive preflight failed" }
    } finally {
      Remove-Item -LiteralPath $inline -Force -ErrorAction SilentlyContinue
    }
  } finally {
    Remove-Item -LiteralPath $listing -Force -ErrorAction SilentlyContinue
  }
}

function Test-TrulyInteractive {
  if ($env:CI -or $env:GITHUB_ACTIONS) { return $false }
  if (-not [Environment]::UserInteractive) { return $false }
  if ([Console]::IsInputRedirected) { return $false }
  if ([Console]::IsOutputRedirected) { return $false }
  return $true
}

function Resolve-SetupMode {
  # Contract: unset|auto|1 → auto; manual|2 → manual; ask → menu only if truly interactive else auto.
  if ($SetupMode -eq "" -or $SetupMode -eq "auto" -or $SetupMode -eq "1") {
    $script:SetupMode = "auto"
    if ($VerboseInstall) {
      if ($env:OCCAM_SETUP) { Write-Host "setup: auto (from OCCAM_SETUP)" }
      else { Write-Host "setup: auto (default)" }
    }
    return
  }
  if ($SetupMode -eq "manual" -or $SetupMode -eq "2") {
    $script:SetupMode = "manual"
    if ($VerboseInstall) { Write-Host "setup: manual (from OCCAM_SETUP)" }
    return
  }
  if ($SetupMode -ne "ask") {
    throw "OCCAM_SETUP must be auto|manual|ask (got $SetupMode)"
  }

  if (-not (Test-TrulyInteractive)) {
    $script:SetupMode = "auto"
    if ($VerboseInstall) { Write-Host "setup: auto (ask ignored: non-interactive)" }
    return
  }

  Write-Host ""
  Write-Host "  First-run setup"
  Write-Host ("  [1] Auto   " + $script:OccamEmDash + " detect and connect supported AI apps")
  Write-Host ("  [2] Manual " + $script:OccamEmDash + " choose which AI app to connect")
  Write-Host ""
  $choice = Read-Host "  Setup [1]"
  if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }
  if ($choice -match '^(2|manual)$') { $script:SetupMode = "manual" } else { $script:SetupMode = "auto" }
  if ($VerboseInstall) { Write-Host "setup: $($script:SetupMode)" }
}

function Get-Sha256Hex([string]$Path) {
  (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Download-File([string]$Url, [string]$Dest) {
  Assert-UrlScheme $Url
  if ($VerboseInstall) { Write-Host "download: $Url" }
  try {
    Invoke-WebRequest -Uri $Url -OutFile $Dest -UseBasicParsing
  } catch {
    Write-Error @"
download failed $($script:OccamEmDash) is the release tarball published?
  url: $Url
  maintainer: tag v$Version and ensure GitHub Release assets exist
  see: INSTALL.md
"@
    throw
  }
}

# Install ~/.local/bin/occam.cmd + occam.ps1 (+ User PATH) from the verified
# release archive. Prepends bin onto the current-process PATH.
function Install-OccamUserCommand([string]$OccamHome) {
  $helper = $null
  $helperTmp = $null
  $cliArgs = @()

  if ($script:InstallContract -eq "legacy") {
    $helperTmp = Join-Path ([System.IO.Path]::GetTempPath()) ("occam-install-user-cli-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path (Join-Path $helperTmp "scripts\lib\operator") -Force | Out-Null
    $overlayBase = if ($env:OCCAM_OVERLAY_BASE_URL) {
      $env:OCCAM_OVERLAY_BASE_URL.TrimEnd("/").TrimEnd("\")
    } else {
      "https://raw.githubusercontent.com/ContextForgeAI/occam/main"
    }
    try {
      Invoke-WebRequest -UseBasicParsing -Uri "$overlayBase/scripts/lib/operator/install-user-cli.mjs" `
        -OutFile (Join-Path $helperTmp "scripts\lib\operator\install-user-cli.mjs")
      Invoke-WebRequest -UseBasicParsing -Uri "$overlayBase/scripts/lib/resolve-node-runtime.mjs" `
        -OutFile (Join-Path $helperTmp "scripts\lib\resolve-node-runtime.mjs")
    } catch {
      if ($helperTmp) { Remove-Item -LiteralPath $helperTmp -Recurse -Force -ErrorAction SilentlyContinue }
      Write-Host ($script:OccamFail + " Could not install the occam command (download failed).") -ForegroundColor Red
      throw
    }
    $helper = Join-Path $helperTmp "scripts\lib\operator\install-user-cli.mjs"
    $cliArgs = @($helper, "--home", $OccamHome, "--base-url", $overlayBase, "--json")
  } else {
    $helper = Join-Path $OccamHome "scripts\lib\operator\install-user-cli.mjs"
    $nodeResolver = Join-Path $OccamHome "scripts\lib\resolve-node-runtime.mjs"
    if (-not (Test-Path -LiteralPath $helper) -or -not (Test-Path -LiteralPath $nodeResolver)) {
      Write-Host ($script:OccamFail + " Verified release archive is missing the occam command installer.") -ForegroundColor Red
      throw "verified release archive is missing the occam command installer"
    }
    $cliArgs = @($helper, "--home", $OccamHome, "--no-overlay", "--json")
  }

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $jsonOut = & node @cliArgs *>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) {
      Write-Host ($script:OccamFail + " Could not install the occam command.") -ForegroundColor Red
      Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for details." -ForegroundColor Yellow
      if ($jsonOut) { $jsonOut | Select-Object -Last 30 | ForEach-Object { Write-Host $_ } }
      throw "occam command installation failed (exit code $LASTEXITCODE)"
    }
    if ($VerboseInstall -and $jsonOut) {
      Write-Host ($jsonOut | Out-String)
    }
  } finally {
    $ErrorActionPreference = $prevEap
    if ($helperTmp) { Remove-Item -LiteralPath $helperTmp -Recurse -Force -ErrorAction SilentlyContinue }
  }

  $binDir = $null
  try {
    $parsed = ($jsonOut | Out-String) | ConvertFrom-Json
    $binDir = [string]$parsed.pathForCurrentProcess
    if (-not $binDir) { $binDir = [string]$parsed.binDir }
  } catch {
    $binDir = Join-Path $env:USERPROFILE ".local\bin"
  }

  if ($binDir) {
    $binDir = [System.IO.Path]::GetFullPath($binDir)
    $parts = @($env:PATH -split ';' | Where-Object { $_ -and $_.Trim() -ne '' })
    $hit = $false
    foreach ($p in $parts) {
      try {
        if ([System.IO.Path]::GetFullPath($p).Equals($binDir, [StringComparison]::OrdinalIgnoreCase)) {
          $hit = $true
          break
        }
      } catch {}
    }
    if (-not $hit) {
      # Prepend — same policy as User PATH persistence (see install-user-cli.mjs).
      $env:PATH = (@($binDir) + $parts) -join ';'
    } else {
      # Already present later in PATH — move to front for this process so we win
      # over an older OCCAM_HOME/scripts entry from prior manual installs.
      $rest = @($parts | Where-Object {
        try { -not [System.IO.Path]::GetFullPath($_).Equals($binDir, [StringComparison]::OrdinalIgnoreCase) }
        catch { $true }
      })
      $env:PATH = (@($binDir) + $rest) -join ';'
    }
  }

  # Drop cached command lookup so a stale miss from before PATH mutation is not reused.
  if (Get-Command occam -ErrorAction SilentlyContinue) {
    Remove-Item -Path "Function:occam" -ErrorAction SilentlyContinue
  }
  $cmd = Get-Command occam -ErrorAction SilentlyContinue
  if (-not $cmd) {
    Write-Host ($script:OccamFail + " occam command is not available on PATH after install.") -ForegroundColor Red
    Write-Host "Open a new PowerShell, or run: `$env:PATH = '$binDir;' + `$env:PATH" -ForegroundColor Yellow
    throw "occam command is not available on PATH after install"
  }
}

# Run a legacy-tarball child step. Pre-quiet release packs ignore -Quiet/--quiet
# flags (PowerShell also silently drops unknown -Quiet), so default mode MUST
# capture stdout/stderr. Checks still run; diagnostics show only on failure or verbose.
function Invoke-LegacyInstallStep {
  param(
    [Parameter(Mandatory = $true)][string]$Label,
    [Parameter(Mandatory = $true)][scriptblock]$Action
  )
  if ($VerboseInstall) {
    & $Action
    if ($LASTEXITCODE -ne 0) {
      Write-Host ($script:OccamFail + " $Label failed") -ForegroundColor Red
      Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for details." -ForegroundColor Yellow
      throw "$Label failed (exit code $LASTEXITCODE)"
    }
    return
  }

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    # *>&1 is required: doctor uses Write-Host (Information stream), which 2>&1 does not capture.
    $output = & $Action *>&1 | ForEach-Object { "$_" }
  } finally {
    $ErrorActionPreference = $prevEap
  }
  $code = $LASTEXITCODE
  if ($null -eq $code) { $code = 0 }
  if ($code -ne 0) {
    Write-Host ($script:OccamFail + " $Label failed") -ForegroundColor Red
    Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for full diagnostics." -ForegroundColor Yellow
    if ($output) {
      Write-Host ""
      $output | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
    }
    throw "$Label failed (exit code $code)"
  }
}

# Fail-closed: never pass "" / null / drive-root into destructive filesystem cmdlets.
function Assert-SafeInstallPath([string]$Path, [string]$Label) {
  if ([string]::IsNullOrWhiteSpace($Path)) {
    throw "$Label path is empty (internal installer error)"
  }
  $full = [System.IO.Path]::GetFullPath($Path)
  $root = [System.IO.Path]::GetPathRoot($full)
  if ([string]::IsNullOrWhiteSpace($root)) {
    throw "$Label path is invalid: $Path"
  }
  $normFull = $full.TrimEnd('\', '/')
  $normRoot = $root.TrimEnd('\', '/')
  if ($normFull -eq $normRoot) {
    throw ($Label + " path resolves to a drive root " + $script:OccamEmDash + " refusing: $Path")
  }
  if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    $normHome = [System.IO.Path]::GetFullPath($env:USERPROFILE).TrimEnd('\', '/')
    if ($normFull.Equals($normHome, [StringComparison]::OrdinalIgnoreCase)) {
      throw ($Label + " path resolves to the user profile " + $script:OccamEmDash + " refusing: $Path")
    }
  }
}

function Assert-TransactionBackupPath([string]$TargetDir, [string]$BackupDir) {
  if ([string]::IsNullOrWhiteSpace($BackupDir)) { return }
  $target = [System.IO.Path]::GetFullPath($TargetDir).TrimEnd('\', '/')
  $backup = [System.IO.Path]::GetFullPath($BackupDir).TrimEnd('\', '/')
  $sameParent = [System.IO.Path]::GetDirectoryName($target).Equals(
    [System.IO.Path]::GetDirectoryName($backup),
    [StringComparison]::OrdinalIgnoreCase
  )
  if (-not $sameParent -or -not $backup.StartsWith("$target.pre-replace-", [StringComparison]::OrdinalIgnoreCase)) {
    throw "backup path escaped the install transaction boundary: $backup"
  }
}

# Stop install-scoped Occam hosts before replacing the tree. Never deletes.
# Under `irm | iex`, $PSScriptRoot / $PSCommandPath are empty — never Join-Path them.
function Invoke-PrepareInstallReplace([string]$Dir, [string]$StagedDir = "") {
  Assert-SafeInstallPath $Dir "install"
  if (-not (Test-Path -LiteralPath $Dir)) { return $true }

  $helper = $null
  # Prefer the helper in the archive whose SHA-256 was just verified, then helpers
  # already on disk (file-mode bootstrap or the existing install tree).
  # Do NOT Join-Path $PSScriptRoot when empty — that throws under irm|iex (Path="").
  $candidates = New-Object System.Collections.Generic.List[string]
  if (-not [string]::IsNullOrWhiteSpace($StagedDir)) {
    $candidates.Add((Join-Path $StagedDir "scripts\lib\prepare-install-replace.mjs"))
  }
  if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $candidates.Add((Join-Path $PSScriptRoot "lib\prepare-install-replace.mjs"))
  }
  $candidates.Add((Join-Path $Dir "scripts\lib\prepare-install-replace.mjs"))
  foreach ($c in $candidates) {
    if (-not [string]::IsNullOrWhiteSpace($c) -and (Test-Path -LiteralPath $c)) {
      $helper = $c
      break
    }
  }
  if (-not $helper) {
    Write-Host "error: verified release archive is missing the install replacement helper" -ForegroundColor Red
    Write-Host "No files were changed."
    exit 1
  }

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $jsonOut = & node $helper --dir $Dir --rid $Rid --json *>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $prevEap
  }

  if ($code -eq 0) { return $true }

  $msg = $null
  try {
    $parsed = ($jsonOut | Out-String) | ConvertFrom-Json
    $msg = [string]$parsed.message
  } catch {}
  if (-not $msg) {
    $msg = @"
Occam is currently in use.

Close or restart these AI apps before updating:
$($script:OccamBullet) Any app that has Occam connected (Cursor, Claude Desktop, $($script:OccamEllipsis))

Then run the installer again.

No files were changed.
"@
  }
  Write-Host $msg
  exit 2
}

function Replace-OccamInstallTree([string]$TargetDir, [string]$StagedDir) {
  Assert-SafeInstallPath $TargetDir "install"
  Assert-SafeInstallPath $StagedDir "staging"
  $TargetDir = [System.IO.Path]::GetFullPath($TargetDir)
  $StagedDir = [System.IO.Path]::GetFullPath($StagedDir)
  $backup = $null
  $attempts = 3
  for ($i = 1; $i -le $attempts; $i++) {
    Invoke-PrepareInstallReplace $TargetDir $StagedDir | Out-Null

    if (-not (Test-Path -LiteralPath $TargetDir)) {
      break
    }

    $backup = "$TargetDir.pre-replace-$([guid]::NewGuid().ToString('N').Substring(0,8))"
    Assert-SafeInstallPath $backup "backup"
    try {
      Move-Item -LiteralPath $TargetDir -Destination $backup -Force -ErrorAction Stop
      break
    } catch {
      $backup = $null
      if ($i -eq $attempts) {
        Write-Host @"
Occam is currently in use.

The existing install could not be moved aside (file lock).
Close or restart these AI apps before updating:
$($script:OccamBullet) Cursor
$($script:OccamBullet) Claude Desktop
$($script:OccamBullet) Any other app with Occam connected

Then run the installer again.

No files were changed.
"@
        throw "existing install could not be moved aside after $attempts attempts"
      }
      Start-Sleep -Milliseconds 700
    }
  }

  try {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Get-ChildItem -LiteralPath $StagedDir | ForEach-Object {
      Move-Item -LiteralPath $_.FullName -Destination $TargetDir -Force -ErrorAction Stop
    }
  } catch {
    if ($backup -and (Test-Path -LiteralPath $backup)) {
      Assert-SafeInstallPath $TargetDir "install"
      Assert-SafeInstallPath $backup "backup"
      Remove-Item -Recurse -Force $TargetDir -ErrorAction SilentlyContinue
      Move-Item -LiteralPath $backup -Destination $TargetDir -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Install failed while replacing files." -ForegroundColor Red
    Write-Host "The previous Occam install was restored when possible." -ForegroundColor Yellow
    throw
  }

  $script:InstallTransactionTarget = $TargetDir
  $script:InstallTransactionBackup = $backup
  $script:InstallTransactionActive = $true
  $script:InstallTransactionCommitted = $false
}

function Stop-NewOccamInstallForRollback([string]$TargetDir) {
  $helper = Join-Path $TargetDir "scripts\lib\prepare-install-replace.mjs"
  if (-not (Test-Path -LiteralPath $helper -PathType Leaf)) {
    Write-Host "error: rollback helper is missing from the failed install: $helper" -ForegroundColor Red
    return $false
  }
  $previousPreference = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $output = & node $helper --dir $TargetDir --rid $Rid --json *>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $previousPreference
  }
  if ($code -eq 0) { return $true }
  Write-Host "error: rollback could not stop every process using the new install." -ForegroundColor Red
  if ($output) { $output | Select-Object -Last 30 | ForEach-Object { Write-Host $_ } }
  return $false
}

function Restore-OccamInstallTransaction {
  if (-not $script:InstallTransactionActive) { return $true }
  $target = [string]$script:InstallTransactionTarget
  $backup = [string]$script:InstallTransactionBackup
  Assert-SafeInstallPath $target "install"
  Assert-TransactionBackupPath $target $backup

  Write-Host "Install validation failed after the release tree was replaced." -ForegroundColor Red
  Write-Host "Stopping processes started from the new install before rollback..." -ForegroundColor Yellow
  if (-not (Stop-NewOccamInstallForRollback $target)) {
    Write-Host "The failed install was preserved at: $target" -ForegroundColor Yellow
    if ($backup) { Write-Host "The previous install backup was preserved at: $backup" -ForegroundColor Yellow }
    Write-Host "Close AI apps using Occam, then move the backup back into place." -ForegroundColor Yellow
    return $false
  }

  try {
    $rollbackParent = [System.IO.Path]::GetDirectoryName($target)
    if ([string]::IsNullOrWhiteSpace($rollbackParent)) {
      throw "rollback parent path is empty (internal installer error)"
    }
    Set-Location -LiteralPath $rollbackParent
    if (Test-Path -LiteralPath $target) {
      Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
    }
    if ($backup -and (Test-Path -LiteralPath $backup)) {
      Move-Item -LiteralPath $backup -Destination $target -Force -ErrorAction Stop
      Write-Host "The previous Occam install was restored: $target" -ForegroundColor Yellow
    } else {
      Write-Host "The failed fresh Occam install was removed: $target" -ForegroundColor Yellow
    }
  } catch {
    Write-Host "error: rollback could not restore the previous install: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Failed install: $target" -ForegroundColor Yellow
    if ($backup) { Write-Host "Previous install backup: $backup" -ForegroundColor Yellow }
    return $false
  }

  $script:InstallTransactionActive = $false
  $script:InstallTransactionTarget = $null
  $script:InstallTransactionBackup = $null
  return $true
}

function Complete-OccamInstallTransaction {
  if (-not $script:InstallTransactionActive) { return }
  $target = [string]$script:InstallTransactionTarget
  $backup = [string]$script:InstallTransactionBackup
  Assert-SafeInstallPath $target "install"
  Assert-TransactionBackupPath $target $backup
  $script:InstallTransactionCommitted = $true
  if ($backup -and (Test-Path -LiteralPath $backup)) {
    try {
      Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction Stop
    } catch {
      Write-Host "warning: install succeeded, but the previous-tree backup could not be removed: $backup" -ForegroundColor Yellow
      Write-Host "Review that exact path and remove it manually after confirming Occam works." -ForegroundColor Yellow
    }
  }
  $script:InstallTransactionActive = $false
  $script:InstallTransactionTarget = $null
  $script:InstallTransactionBackup = $null
}

Resolve-SetupMode
Test-NodeVersion

if (-not (Get-Command tar.exe -ErrorAction SilentlyContinue)) {
  throw "required command not found: tar.exe"
}

if ($VerboseInstall) {
  Write-Host ""
  Write-Host "install_dir: $InstallDir"
  if ($HostTarget) { Write-Host "host_hint: $HostTarget" }
  Write-Host "release_url: $ReleaseUrl"
  Write-Host ""
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("ff-occam-get-" + [guid]::NewGuid().ToString("N"))
$script:BootstrapTmpDir = $tmp
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
try {
  $manifestPath = Join-Path $tmp "manifest.json"
  $tarballPath = Join-Path $tmp "release.tar.gz"

  Download-File $ManifestUrl $manifestPath
  $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
  $expectedTarball = "ff-occam-$Version-$Rid.tar.gz"
  if ([string]$manifest.version -cne $Version) {
    throw "release manifest version mismatch (expected $Version, got $($manifest.version))"
  }
  if ([string]$manifest.rid -cne $Rid) {
    throw "release manifest RID mismatch (expected $Rid, got $($manifest.rid))"
  }
  $manifestTarball = if ($null -ne $manifest.PSObject.Properties["tarball"]) { [string]$manifest.tarball } else { "" }
  if ($manifestTarball -and $manifestTarball -cne $expectedTarball) {
    throw "release manifest tarball mismatch (expected $expectedTarball, got $manifestTarball)"
  }
  $runtimeLayout = if ($null -ne $manifest.PSObject.Properties["runtimeLayout"] -and
    -not [string]::IsNullOrWhiteSpace([string]$manifest.runtimeLayout)) {
    [string]$manifest.runtimeLayout
  } else {
    ""
  }
  if (-not $runtimeLayout) {
    $script:InstallContract = "legacy"
  } elseif ($runtimeLayout -ceq "self-contained-v1") {
    $script:InstallContract = "self-contained-v1"
  } else {
    throw "unsupported release runtimeLayout: $runtimeLayout"
  }
  $expectedSha = [string]$manifest.sha256
  if ($expectedSha -notmatch '^[0-9A-Fa-f]{64}$') {
    throw "release manifest sha256 must be 64 hexadecimal characters"
  }
  $expectedSha = $expectedSha.ToLowerInvariant()

  Download-File $ReleaseUrl $tarballPath
  $actualSha = Get-Sha256Hex $tarballPath
  if ($actualSha -ne $expectedSha) {
    throw "sha256 mismatch`n  expected: $expectedSha`n  actual:   $actualSha"
  }
  if ($VerboseInstall) {
    Write-Host "sha256: OK"
    Write-Host "release: version=$($manifest.version) rid=$($manifest.rid) contract=$($script:InstallContract)"
  }

  $signaturePolicy = if ($null -ne $manifest.PSObject.Properties["signaturePolicy"] -and
    -not [string]::IsNullOrWhiteSpace([string]$manifest.signaturePolicy)) {
    [string]$manifest.signaturePolicy
  } else {
    "sha256-only"
  }
  switch ($signaturePolicy) {
    "sha256-only" { }
    "required-cosign-v1" {
      $bundlePath = Join-Path $tmp "release.tar.gz.bundle"
      $bundleUrl = if ($env:OCCAM_RELEASE_BUNDLE_URL) {
        $env:OCCAM_RELEASE_BUNDLE_URL
      } else {
        "$ReleaseUrl.bundle"
      }
      Download-File $bundleUrl $bundlePath
      $repoPreflight = Join-Path $PSScriptRoot "lib\verify-release-signature.mjs"
      if ($PSScriptRoot -and (Test-Path -LiteralPath $repoPreflight)) {
        & node $repoPreflight --manifest $manifestPath --archive $tarballPath --bundle $bundlePath --version $Version
        if ($LASTEXITCODE -ne 0) { throw "release signature verification failed" }
      } else {
        if (-not (Get-Command cosign -ErrorAction SilentlyContinue)) {
          throw @"
signaturePolicy=required-cosign-v1 requires the cosign CLI on PATH.

Install Cosign, then re-run the Occam bootstrap:
  https://docs.sigstore.dev/cosign/system_config/installation/

Authenticity checks prove release signer identity — not page-content truth.
See INSTALL.md (Cosign / signaturePolicy).
"@
        }
        $expectedIdentity = "https://github.com/ContextForgeAI/occam/.github/workflows/occam-release.yml@refs/tags/v$Version"
        & cosign verify-blob $tarballPath `
          --bundle $bundlePath `
          --certificate-identity $expectedIdentity `
          --certificate-oidc-issuer "https://token.actions.githubusercontent.com"
        if ($LASTEXITCODE -ne 0) { throw "cosign verify-blob failed" }
      }
    }
    default { throw "unsupported release signaturePolicy: $signaturePolicy" }
  }

  if ($script:InstallContract -eq "self-contained-v1") {
    $expectedRoot = "ff-occam-$Version-$Rid"
    Assert-ReleaseArchivePreflight -ArchivePath $tarballPath -ExpectedRoot $expectedRoot
  }

  $parent = Split-Path -Parent $InstallDir
  if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }

  $extractTmp = Join-Path $tmp "extract"
  New-Item -ItemType Directory -Path $extractTmp -Force | Out-Null
  tar.exe -xzf $tarballPath -C $extractTmp
  $inner = Get-ChildItem $extractTmp | Select-Object -First 1
  if ($null -eq $inner) { throw "empty tarball" }
  $staged = $extractTmp
  if ($inner.PSIsContainer -and (@(Get-ChildItem $extractTmp).Count -eq 1)) {
    $staged = $inner.FullName
  }

  if ($script:InstallContract -eq "self-contained-v1") {
    $runtimeChecker = Join-Path $staged "scripts\lib\operator\install-user-cli.mjs"
    if (-not (Test-Path -LiteralPath $runtimeChecker)) {
      throw "verified release archive is incomplete (missing runtime checker); self-contained does not fall back to legacy overlay"
    }
    & node $runtimeChecker --check-release-root $staged --version $Version --rid $Rid
    if ($LASTEXITCODE -ne 0) {
      throw "verified release archive is incomplete; self-contained does not fall back to legacy overlay"
    }
  }

  # Download/stage first → stop install-scoped hosts → swap. Never delete before stage is ready.
  Replace-OccamInstallTree -TargetDir $InstallDir -StagedDir $staged
  if ($VerboseInstall) { Write-Host "extracted: $InstallDir" }

  $env:OCCAM_HOME = $InstallDir
  Set-Location $InstallDir
  if ($HostTarget) { $env:OCCAM_HOST = $HostTarget }

  $postUx = Join-Path $InstallDir "scripts\lib\operator\post-install-ux.mjs"
  $postArgs = @($postUx, "--setup", $SetupMode, "--version", $Version, "--download-ok")
  if ($VerboseInstall) { $postArgs += "--verbose" }

  if (Test-Path $postUx) {
    & node @postArgs
    if ($LASTEXITCODE -ne 0) { throw "post-install setup failed (exit code $LASTEXITCODE)" }
    Install-OccamUserCommand $InstallDir
  } else {
    # Legacy release tarball (no post-install-ux.mjs): quiet by capturing child I/O.
    # Published v1.0.0-rc.2 packs predate quiet doctor/verify/smoke flags.
    Write-Host ""
    Write-Host "Occam $Version"
    Write-Host ""
    Write-Host "Installing Occam"
    Write-Host ($script:OccamOk + " Download verified")
    $env:OCCAM_INSTALL_QUIET = if ($VerboseInstall) { "0" } else { "1" }
    $env:OCCAM_BANNER = "0"
    $env:WT_OCCAM_BANNER = "0"

    Write-Host ("  Installing runtime" + $script:OccamEllipsis)
    $doctorPs1 = Join-Path $InstallDir "scripts\occam-doctor.ps1"
    Invoke-LegacyInstallStep -Label "Runtime setup (doctor)" -Action {
      & $doctorPs1 -SkipBuild
    }
    Write-Host ($script:OccamOk + " Runtime installed")
    Write-Host ($script:OccamOk + " Browser ready")

    Write-Host ("  Running self-check" + $script:OccamEllipsis)
    $verifyJs = Join-Path $InstallDir "scripts\lib\verify-install.mjs"
    Invoke-LegacyInstallStep -Label "Host verify" -Action {
      & node $verifyJs --skip-build --version $Version
    }

    $smokeJs = Join-Path $InstallDir "scripts\hermes-smoke.mjs"
    Invoke-LegacyInstallStep -Label "Self-check" -Action {
      & node $smokeJs
    }
    Write-Host ($script:OccamOk + " Self-check passed")

    # Continue into the SAME connect onboarding as modern post-install-ux.
    $connectJs = Join-Path $InstallDir "scripts\occam-connect.mjs"
    if (Test-Path $connectJs) {
      $connectArgs = @($connectJs)
      if ($VerboseInstall) { $connectArgs += "--verbose" }
      & node @connectArgs
      if ($LASTEXITCODE -ne 0) { throw "connect failed (exit code $LASTEXITCODE)" }
    } else {
      Write-Host ""
      Write-Host "Occam is installed."
      Write-Host ""
      Write-Host "Connect an AI app later with:"
      Write-Host "  occam connect"
    }

    # Install the launcher only after every doctor/smoke/connect step succeeds.
    Install-OccamUserCommand $InstallDir
  }

  Complete-OccamInstallTransaction
} catch {
  $originalFailure = $_
  if ($script:InstallTransactionActive -and -not $script:InstallTransactionCommitted) {
    $restored = Restore-OccamInstallTransaction
    if (-not $restored) {
      throw "install failed and automatic rollback could not complete. Original failure: $($originalFailure.Exception.Message)"
    }
  }
  throw $originalFailure
} finally {
  Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
