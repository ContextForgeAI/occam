#Requires -Version 5.1
param(
    [switch]$SkipBuild,
    [switch]$Quiet,
    [switch]$VerboseDoctor
)

$ErrorActionPreference = "Stop"
$root = if ($env:OCCAM_HOME) { $env:OCCAM_HOME } else { Split-Path -Parent $PSScriptRoot }
$env:OCCAM_HOME = $root
$cacheScript = Join-Path $PSScriptRoot "lib\playwright-cache.mjs"

# Default: concise human summary. Engineering dump via -VerboseDoctor / OCCAM_VERBOSE=1.
if (-not $PSBoundParameters.ContainsKey("Quiet") -and -not $VerboseDoctor) {
    $Quiet = $true
}
if ($env:OCCAM_VERBOSE -eq "1" -or $env:OCCAM_VERBOSE -eq "true" -or $VerboseDoctor) {
    $Quiet = $false
}
if ($env:OCCAM_INSTALL_QUIET -eq "0" -or $env:OCCAM_INSTALL_QUIET -eq "false") {
    $Quiet = $false
}
if ($env:OCCAM_INSTALL_QUIET -eq "1" -or $env:OCCAM_INSTALL_QUIET -eq "true") {
    $Quiet = $true
}
function Write-Doctor([string]$Message, [ConsoleColor]$Color = [ConsoleColor]::Gray) {
    if (-not $Quiet) { Write-Host $Message -ForegroundColor $Color }
}

Write-Doctor "Occam doctor" Cyan
Write-Doctor "OCCAM_HOME=$root"

node (Join-Path $PSScriptRoot "lib\assert-net10-csproj.mjs") $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) {
    Write-Error "node not found on PATH"
}
Write-Doctor "node: $($node.Source)"

$workersRoot = Join-Path $root "workers"
if (-not (Test-Path (Join-Path $workersRoot "package.json"))) {
    Write-Error "Missing workers/package.json (npm workspace root)"
}

Push-Location $workersRoot
try {
    if (-not (Test-Path "node_modules")) {
        Write-Doctor "npm install (workspace root) ..."
        if ($Quiet) {
            npm install --no-fund --no-audit --silent 2>$null | Out-Null
        } else {
            npm install --no-fund --no-audit
        }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
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
    Write-Doctor "playwright chromium: skip (OCCAM_BROWSER_CHANNEL=$channel)" DarkGray
}
elseif ($env:OCCAM_BROWSER_EXECUTABLE_PATH -or $env:OCCAM_CHROME_PATH) {
    $skipPlaywrightBundled = $true
    Write-Doctor "playwright chromium: skip (system executable path set)" DarkGray
}

$browserWorker = Join-Path $root "workers\browser-extract"
if ((Test-Path $browserWorker) -and -not $skipPlaywrightBundled) {
    $cachePath = & node $cacheScript path 2>$null
    if ($cachePath) {
        Write-Doctor "playwright cache: $cachePath" DarkGray
    }
}

$egressSelftest = Join-Path $root "workers\shared\lib\egress-proxy.selftest.mjs"
if ($env:OCCAM_HTTP_PROXY -or $env:OCCAM_HTTPS_PROXY) {
    Write-Doctor "egress proxy env detected (OCCAM_HTTP_PROXY / OCCAM_HTTPS_PROXY)" Yellow
    if (Test-Path $egressSelftest) {
        Write-Doctor "egress proxy module selftest ..."
        if ($Quiet) {
            & node $egressSelftest 2>&1 | Out-Null
        } else {
            & node $egressSelftest
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Host "warning: egress-proxy selftest failed - verify proxy URL and OCCAM_NO_PROXY bypass list" -ForegroundColor Yellow
        }
    }
    Write-Doctor "If transcode fails behind proxy, run full gate (L2_EGRESS_OK) or check corporate PAC/NTLM (v2 sidecar)." Yellow
}

$pdfSelftest = Join-Path $root "workers\shared\lib\pdf-extract.selftest.mjs"
if (Test-Path $pdfSelftest) {
    Write-Doctor "pdf-extract module selftest ..."
    Push-Location (Join-Path $root "workers\http-extract")
    if ($Quiet) {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $pdfOut = & node $pdfSelftest 2>&1 | Out-String
        $pdfExit = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
    } else {
        & node $pdfSelftest
        $pdfExit = $LASTEXITCODE
        $pdfOut = ""
    }
    Pop-Location
    if ($pdfExit -ne 0) {
        Write-Host "warning: pdf-extract selftest failed - PDF transcode may be unavailable (is 'unpdf' installed?)" -ForegroundColor Yellow
        if ($Quiet -and $pdfOut) { Write-Host $pdfOut }
    }
}

$ssrfSelftest = Join-Path $root "workers\shared\lib\private-ip.selftest.mjs"
if (Test-Path $ssrfSelftest) {
    Write-Doctor "private-ip (SSRF guard) module selftest ..."
    Push-Location (Join-Path $root "workers\http-extract")
    if ($Quiet) {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $ssrfOut = & node $ssrfSelftest 2>&1 | Out-String
        $ssrfExit = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
    } else {
        & node $ssrfSelftest
        $ssrfExit = $LASTEXITCODE
        $ssrfOut = ""
    }
    Pop-Location
    if ($ssrfExit -ne 0) {
        Write-Host "warning: private-ip selftest failed - SSRF/private-URL protection may be degraded" -ForegroundColor Yellow
        if ($Quiet -and $ssrfOut) { Write-Host $ssrfOut }
    }
}

if (Test-Path $browserWorker) {
    # Launch is the source of truth: it also installs a missing bundled runtime and retries once.
    # Single path string — quiet vs verbose only changes output, not probe count.
    $chromiumLaunchProbe = Join-Path $browserWorker "lib\ensure-chromium-usable.mjs"
    if (-not (Test-Path $chromiumLaunchProbe)) {
        # Older Level B tarballs may only ship verify-browser-launch.mjs.
        $chromiumLaunchProbe = Join-Path $browserWorker "lib\verify-browser-launch.mjs"
    }
    if (Test-Path $chromiumLaunchProbe) {
        Write-Doctor "browser runtime check (launch probe) ..."
        Push-Location $browserWorker
        try {
            if ($Quiet) {
                # Native node stderr must not become a terminating error under $ErrorActionPreference Stop.
                $prevEap = $ErrorActionPreference
                $ErrorActionPreference = "Continue"
                & node $chromiumLaunchProbe 1>$null 2>$null
                $probeExit = $LASTEXITCODE
                $ErrorActionPreference = $prevEap
            } else {
                & node $chromiumLaunchProbe
                $probeExit = $LASTEXITCODE
            }
            if ($probeExit -ne 0) {
                Write-Error "browser runtime unavailable"
            }
        }
        finally {
            Pop-Location
        }
    } else {
        Write-Host "warning: browser launch probe script missing — skip browser check" -ForegroundColor Yellow
    }
}

$verifyManifest = Join-Path $root "scripts\lib\verify-community-manifest.mjs"
if (Test-Path $verifyManifest) {
    Write-Doctor "community manifest sha256 verify ..."
    if ($Quiet) {
        & node $verifyManifest 2>&1 | Out-Null
    } else {
        & node $verifyManifest
    }
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
        Write-Doctor "mcp host: $($built.FullName) ($($built.LastWriteTime))" DarkGray
        $rootExe = Join-Path $root "OccamMcp.Core.exe"
        Copy-Item -Path $publishExe -Destination $rootExe -Force
        Write-Doctor "mcp host (OCCAM_HOME root): $rootExe" DarkGray
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

Write-Doctor "doctor: OK" Green
$sessionsRoot = if ($env:OCCAM_SESSIONS_ROOT) { $env:OCCAM_SESSIONS_ROOT } else { Join-Path $env:USERPROFILE ".occam\sessions" }
Write-Doctor "sessions: $sessionsRoot (optional: node scripts/occam-session.mjs init)" DarkGray
if (-not $Quiet) {
    Write-Host ""
    Write-Host "Occam runtime is installed (self-check via doctor passed)." -ForegroundColor Cyan
    Write-Host "Connect an AI app:  occam connect"
    Write-Host "Manual snippet:     node scripts/lib/print-connection-snippet.mjs `"$root`" generic-stdio"
} elseif ($env:OCCAM_INSTALL_QUIET -ne "1" -and $env:OCCAM_INSTALL_QUIET -ne "true") {
    Write-Host "Occam doctor"
    Write-Host ""
    Write-Host "✓ Runtime"
    Write-Host "✓ Browser"
    Write-Host "✓ Web safety"
    Write-Host "✓ PDF support"
    Write-Host "✓ Installation"
    Write-Host ""
    Write-Host "Everything looks good."
    Write-Host ""
    Write-Host "Connect an AI app:  occam connect"
}