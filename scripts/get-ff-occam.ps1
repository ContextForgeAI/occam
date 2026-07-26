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

$Version = if ($env:OCCAM_VERSION) { $env:OCCAM_VERSION } else { "1.0.0-rc.2" }
$Rid = if ($env:OCCAM_RID) { $env:OCCAM_RID } else { "win-x64" }
$InstallDir = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else {
  Join-Path $env:USERPROFILE ".local\share\ff-occam"
}
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

function Assert-UrlScheme([string]$Url) {
  if ($Url -match '^https://') { return }
  if ($Url -match '^http://') {
    if ($AllowHttp -eq "1") {
      Write-Warning "OCCAM_RELEASE_ALLOW_HTTP=1 — HTTP release URL"
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
  Write-Host "  [1] Auto   — detect and connect supported AI apps"
  Write-Host "  [2] Manual — choose which AI app to connect"
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
download failed — is the release tarball published?
  url: $Url
  maintainer: tag v$Version and ensure GitHub Release assets exist
  see: INSTALL.md
"@
    throw
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
      Write-Host "✗ $Label failed" -ForegroundColor Red
      Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for details." -ForegroundColor Yellow
      exit $LASTEXITCODE
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
    Write-Host "✗ $Label failed" -ForegroundColor Red
    Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for full diagnostics." -ForegroundColor Yellow
    if ($output) {
      Write-Host ""
      $output | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
    }
    exit $code
  }
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
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
try {
  $manifestPath = Join-Path $tmp "manifest.json"
  $tarballPath = Join-Path $tmp "release.tar.gz"

  Download-File $ManifestUrl $manifestPath
  $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
  $expectedSha = [string]$manifest.sha256
  if ([string]::IsNullOrWhiteSpace($expectedSha)) { throw "manifest missing sha256" }
  $expectedSha = $expectedSha.ToLowerInvariant()

  Download-File $ReleaseUrl $tarballPath
  $actualSha = Get-Sha256Hex $tarballPath
  if ($actualSha -ne $expectedSha) {
    throw "sha256 mismatch`n  expected: $expectedSha`n  actual:   $actualSha"
  }
  if ($VerboseInstall) {
    Write-Host "sha256: OK"
    Write-Host "release: version=$($manifest.version) rid=$($manifest.rid)"
  }

  $parent = Split-Path -Parent $InstallDir
  if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
  if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
  New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

  $extractTmp = Join-Path $tmp "extract"
  New-Item -ItemType Directory -Path $extractTmp -Force | Out-Null
  tar.exe -xzf $tarballPath -C $extractTmp
  $inner = Get-ChildItem $extractTmp | Select-Object -First 1
  if ($null -eq $inner) { throw "empty tarball" }
  if ($inner.PSIsContainer -and (@(Get-ChildItem $extractTmp).Count -eq 1)) {
    Get-ChildItem $inner.FullName | ForEach-Object {
      Move-Item -LiteralPath $_.FullName -Destination $InstallDir -Force
    }
  } else {
    Get-ChildItem $extractTmp | ForEach-Object {
      Move-Item -LiteralPath $_.FullName -Destination $InstallDir -Force
    }
  }
  if ($VerboseInstall) { Write-Host "extracted: $InstallDir" }
} finally {
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

$env:OCCAM_HOME = $InstallDir
Set-Location $InstallDir
if ($HostTarget) { $env:OCCAM_HOST = $HostTarget }

$postUx = Join-Path $InstallDir "scripts\lib\operator\post-install-ux.mjs"
$postArgs = @($postUx, "--setup", $SetupMode, "--version", $Version, "--download-ok")
if ($VerboseInstall) { $postArgs += "--verbose" }

if (Test-Path $postUx) {
  & node @postArgs
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
  # Legacy release tarball (no post-install-ux.mjs): quiet by capturing child I/O.
  # Published v1.0.0-rc.2 packs predate quiet doctor/verify/smoke flags.
  Write-Host ""
  Write-Host "Occam $Version"
  Write-Host ""
  Write-Host "Installing Occam"
  Write-Host "✓ Download verified"
  $env:OCCAM_INSTALL_QUIET = if ($VerboseInstall) { "0" } else { "1" }
  $env:OCCAM_BANNER = "0"
  $env:WT_OCCAM_BANNER = "0"

  $doctorPs1 = Join-Path $InstallDir "scripts\occam-doctor.ps1"
  Invoke-LegacyInstallStep -Label "Runtime setup (doctor)" -Action {
    & $doctorPs1 -SkipBuild
  }
  Write-Host "✓ Runtime installed"
  Write-Host "✓ Browser ready"

  $verifyJs = Join-Path $InstallDir "scripts\lib\verify-install.mjs"
  Invoke-LegacyInstallStep -Label "Host verify" -Action {
    & node $verifyJs --skip-build --version $Version
  }

  $smokeJs = Join-Path $InstallDir "scripts\hermes-smoke.mjs"
  Invoke-LegacyInstallStep -Label "Self-check" -Action {
    & node $smokeJs
  }
  Write-Host "✓ Self-check passed"
  Write-Host ""
  Write-Host "Occam is installed."
  if (Test-Path (Join-Path $InstallDir "scripts\occam-connect.mjs")) {
    Write-Host "Connecting to your AI app"
    & node (Join-Path $InstallDir "scripts\occam-connect.mjs")
  } else {
    Write-Host "Connect an AI app later with: occam connect"
  }
}
