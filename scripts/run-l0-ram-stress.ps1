#Requires -Version 5.1
param(
    [switch]$StressTest,
    [switch]$Smoke,
    [string]$Configuration = "Release",
    [string]$RunId = "",
    [int]$Parallel = 8,
    [int]$Loops = 1,
    [int]$RamBudgetMb = 250
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:OCCAM_HOME = $Root

function Test-PlaywrightInstalled {
    $pwRoot = Join-Path $env:LOCALAPPDATA "ms-playwright"
    if (-not (Test-Path $pwRoot)) { return $false }
    return @(Get-ChildItem $pwRoot -Directory -Filter "chromium-*" -ErrorAction SilentlyContinue).Count -gt 0
}

if (-not $StressTest) {
    Write-Host "Pass -StressTest to run L0 RAM stress." -ForegroundColor Yellow
    Write-Host "Example: powershell -File scripts/run-l0-ram-stress.ps1 -StressTest"
    exit 2
}

Write-Host "=== L0 RAM stress preflight ===" -ForegroundColor Cyan

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Error "node not found on PATH"
}

$workersRoot = Join-Path $Root "workers"
if (-not (Test-Path (Join-Path $workersRoot "node_modules"))) {
    Write-Host "npm install (workers workspace)..." -ForegroundColor Yellow
    Push-Location $workersRoot
    npm install --no-fund --no-audit
    Pop-Location
}

if (-not (Test-PlaywrightInstalled)) {
    Write-Host "playwright install chromium ..." -ForegroundColor Yellow
    Push-Location (Join-Path $Root "workers\browser-extract")
    npx playwright install chromium
    Pop-Location
}

if (-not $RunId) {
    $RunId = Get-Date -Format "yyyyMMdd-HHmmss"
}

$proj = Join-Path $Root "benchmarks\l0-ram-stress\L0RamStress.csproj"
dotnet build $proj -c $Configuration -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$effectiveParallel = if ($Smoke) { [Math]::Min(2, $Parallel) } else { $Parallel }
$env:WT_BROWSER_MAX_PARALLEL = "$effectiveParallel"

if ($Smoke) {
    # Smoke: exercise shared daemon + queue (low RAM path).
    $env:OCCAM_BROWSER_PROFILE = "shared"
} else {
    # Full stress parallel>2: isolated workers (WebMCP-style throughput).
    $env:OCCAM_BROWSER_PROFILE = "isolated"
    $env:OCCAM_BROWSER_DAEMON = "0"
}

# Per-extract budget (daemon path scales × parallel in Core).
$env:OCCAM_BROWSER_TIMEOUT_MS = "90000"

$args = @(
    "--project", $proj,
    "-c", $Configuration,
    "--",
    "--stress-test",
    "--force-browser",
    "--run-id", $RunId,
    "--parallel", "$effectiveParallel",
    "--loops", "$Loops",
    "--ram-budget-mb", "$RamBudgetMb"
)

if ($Smoke) {
    $args += "--ram-smoke"
}

Write-Host "Running L0 RAM stress (Smoke=$Smoke Parallel=$effectiveParallel)..." -ForegroundColor Cyan
dotnet run @args
$exitCode = $LASTEXITCODE

switch ($exitCode) {
    0 { Write-Host "RAM_STRESS_GATE PASS" -ForegroundColor Green }
    1 { Write-Host "RAM_STRESS_GATE FAIL (budget)" -ForegroundColor Red }
    2 { Write-Host "RAM_STRESS_GATE FAIL (leak trend)" -ForegroundColor Red }
    3 { Write-Host "RAM_STRESS_GATE FAIL (workers)" -ForegroundColor Red }
    default { Write-Host "RAM_STRESS_GATE FAIL (exit $exitCode)" -ForegroundColor Red }
}

exit $exitCode
