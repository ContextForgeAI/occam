#Requires -Version 5.1
param(
    [switch]$Open,
    [switch]$WithUnit,
    [switch]$Visual
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:OCCAM_HOME = $Root

$proj = Join-Path $Root "benchmarks\l0-gate\L0Gate.csproj"
$args = @("--fast", "--smoke-only")
if ($WithUnit) { $args = @("--fast") }
if ($Visual) { $args += "--visual" }
if ($Open) { $args += "--open" }

Write-Host "L0 FAST gate (mdn + nginx + not-found, HTTP only)" -ForegroundColor Cyan
dotnet run --project $proj -- @args
exit $LASTEXITCODE
