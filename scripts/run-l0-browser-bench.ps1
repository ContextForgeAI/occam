#Requires -Version 5.1
param(
    [string]$Url,
    [int]$Rounds = 3,
    [switch]$NoSpawnCompare
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:OCCAM_HOME = $Root

$proj = Join-Path $Root "benchmarks\l0-gate\L0Gate.csproj"
$args = @("--bench-browser", "--rounds=$Rounds")
if ($Url) { $args += "--url=$Url" }
if ($NoSpawnCompare) { $args += "--no-spawn-compare" }

Write-Host "L0 browser bench — cold vs warm daemon (+ optional spawn compare)" -ForegroundColor Cyan
dotnet run --project $proj -c Release -- @args
exit $LASTEXITCODE
