#Requires -Version 5.1
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = if ($env:OCCAM_HOME) { $env:OCCAM_HOME } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $root
$cacheScript = Join-Path $PSScriptRoot "lib\playwright-cache.mjs"

Write-Host "FF-Occam MCP doctor (L0 skeleton)" -ForegroundColor Cyan
Write-Host "OCCAM_HOME=$root"

node (Join-Path $PSScriptRoot "lib\assert-net10-csproj.mjs") $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) {
    Write-Error "node not found on PATH"
}
Write-Host "node: $($node.Source)"

$workersRoot = Join-Path $root "workers"
if (-not (Test-Path (Join-Path $workersRoot "package.json"))) {
    Write-Error "Missing workers/package.json (npm workspace root)"
}

Push-Location $workersRoot
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "npm install (workspace root) ..."
        npm install --no-fund --no-audit
    }
}
finally {
    Pop-Location
}

$skipPlaywrightBundled = $false
$channelRaw = $env:OCCAM_BROWSER_CHANNEL
$channel = if ($channelRaw) { $channelRaw.Trim().ToLowerInvariant() } else { "" }
if ($channel -and $channel -ne "chromium" -and @("chrome", "msedge", "chrome-beta", "msedge-beta") -contains $channel) {
    $skipPlaywrightBundled = $true
    Write-Host "playwright chromium: skip (OCCAM_BROWSER_CHANNEL=$channel)" -ForegroundColor DarkGray
}
elseif ($env:OCCAM_BROWSER_EXECUTABLE_PATH -or $env:OCCAM_CHROME_PATH) {
    $skipPlaywrightBundled = $true
    Write-Host "playwright chromium: skip (system executable path set)" -ForegroundColor DarkGray
}

$browserWorker = Join-Path $root "workers\browser-extract"
if ((Test-Path $browserWorker) -and -not $skipPlaywrightBundled) {
    Push-Location $browserWorker
    try {
        & node $cacheScript has-chromium
        if ($LASTEXITCODE -eq 0) {
            Write-Host "playwright chromium: already installed (skip)" -ForegroundColor DarkGray
        }
        else {
            Write-Host "playwright install chromium ..."
            npx playwright install chromium
        }

        $cachePath = & node $cacheScript path 2>$null
        if ($cachePath) {
            Write-Host "playwright cache: $cachePath" -ForegroundColor DarkGray
        }
    }
    finally {
        Pop-Location
    }
}

$egressSelftest = Join-Path $root "workers\shared\lib\egress-proxy.selftest.mjs"
if ($env:OCCAM_HTTP_PROXY -or $env:OCCAM_HTTPS_PROXY) {
    Write-Host "egress proxy env detected (OCCAM_HTTP_PROXY / OCCAM_HTTPS_PROXY)" -ForegroundColor Yellow
    if (Test-Path $egressSelftest) {
        Write-Host "egress proxy module selftest ..."
        & node $egressSelftest
        if ($LASTEXITCODE -ne 0) {
            Write-Host "warning: egress-proxy selftest failed - verify proxy URL and OCCAM_NO_PROXY bypass list" -ForegroundColor Yellow
        }
    }
    Write-Host "If transcode fails behind proxy, run full gate (L2_EGRESS_OK) or check corporate PAC/NTLM (v2 sidecar)." -ForegroundColor Yellow
}

$pdfSelftest = Join-Path $root "workers\shared\lib\pdf-extract.selftest.mjs"
if (Test-Path $pdfSelftest) {
    Write-Host "pdf-extract module selftest ..."
    Push-Location (Join-Path $root "workers\http-extract")
    & node $pdfSelftest
    $pdfExit = $LASTEXITCODE
    Pop-Location
    if ($pdfExit -ne 0) {
        Write-Host "warning: pdf-extract selftest failed - PDF transcode may be unavailable (is 'unpdf' installed?)" -ForegroundColor Yellow
    }
}

$ssrfSelftest = Join-Path $root "workers\shared\lib\private-ip.selftest.mjs"
if (Test-Path $ssrfSelftest) {
    Write-Host "private-ip (SSRF guard) module selftest ..."
    Push-Location (Join-Path $root "workers\http-extract")
    & node $ssrfSelftest
    $ssrfExit = $LASTEXITCODE
    Pop-Location
    if ($ssrfExit -ne 0) {
        Write-Host "warning: private-ip selftest failed - SSRF/private-URL protection may be degraded" -ForegroundColor Yellow
    }
}

$browserWorker = Join-Path $root "workers\browser-extract"
if (Test-Path $browserWorker) {
    Write-Host "browser launch smoke ..."
    Push-Location $browserWorker
    try {
        & node (Join-Path $browserWorker "lib\verify-browser-launch.mjs")
        if ($LASTEXITCODE -ne 0) {
            Write-Error "browser launch smoke failed"
        }
    }
    finally {
        Pop-Location
    }
}

$verifyManifest = Join-Path $root "scripts\lib\verify-community-manifest.mjs"
if (Test-Path $verifyManifest) {
    Write-Host "community manifest sha256 verify ..."
    & node $verifyManifest
    if ($LASTEXITCODE -ne 0) {
        Write-Error "verify-community-manifest failed"
    }
}

if (-not $SkipBuild) {
    $rid = node (Join-Path $PSScriptRoot "lib\resolve-rid.mjs")
    if (-not $rid) {
        Write-Error "Could not resolve dotnet RID (scripts/lib/resolve-rid.mjs)"
    }
    $publishExe = Join-Path $root "src\FFOccamMcp.Core\bin\Release\net10.0\$rid\publish\OccamMcp.Core.exe"
    if (Test-Path $publishExe) {
        $lockedBy = @()
        try {
            $stream = [System.IO.File]::Open($publishExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
            $stream.Close()
        }
        catch {
            $lockedBy = Get-Process -Name "OccamMcp.Core" -ErrorAction SilentlyContinue
        }
        if ($lockedBy.Count -gt 0) {
            Write-Host "warning: publish exe is locked by running MCP host(s):" -ForegroundColor Yellow
            foreach ($proc in $lockedBy) {
                Write-Host "  PID $($proc.Id) started $($proc.StartTime)" -ForegroundColor Yellow
            }
            Write-Host "Reload MCP servers in Cursor (or restart Cursor), then re-run doctor." -ForegroundColor Yellow
            Write-Host "Until publish succeeds, tools/list may stay stale (reload MCP after publish)." -ForegroundColor Yellow
        }
    }
    # Native AOT publish needs the MSVC linker. Load the VS x64 dev environment so doctor
    # works from any shell (no need to launch from a Developer Command Prompt).
    . (Join-Path $PSScriptRoot "lib\load-vs-dev-env.ps1")
    Enter-OccamVsDevEnv | Out-Null

    Write-Host "dotnet publish (RID=$rid) ..."
    dotnet publish (Join-Path $root "src\FFOccamMcp.Core\FFOccamMcp.Core.csproj") -c Release -r $rid
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed (exit $LASTEXITCODE). If the exe was locked, reload MCP servers and retry."
    }
    if (Test-Path $publishExe) {
        $built = Get-Item $publishExe
        Write-Host "mcp host: $($built.FullName) ($($built.LastWriteTime))" -ForegroundColor DarkGray
        $rootExe = Join-Path $root "OccamMcp.Core.exe"
        Copy-Item -Path $publishExe -Destination $rootExe -Force
        Write-Host "mcp host (OCCAM_HOME root): $rootExe" -ForegroundColor DarkGray
    }
    else {
        Write-Error "publish output missing: $publishExe"
    }
}

if ($SkipBuild) {
    node (Join-Path $PSScriptRoot "lib\assert-host-binary.mjs") $root --skip-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else {
    node (Join-Path $PSScriptRoot "lib\assert-host-binary.mjs") $root
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "doctor: OK" -ForegroundColor Green
$sessionsRoot = if ($env:OCCAM_SESSIONS_ROOT) { $env:OCCAM_SESSIONS_ROOT } else { Join-Path $env:USERPROFILE ".occam\sessions" }
Write-Host "sessions: $sessionsRoot (optional: node scripts/occam-session.mjs init)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "MCP host ready. Wire any MCP client (Cursor, Claude Desktop, VS Code, …):" -ForegroundColor Cyan
Write-Host "  occam onboard"
Write-Host "  # or: node scripts/lib/print-connection-snippet.mjs `"$root`" generic-stdio"
Write-Host ""
Write-Host "Canonical launcher: node scripts/launch-mcp-host.mjs with OCCAM_HOME=$root" -ForegroundColor DarkGray
Write-Host "Avoid on git clone: packages/occam-mcp/bin/occam-mcp.js without OCCAM_HOME (npx/release path)." -ForegroundColor Yellow
Write-Host "Reload MCP servers in your host after saving config." -ForegroundColor Yellow
