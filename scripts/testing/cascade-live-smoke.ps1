# Live cascade smoke — three public URLs, wall-clock latency, no invented numbers.
# Usage (from repo root, OCCAM_HOME set):
#   pwsh scripts/testing/cascade-live-smoke.ps1
#   pwsh scripts/testing/cascade-live-smoke.ps1 -OutDir docs/testing/windows-x64
param(
    [string]$OutDir = "",
    [string]$PlatformLabel = "windows-x64"
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
if (-not $OutDir) { $OutDir = Join-Path $repo "docs/testing/$PlatformLabel" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$env:OCCAM_HOME = $repo
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$urls = @(
    'https://example.com',
    'https://nginx.org/',
    'https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404'
)

$log = Join-Path $OutDir 'cascade-live-smoke.log'
$jsonl = Join-Path $OutDir 'cascade-live-smoke.jsonl'
"### cascade live smoke $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')" | Set-Content $log
"repo=$repo" | Add-Content $log
"platform=$PlatformLabel" | Add-Content $log
"" | Add-Content $log

if (Test-Path $jsonl) { Remove-Item $jsonl }

$latencies = [System.Collections.Generic.List[double]]::new()
$okCount = 0
$failCount = 0

foreach ($url in $urls) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $out = & dotnet run --project (Join-Path $repo 'src/FFOccamMcp.Core') -c Release --no-build -- `
        cascade run --url=$url --task=overview --budget=512 2>&1
    $sw.Stop()
    $ms = [int]$sw.Elapsed.TotalMilliseconds
    $exit = $LASTEXITCODE

    # stdout JSON is the last line that starts with {
    $jsonLine = ($out | Where-Object { $_ -is [string] -and $_.StartsWith('{') } | Select-Object -Last 1)
    if (-not $jsonLine) { $jsonLine = '{}' }

    $parsedOk = $false
    $backend = $null
    $partial = $false
    $failure = $null
    try {
        $obj = $jsonLine | ConvertFrom-Json
        $parsedOk = [bool]$obj.ok
        $backend = $obj.backend
        $partial = [bool]$obj.partial
        if ($obj.failure) { $failure = "$($obj.failure.code):$($obj.failure.message)" }
    } catch {
        $failure = "json_parse_error"
    }

    if ($exit -eq 0 -and $parsedOk) {
        $okCount++
        $latencies.Add([double]$ms)
        $status = 'OK'
    } else {
        $failCount++
        $status = 'FAIL'
    }

    $row = [ordered]@{
        url = $url
        ok = ($exit -eq 0 -and $parsedOk)
        exitCode = $exit
        latencyMs = $ms
        backend = $backend
        partial = $partial
        failure = $failure
        collectedAt = [DateTime]::UtcNow.ToString('o')
    }
    ($row | ConvertTo-Json -Compress) | Add-Content $jsonl

    "url=$url status=$status exit=$exit latency_ms=$ms backend=$backend partial=$partial failure=$failure" | Add-Content $log
    Write-Host "cascade live: $status ${ms}ms $url"
}

"" | Add-Content $log
"ok_count=$okCount" | Add-Content $log
"fail_count=$failCount" | Add-Content $log

if ($latencies.Count -gt 0) {
    $sorted = $latencies | Sort-Object
    $p50 = $sorted[[Math]::Floor(($sorted.Count - 1) * 0.5)]
    $p95 = $sorted[[Math]::Floor(($sorted.Count - 1) * 0.95)]
    $mean = ($sorted | Measure-Object -Average).Average
    "latency_n=$($sorted.Count)" | Add-Content $log
    "latency_p50_ms=$([int]$p50)" | Add-Content $log
    "latency_p95_ms=$([int]$p95)" | Add-Content $log
    "latency_mean_ms=$([int]$mean)" | Add-Content $log
    "latency_min_ms=$([int]$sorted[0])" | Add-Content $log
    "latency_max_ms=$([int]$sorted[-1])" | Add-Content $log
} else {
    "latency_n=0" | Add-Content $log
}

if ($failCount -eq 0 -and $okCount -eq $urls.Count) {
    "CASCADE_LIVE_SMOKE_OK" | Add-Content $log
    Write-Host 'CASCADE_LIVE_SMOKE_OK'
    exit 0
}

Write-Host "CASCADE_LIVE_SMOKE_FAIL ok=$okCount fail=$failCount"
exit 1
