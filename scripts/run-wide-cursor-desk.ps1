#Requires -Version 5.1
<#
.SYNOPSIS
  Maximum Cursor-desk validation before Hermes/OpenClaw multi-device testing.

  Phases:
    1. media-refs selftest + L0 gate (unit + live corpora unless -Fast)
    2. Recipe R (desk-recipe-r.mjs) — K9 RAG chain
    3. Recipe E heal desk (optional, -SkipHeal)

  Artifacts: artifacts/wide-cursor-desk/<date>/
#>
param(
    [switch]$Fast,
    [switch]$SkipHeal,
    [switch]$SkipGate
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:OCCAM_HOME = $Root
$env:OCCAM_FORCE_DOTNET_RUN = "1"

$date = Get-Date -Format "yyyy-MM-dd"
$outDir = Join-Path $Root "artifacts\wide-cursor-desk\$date"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$log = Join-Path $outDir "run.log"
$summary = @{
    startedAt = (Get-Date).ToUniversalTime().ToString("o")
    occamHome = $Root
    phases  = @()
}

function Write-Phase($name, $ok, $detail) {
    $entry = @{ name = $name; ok = $ok; detail = $detail }
    $summary.phases += $entry
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1}" -f $(if ($ok) { "PASS" } else { "FAIL" }), $name) -ForegroundColor $color
    if ($detail) { Write-Host "  $detail" }
}

Write-Host "Wide Cursor desk validation" -ForegroundColor Cyan
Write-Host "OCCAM_HOME=$Root"
Write-Host "Artifacts=$outDir"

# --- Phase 0: media-refs selftest ---
try {
    $null = node (Join-Path $Root "workers\shared\lib\media-refs.selftest.mjs") 2>&1 | Tee-Object -FilePath $log -Append
    Write-Phase "media-refs.selftest" $true "node selftest OK"
} catch {
    Write-Phase "media-refs.selftest" $false $_.Exception.Message
}

# --- Phase 1: L0 gate ---
if (-not $SkipGate) {
    $gateArgs = @("--project", (Join-Path $Root "benchmarks\l0-gate\L0Gate.csproj"))
    if ($Fast) {
        $gateArgs += @("--", "--fast", "--smoke-only")
    } else {
        $gateArgs += @("--")
    }
    Write-Host "`n=== L0 gate $(if ($Fast) { '(fast smoke-only)' } else { '(full)' }) ===" -ForegroundColor Cyan
    dotnet run @gateArgs 2>&1 | Tee-Object -FilePath $log -Append
    $gateOk = $LASTEXITCODE -eq 0
    Write-Phase "l0-gate" $gateOk "exit=$LASTEXITCODE"
} else {
    Write-Phase "l0-gate" $true "skipped"
}

# --- Phase 2: Recipe R ---
$recipeOut = Join-Path $outDir "recipe-r"
Write-Host "`n=== Recipe R (K9) ===" -ForegroundColor Cyan
node (Join-Path $Root "scripts\desk-recipe-r.mjs") "--out=$recipeOut" 2>&1 | Tee-Object -FilePath $log -Append
$recipeOk = $LASTEXITCODE -eq 0
Write-Phase "desk-recipe-r" $recipeOk "artifacts=$recipeOut"

# --- Phase 3: Recipe E heal desk ---
if (-not $SkipHeal) {
    $healOut = Join-Path $outDir "recipe-e-heal"
    Write-Host "`n=== Recipe E heal desk ===" -ForegroundColor Cyan
    node (Join-Path $Root "scripts\desk-recipe-e-heal.mjs") "--out=$healOut" 2>&1 | Tee-Object -FilePath $log -Append
    $healOk = $LASTEXITCODE -eq 0
    Write-Phase "desk-recipe-e-heal" $healOk "artifacts=$healOut"
} else {
    Write-Phase "desk-recipe-e-heal" $true "skipped"
}

$summary.finishedAt = (Get-Date).ToUniversalTime().ToString("o")
$summary.overallPass = -not ($summary.phases | Where-Object { -not $_.ok })
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $outDir "summary.json") -Encoding UTF8

$overallLabel = if ($summary.overallPass) { "PASS" } else { "PARTIAL / FAIL" }
$mdPath = Join-Path $outDir "WIDE-CURSOR-DESK.md"
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Wide Cursor desk - $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("**Overall:** $overallLabel")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Phase | Result | Detail |")
[void]$sb.AppendLine("|-------|--------|--------|")
foreach ($p in $summary.phases) {
    $res = if ($p.ok) { "PASS" } else { "FAIL" }
    [void]$sb.AppendLine("| $($p.name) | $res | $($p.detail) |")
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Next (other devices)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("1. Linux Hermes - install Occam + stdio MCP; replay Recipe R")
[void]$sb.AppendLine("2. macOS OpenClaw - ingest excerpts + mediaRefs")
[void]$sb.AppendLine("3. OpenRouter - answer layer on retrieved chunks")
[void]$sb.AppendLine("")
$sb.ToString() | Set-Content $mdPath -Encoding UTF8

Write-Host "`n$outDir\WIDE-CURSOR-DESK.md" -ForegroundColor Cyan
if (-not $summary.overallPass) { exit 1 }
exit 0
