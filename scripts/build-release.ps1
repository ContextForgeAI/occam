#Requires -Version 5.1
<#
.SYNOPSIS
  Build per-RID release tarball + sha256 manifest (P2-5b Level B).

.EXAMPLE
  .\scripts\build-release.ps1 -Rid win-x64 -Version 0.7.7-install
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,
    [string]$Version,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$nodeArgs = @(
    (Join-Path $PSScriptRoot "lib\build-release.mjs"),
    "--rid", $Rid
)
if ($Version) {
    $nodeArgs += @("--version", $Version)
}
if ($OutputDir) {
    $nodeArgs += @("--output-dir", $OutputDir)
}

& node @nodeArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
