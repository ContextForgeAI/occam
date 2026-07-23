#Requires -Version 5.1
<#
.SYNOPSIS
  CI — build Level B tarball for one RID + verify manifest/layout.

.EXAMPLE
  .\scripts\ci-release-build.ps1 -Rid win-x64
  .\scripts\ci-release-build.ps1 -Rid win-x64 -Version 0.8.12
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,
    [string]$Version,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ($Version) { $env:OCCAM_RELEASE_VERSION = $Version }
if ($OutputDir) { $env:OCCAM_RELEASE_OUTPUT_DIR = $OutputDir }

Write-Host "[ci-release-build] rid=$Rid OCCAM_HOME=$root"

$stopScript = Join-Path $PSScriptRoot "lib\stop-occam-processes.mjs"
if (Test-Path $stopScript) {
    try { & node $stopScript 2>$null } catch { }
}

$buildArgs = @(
    (Join-Path $PSScriptRoot "lib\build-release.mjs"),
    "--rid", $Rid
)
if ($Version) { $buildArgs += @("--version", $Version) }
if ($OutputDir) { $buildArgs += @("--output-dir", $OutputDir) }
& node @buildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$verifyArgs = @(
    (Join-Path $PSScriptRoot "lib\verify-release-artifact.mjs"),
    "--rid", $Rid
)
if ($Version) { $verifyArgs += @("--version", $Version) }
if ($OutputDir) { $verifyArgs += @("--output-dir", $OutputDir) }
& node @verifyArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
