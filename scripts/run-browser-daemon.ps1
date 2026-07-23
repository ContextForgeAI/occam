#Requires -Version 5.1
param(
    [int]$Port = 39217
)

$ErrorActionPreference = "Stop"
$Root = if ($env:OCCAM_HOME) { $env:OCCAM_HOME } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $Root
$env:OCCAM_BROWSER_DAEMON_PORT = "$Port"

$cacheScript = Join-Path $PSScriptRoot "lib\playwright-cache.mjs"
if (-not $env:PLAYWRIGHT_BROWSERS_PATH) {
    $cachePath = & node $cacheScript path 2>$null
    if ($cachePath) {
        $env:PLAYWRIGHT_BROWSERS_PATH = $cachePath
    }
}

$script = Join-Path $Root "workers\browser-extract\browser-daemon.mjs"
if (-not (Test-Path $script)) {
    Write-Error "Missing $script"
}

Write-Host "Starting browser daemon on http://127.0.0.1:$Port" -ForegroundColor Cyan
node $script --port=$Port
