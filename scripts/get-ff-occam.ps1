#Requires -Version 5.1
<#
.SYNOPSIS
  FF-Occam MCP — Level B one-liner bootstrap for Windows (irm | iex).

.DESCRIPTION
  Node 20+ only — NO git, NO .NET SDK on the install machine.
  Mirrors scripts/get-ff-occam.sh.

.EXAMPLE
  irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex

.EXAMPLE
  $env:OCCAM_SETUP='auto'; $env:OCCAM_HOST='cursor'
  irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
#>
$ErrorActionPreference = "Stop"

$Version = if ($env:OCCAM_VERSION) { $env:OCCAM_VERSION } else { "1.0.0-rc.2" }
$Rid = if ($env:OCCAM_RID) { $env:OCCAM_RID } else { "win-x64" }
$InstallDir = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else {
  Join-Path $env:USERPROFILE ".local\share\ff-occam"
}
$HostTarget = if ($env:OCCAM_HOST) { $env:OCCAM_HOST } else { "hermes" }
$AllowHttp = if ($env:OCCAM_RELEASE_ALLOW_HTTP) { $env:OCCAM_RELEASE_ALLOW_HTTP } else { "0" }
$SetupMode = if ($env:OCCAM_SETUP) { $env:OCCAM_SETUP.Trim().ToLowerInvariant() } else { "" }

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
  Write-Host "node: $(node -v)"
}

function Resolve-SetupMode {
  if ($SetupMode -eq "auto" -or $SetupMode -eq "1") { $script:SetupMode = "auto"; Write-Host "setup: auto (from OCCAM_SETUP)"; return }
  if ($SetupMode -eq "manual" -or $SetupMode -eq "2") { $script:SetupMode = "manual"; Write-Host "setup: manual (from OCCAM_SETUP)"; return }
  if ($SetupMode -ne "") { throw "OCCAM_SETUP must be auto or manual (got $SetupMode)" }

  # Non-interactive pipe / irm|iex → auto
  if ([Console]::IsInputRedirected -or -not [Environment]::UserInteractive) {
    $script:SetupMode = "auto"
    Write-Host "setup: auto (non-interactive)"
    return
  }

  Write-Host ""
  Write-Host "  First-run setup"
  Write-Host "  [1] Auto   — defaults from OCCAM_HOST (default: hermes)"
  Write-Host "  [2] Manual — guided wizard (occam-onboard)"
  Write-Host ""
  $choice = Read-Host "  Setup [1]"
  if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }
  if ($choice -match '^(2|manual)$') { $script:SetupMode = "manual" } else { $script:SetupMode = "auto" }
  Write-Host "setup: $($script:SetupMode)"
}

function Get-Sha256Hex([string]$Path) {
  (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Download-File([string]$Url, [string]$Dest) {
  Assert-UrlScheme $Url
  Write-Host "download: $Url"
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

Write-Host ""
Write-Host "  FF-Occam MCP"
Write-Host "  Level B bootstrap (Windows)"
Write-Host ""

Resolve-SetupMode
Test-NodeVersion

# tar.exe ships with Windows 10+
if (-not (Get-Command tar.exe -ErrorAction SilentlyContinue)) {
  throw "required command not found: tar.exe"
}

Write-Host ""
Write-Host "install_dir: $InstallDir"
Write-Host "host_target: $HostTarget"
Write-Host "release_url: $ReleaseUrl"
Write-Host ""

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
  Write-Host "sha256: OK"
  Write-Host "release: version=$($manifest.version) rid=$($manifest.rid)"

  $parent = Split-Path -Parent $InstallDir
  if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
  if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
  New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

  # strip-components=1 equivalent: extract then flatten one root folder if present
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
  Write-Host "extracted: $InstallDir"
} finally {
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

$env:OCCAM_HOME = $InstallDir
Set-Location $InstallDir

Write-Host ""
Write-Host "doctor (npm + Playwright, skip dotnet publish) ..."
& (Join-Path $InstallDir "scripts\occam-doctor.ps1") -SkipBuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "verify-install ..."
& node (Join-Path $InstallDir "scripts\lib\verify-install.mjs") --skip-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "smoke (core tools) ..."
& node (Join-Path $InstallDir "scripts\hermes-smoke.mjs")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$env:OCCAM_HOST = $HostTarget
Write-Host ""
if ($SetupMode -eq "manual") {
  Write-Host "Starting manual onboard wizard ..."
  & node (Join-Path $InstallDir "scripts\occam-onboard.mjs") --skip-doctor --skip-welcome
} else {
  $profile = if ($HostTarget -eq "cursor") { "default" } else { "hermes-headless" }
  Write-Host "Applying auto setup (profile=$profile host=$HostTarget) ..."
  & node (Join-Path $InstallDir "scripts\occam-onboard.mjs") `
    --non-interactive `
    --profile $profile `
    --host-target $HostTarget `
    --skip-doctor `
    --plain
}

Write-Host ""
Write-Host "=== Connection snippet (host=$HostTarget) ==="
& node (Join-Path $InstallDir "scripts\lib\print-connection-snippet.mjs") $InstallDir $HostTarget
Write-Host ""
Write-Host "OCCAM_HOME=$InstallDir"
Write-Host "Next: node `"$InstallDir\scripts\hermes-smoke.mjs`""
