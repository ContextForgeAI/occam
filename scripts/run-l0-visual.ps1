param(
    [string]$Url,
    [string]$Id = "adhoc",
    [string]$Backend = "http_then_browser",
    [switch]$Open,
    [switch]$FullGate,
    [switch]$OrphanAudit,
    [int]$OrphanCooldownSec = 60
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:OCCAM_HOME = $Root

$proj = Join-Path $Root "benchmarks\l0-gate\L0Gate.csproj"

if ($Url) {
    $args = @("--url=$Url", "--id=$Id", "--backend=$Backend")
    if ($Open) { $args += "--open" }
    dotnet run --project $proj -- @args
    exit $LASTEXITCODE
}

$gateArgs = @("--visual")
if (-not $FullGate) { $gateArgs += @("--fast", "--smoke-only") }
if ($Open) { $gateArgs += "--open" }

Write-Host "L0 visual run (fast HTTP smoke unless -FullGate)" -ForegroundColor Cyan
dotnet run --project $proj -- @gateArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($OrphanAudit) {
    $auditScript = Join-Path $Root "scripts\run-l0-orphan-audit.ps1"
    Write-Host "L0 orphan audit (${OrphanCooldownSec}s cooldown)" -ForegroundColor Cyan
    & $auditScript -OrphanCooldownSec $OrphanCooldownSec
    exit $LASTEXITCODE
}

exit 0
