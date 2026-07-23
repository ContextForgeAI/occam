# P1: L0 orphan browser audit — playwright chrome/chromium/msedge/node after transcode.
#Requires -Version 5.1
param(
    [int]$OrphanCooldownSec = 60,
    [switch]$RunWorkers,
    [string]$L1Url = "https://www.iana.org/domains/reserved",
    [string]$L2Url = "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Promise"
)

$ErrorActionPreference = "Stop"
$repoRoot = if ($env:OCCAM_HOME) { $env:OCCAM_HOME } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $repoRoot
Set-Location $repoRoot

$outDir = Join-Path $repoRoot "artifacts\orphan-audit"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Get-PlaywrightRelatedProcesses {
    Get-Process chrome, chromium, msedge, node -ErrorAction SilentlyContinue |
        Where-Object {
            $path = $_.Path
            if ($null -eq $path) { return $false }
            return $path -like "*playwright*" -or $path -like "*ms-playwright*"
        }
}

function Invoke-L0GateUrl {
    param(
        [string]$Id,
        [string]$Url,
        [string]$Backend
    )
    $proj = Join-Path $repoRoot "benchmarks\l0-gate\L0Gate.csproj"
    Write-Host "=== l0-gate $Id ($Backend): $Url ===" -ForegroundColor Cyan
    dotnet run --project $proj -- --url=$Url --id=$Id --backend=$Backend
    if ($LASTEXITCODE -ne 0) {
        throw "l0-gate failed for $Id ($Backend), exit $LASTEXITCODE"
    }
}

$report = [ordered]@{
    schema_version = "1.0"
    profile        = "l0-orphan-audit"
    captured_at    = (Get-Date).ToUniversalTime().ToString("o")
    notes          = @(
        "P1 run-l0-orphan-audit.ps1",
        "P9-INF2 WorkerProcessGroup",
        "orphan cooldown ${OrphanCooldownSec}s"
    )
}

if ($RunWorkers) {
    $l1Ok = $true
    $l2Ok = $true
    try {
        Invoke-L0GateUrl -Id "l1-orphan-audit" -Url $L1Url -Backend "http"
    }
    catch {
        $l1Ok = $false
        Write-Warning $_.Exception.Message
    }

    try {
        Invoke-L0GateUrl -Id "l2-orphan-audit" -Url $L2Url -Backend "browser"
    }
    catch {
        $l2Ok = $false
        Write-Warning $_.Exception.Message
    }

    $report.l1_http = @{
        url = $L1Url
        ok  = $l1Ok
    }
    $report.l2_browser = @{
        url = $L2Url
        ok  = $l2Ok
    }
}

Write-Host "=== Orphan audit (${OrphanCooldownSec}s cooldown) ===" -ForegroundColor Cyan
Start-Sleep -Seconds $OrphanCooldownSec
$orphans = @(Get-PlaywrightRelatedProcesses)
$report.orphan_audit = @{
    cooldown_sec               = $OrphanCooldownSec
    orphan_browser_processes     = $orphans.Count
    orphan_pids                  = @($orphans | ForEach-Object { $_.Id })
    orphan_paths                 = @($orphans | ForEach-Object { $_.Path })
    gate_pass                    = ($orphans.Count -eq 0)
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outPath = Join-Path $outDir "audit-$stamp.json"
$report | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding UTF8

Write-Host "Wrote $outPath" -ForegroundColor Green
$gateLabel = if ($report.orphan_audit.gate_pass) { "PASS" } else { "FAIL ($($orphans.Count) processes)" }
Write-Host ("Orphan gate: {0}" -f $gateLabel)

if (-not $report.orphan_audit.gate_pass) { exit 2 }
if ($RunWorkers -and $report.l2_browser -and -not $report.l2_browser.ok) { exit 1 }
if ($RunWorkers -and $report.l1_http -and -not $report.l1_http.ok) { exit 1 }
exit 0
