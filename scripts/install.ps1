#Requires -Version 5.1
<#
.SYNOPSIS
  FF-Occam MCP installer — production-oriented (pinned ref, verify, no silent git failures)

.DESCRIPTION
  Level A: clone the repo, then run with -Ref pinned to a release tag (requires .NET SDK).
  Level B: -FromUrl / OCCAM_RELEASE_URL — pre-built tarball, Node 20+ only, no SDK on target.
  Avoid irm | iex in production (supply-chain risk).

.EXAMPLE
  git clone $env:OCCAM_REPO_URL C:\opt\ff-occam
  cd C:\opt\ff-occam
  .\scripts\install.ps1 -RepoUrl $env:OCCAM_REPO_URL -Ref v0.7.7-install

.EXAMPLE
  .\scripts\install.ps1 -FromUrl "https://releases.example/ff-occam-0.7.7-install-win-x64.tar.gz" -InstallDir C:\opt\ff-occam
#>
param(
    [string]$InstallDir = $(if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else { Join-Path $env:USERPROFILE ".local\share\ff-occam" }),
    [string]$RepoUrl = $env:OCCAM_REPO_URL,
    [string]$Ref = $(if ($env:OCCAM_REF) { $env:OCCAM_REF } elseif ($env:OCCAM_BRANCH) { $env:OCCAM_BRANCH } else { "main" }),
    [string]$FromUrl = $env:OCCAM_RELEASE_URL,
    [string]$ManifestUrl = $env:OCCAM_RELEASE_MANIFEST_URL,
    [string]$BrowserChannel = $env:OCCAM_BROWSER_CHANNEL,
    [switch]$SkipBuild,
    [switch]$SkipVerify,
    [switch]$ForcePlaywright
)

$ErrorActionPreference = "Stop"

if ([Console]::IsInputRedirected) {
    Write-Warning "stdin is redirected (pipe install). For production, clone the repo and run .\scripts\install.ps1 directly."
}

$scriptRoot = Split-Path -Parent $PSScriptRoot

if ($FromUrl) {
    if ($BrowserChannel) {
        $env:OCCAM_BROWSER_CHANNEL = $BrowserChannel
        Write-Warning "OCCAM_BROWSER_CHANNEL is set — bundled Playwright Chromium is the production default for reproducible extracts."
    }

    & node (Join-Path $scriptRoot "scripts\lib\install-preflight.mjs") release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $releaseArgs = @("--url", $FromUrl, "--install-dir", $InstallDir)
    if ($ManifestUrl) {
        $releaseArgs += @("--manifest-url", $ManifestUrl)
    }
    & node (Join-Path $scriptRoot "scripts\lib\release-install.mjs") @releaseArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $env:OCCAM_HOME = $InstallDir
    Push-Location $InstallDir
    try {
        $version = "unknown"
        $versionPath = Join-Path $InstallDir "VERSION"
        if (Test-Path $versionPath) {
            $version = (Get-Content $versionPath -Raw).Trim()
        }
        Write-Host "install: level=B version=$version"

        if ($ForcePlaywright) {
            Write-Host "playwright install chromium (-ForcePlaywright) ..."
            Push-Location (Join-Path $InstallDir "workers\browser-extract")
            try {
                npx playwright install chromium
            }
            finally {
                Pop-Location
            }
        }

        & (Join-Path $InstallDir "scripts\occam-doctor.ps1") -SkipBuild

        if (-not $SkipVerify) {
            & node (Join-Path $InstallDir "scripts\lib\verify-install.mjs") --skip-build
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "=== FF-Occam install complete (Level B) ===" -ForegroundColor Green
    Write-Host "OCCAM_HOME=$InstallDir"
    Write-Host "version=$version"
    Write-Host ""
    Write-Host "Next: `$env:PATH = `"$InstallDir\scripts;`$env:PATH`" ; occam"
    Write-Host "      occam doctor  # or occam onboard"
    Write-Host "Docs: docs/operator_journey.md"
    Write-Host ""
    Write-Host "Wire any MCP host:" -ForegroundColor Cyan
    Write-Host "  occam onboard"
    & node (Join-Path $InstallDir "scripts\lib\print-connection-snippet.mjs") $InstallDir generic-stdio
    Write-Host ""
    Write-Host "Reload MCP servers in your host after saving."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($RepoUrl)) {
    throw "Set OCCAM_REPO_URL or pass -RepoUrl (Level A), or -FromUrl / OCCAM_RELEASE_URL (Level B)"
}

if ($BrowserChannel) {
    $env:OCCAM_BROWSER_CHANNEL = $BrowserChannel
    Write-Warning "OCCAM_BROWSER_CHANNEL is set — bundled Playwright Chromium is the production default for reproducible extracts."
}

& node (Join-Path $scriptRoot "scripts\lib\install-preflight.mjs") all
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$parent = Split-Path -Parent $InstallDir
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

if (Test-Path $InstallDir) {
    $gitDir = Join-Path $InstallDir ".git"
    if (-not (Test-Path $gitDir)) {
        $item = Get-Item $InstallDir -ErrorAction SilentlyContinue
        if ($item -and -not $item.PSIsContainer) {
            throw "Install path exists and is not a directory: $InstallDir"
        }
        throw "$InstallDir exists but is not a git repo — remove it or pick another -InstallDir"
    }
}

function Invoke-Git {
    param([string[]]$Args, [string]$Cwd = $InstallDir)
    & git -C $Cwd @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed (exit $LASTEXITCODE)"
    }
}

if (Test-Path (Join-Path $InstallDir ".git")) {
    Write-Host "Updating $InstallDir (ref=$Ref) ..."
    Invoke-Git @("fetch", "origin", "--tags")
    try {
        Invoke-Git @("checkout", $Ref)
    }
    catch {
        Invoke-Git @("checkout", "origin/$Ref")
    }
    $isLocalBranch = $false
    try {
        & git -C $InstallDir show-ref --verify --quiet "refs/heads/$Ref"
        $isLocalBranch = ($LASTEXITCODE -eq 0)
    }
    catch { }
    if ($isLocalBranch) {
        try {
            Invoke-Git @("pull", "--ff-only", "origin", $Ref)
        }
        catch {
            throw "git pull --ff-only failed for branch $Ref — resolve manually or re-clone"
        }
    }
}
else {
    Write-Host "Cloning $RepoUrl (ref=$Ref) into $InstallDir ..."
    try {
        & git clone --depth 1 --branch $Ref $RepoUrl $InstallDir
        if ($LASTEXITCODE -ne 0) { throw "git clone failed" }
    }
    catch {
        throw "git clone failed for ref=$Ref — check tag/branch name and repo access"
    }
}

$env:OCCAM_HOME = $InstallDir
Push-Location $InstallDir
try {
    $commit = "unknown"
    try {
        $commit = (git rev-parse --short HEAD).Trim()
    }
    catch { }
    Write-Host "install: level=A ref=$Ref commit=$commit"

    if ($ForcePlaywright) {
        Write-Host "playwright install chromium (-ForcePlaywright) ..."
        Push-Location (Join-Path $InstallDir "workers\browser-extract")
        try {
            npx playwright install chromium
        }
        finally {
            Pop-Location
        }
    }

    $doctorArgs = @()
    if ($SkipBuild) {
        $doctorArgs += "-SkipBuild"
    }
    & (Join-Path $InstallDir "scripts\occam-doctor.ps1") @doctorArgs

    if (-not $SkipVerify) {
        $verifyArgs = @()
        if ($SkipBuild) {
            $verifyArgs += "--skip-build"
        }
        & node (Join-Path $InstallDir "scripts\lib\verify-install.mjs") @verifyArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== FF-Occam install complete (Level A) ===" -ForegroundColor Green
Write-Host "OCCAM_HOME=$InstallDir"
Write-Host "ref=$Ref commit=$commit"
Write-Host ""
Write-Host "Next: `$env:PATH = `"$InstallDir\scripts;`$env:PATH`" ; occam"
Write-Host "      occam doctor  # or occam onboard"
Write-Host "Docs: docs/operator_journey.md"
Write-Host ""
Write-Host "Wire any MCP host:" -ForegroundColor Cyan
Write-Host "  occam onboard"
& node (Join-Path $InstallDir "scripts\lib\print-connection-snippet.mjs") $InstallDir generic-stdio
Write-Host ""
Write-Host "Reload MCP servers in your host after saving."
