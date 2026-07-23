#Requires -Version 5.1
<#
.SYNOPSIS
  Export sanitized community playbook bundle for manual PR (PB4c).

.DESCRIPTION
  Maintainer-only CLI — no MCP tool, no auto-upload.
  Requires --ack-community-review. Rejects secrets (K8) before writing export files.

.EXAMPLE
  $env:OCCAM_HOME = (Get-Location).Path
  .\scripts\occam-playbook-publish.ps1 --input "$env:USERPROFILE\.occam\playbooks\local\example.com.playbook.json" --ack-community-review
#>
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = "Stop"
$root = if ($env:OCCAM_HOME) { $env:OCCAM_HOME } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $root

$cli = Join-Path $PSScriptRoot "lib\playbook-publish.mjs"
if (-not (Test-Path $cli)) {
    Write-Error "Missing publish CLI module: $cli"
}

& node $cli @Args
exit $LASTEXITCODE
