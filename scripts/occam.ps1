# Unified FF-Occam operator CLI — delegates to occam.mjs
param(
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$Args
)

$Root = if ($env:OCCAM_HOME) { $env:OCCAM_HOME } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $Root
$OccamMjs = Join-Path $Root "scripts\occam.mjs"

if (-not (Test-Path $OccamMjs)) {
  Write-Error "missing $OccamMjs"
  exit 1
}

& node $OccamMjs @Args
exit $LASTEXITCODE
