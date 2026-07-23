#Requires -Version 5.1
<#
.SYNOPSIS
  CI / Hermes - publish AOT (doctor) then Agent-First MVP gate on subprocess MCP.

.EXAMPLE
  $env:OCCAM_HOME = (Get-Location).Path
  .\scripts\ci-agent-mvp-gate.ps1

.ENVIRONMENT
  CI_AGENT_MVP_SKIP_DOCTOR=1  skip dotnet publish when binary already built
  CI_AGENT_MVP_LATENCY=1      pass --latency to run-agent-mvp-gate.mjs (slower)
#>
param(
    [switch]$SkipDoctor,
    [switch]$Latency
)

$ErrorActionPreference = "Stop"
$Root = if ($env:OCCAM_HOME) { $env:OCCAM_HOME.Trim() } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $Root
Remove-Item Env:OCCAM_FORCE_DOTNET_RUN -ErrorAction SilentlyContinue

Write-Host "[ci-agent-mvp-gate] OCCAM_HOME=$Root"

$skipDoctor = $SkipDoctor -or ($env:CI_AGENT_MVP_SKIP_DOCTOR -eq "1")
if (-not $skipDoctor) {
    Write-Host "[ci-agent-mvp-gate] running occam-doctor (AOT publish) ..."
    & (Join-Path $Root "scripts\occam-doctor.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "[ci-agent-mvp-gate] CI_AGENT_MVP_SKIP_DOCTOR=1 - skipping doctor"
}

$gateArgs = @("--skip-refresh")
if ($Latency -or $env:CI_AGENT_MVP_LATENCY -eq "1") {
    $gateArgs += "--latency"
}

Write-Host "[ci-agent-mvp-gate] running run-agent-mvp-gate.mjs $($gateArgs -join ' ') ..."
& node (Join-Path $Root "scripts\run-agent-mvp-gate.mjs") @gateArgs
exit $LASTEXITCODE
