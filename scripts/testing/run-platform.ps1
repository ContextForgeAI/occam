# Cross-platform verification run (Windows leg) for the Occam proof-of-read canary.
# Mirrors artifacts/xplat/run-platform.sh so docs/testing/ artefacts are comparable per platform.
param(
    [string]$PlatformLabel = "windows-x64",
    [string]$Rid = "win-x64"
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$out = Join-Path $repo "docs/testing/$PlatformLabel"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

# ---------- runtime info ----------
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$os = Get-CimInstance Win32_OperatingSystem
@(
    "platform_label=$PlatformLabel"
    "rid=$Rid"
    "collected_at=$([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))"
    ""
    "== os =="
    "caption=$($os.Caption)"
    "version=$($os.Version)"
    "build=$($os.BuildNumber)"
    "architecture=$($os.OSArchitecture)"
    ""
    "== cpu =="
    "model=$($cpu.Name)"
    "cores=$($cpu.NumberOfCores)"
    "logical=$($cpu.NumberOfLogicalProcessors)"
    "mem_bytes=$($os.TotalVisibleMemorySize * 1024)"
    ""
    "== dotnet =="
    (dotnet --version)
) | Set-Content -Path (Join-Path $out "runtime-info.txt")
dotnet --info 2>&1 | Select-Object -First 20 | Add-Content -Path (Join-Path $out "runtime-info.txt")

Push-Location $repo

# ---------- build ----------
$buildLog = Join-Path $out "build.log"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
"### dotnet restore" | Set-Content $buildLog
dotnet restore src/FFOccamMcp.Core/FFOccamMcp.Core.csproj 2>&1 | Add-Content $buildLog
"restore_core_exit=$LASTEXITCODE" | Add-Content $buildLog
dotnet restore tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj 2>&1 | Add-Content $buildLog
"restore_tests_exit=$LASTEXITCODE" | Add-Content $buildLog
"" | Add-Content $buildLog
"### dotnet build -c Release (Core)" | Add-Content $buildLog
dotnet build src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release --no-restore 2>&1 | Add-Content $buildLog
"build_core_exit=$LASTEXITCODE" | Add-Content $buildLog
"" | Add-Content $buildLog
"### dotnet build -c Release (Tests, warnings-as-errors)" | Add-Content $buildLog
dotnet build tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj -c Release --no-restore 2>&1 | Add-Content $buildLog
"build_tests_exit=$LASTEXITCODE" | Add-Content $buildLog
$sw.Stop()
"build_wall_seconds=$([int]$sw.Elapsed.TotalSeconds)" | Add-Content $buildLog

# ---------- tests + coverage ----------
$testLog = Join-Path $out "test.log"
$testResults = Join-Path $out "TestResults"
"### dotnet test --collect:XPlat Code Coverage" | Set-Content $testLog
dotnet test tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj -c Release --no-build `
    --collect:"XPlat Code Coverage" --results-directory $testResults 2>&1 | Add-Content $testLog
"test_exit=$LASTEXITCODE" | Add-Content $testLog

$coverage = Get-ChildItem -Path $testResults -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($coverage) { Copy-Item $coverage.FullName (Join-Path $out "coverage.cobertura.xml") -Force }

# ---------- canary ----------
$coreDll = Join-Path $repo "src/FFOccamMcp.Core/bin/Release/net10.0/OccamMcp.Core.dll"
$canaryLog = Join-Path $out "canary-smoke.log"
"### occam canary selftest" | Set-Content $canaryLog
dotnet $coreDll canary selftest 2>&1 | Add-Content $canaryLog
"selftest_exit=$LASTEXITCODE" | Add-Content $canaryLog
"" | Add-Content $canaryLog
"### occam canary vectors --verify docs/testing/canary-vectors.json" | Add-Content $canaryLog
dotnet $coreDll canary vectors --verify docs/testing/canary-vectors.json 2>&1 | Add-Content $canaryLog
"vectors_exit=$LASTEXITCODE" | Add-Content $canaryLog
"" | Add-Content $canaryLog
"### occam canary smoke (HTTP issue -> read -> verify -> reject)" | Add-Content $canaryLog
dotnet $coreDll canary smoke 2>&1 | Add-Content $canaryLog
"smoke_exit=$LASTEXITCODE" | Add-Content $canaryLog

# ---------- canary vectors: semantic re-derive plus a byte comparison ----------
# --out, not a shell redirect: PowerShell re-encodes the stream, so `> file` is not byte-faithful.
$vectorsLog = Join-Path $out "canary-vectors.log"
$localVectors = Join-Path $out "canary-vectors-local.json"
$baselineVectors = Join-Path $repo "docs/testing/canary-vectors.json"
"### semantic check: re-derive every committed vector" | Set-Content $vectorsLog
dotnet $coreDll canary vectors --verify $baselineVectors 2>&1 | Add-Content $vectorsLog
"vectors_exit=$LASTEXITCODE" | Add-Content $vectorsLog
"" | Add-Content $vectorsLog
"### byte check: emit to a file and compare SHA-256 with the committed baseline" | Add-Content $vectorsLog
dotnet $coreDll canary vectors --emit --out $localVectors 2>&1 | Add-Content $vectorsLog
$baseHash = (Get-FileHash $baselineVectors -Algorithm SHA256).Hash.ToLower()
$localHash = (Get-FileHash $localVectors -Algorithm SHA256).Hash.ToLower()
"baseline_sha256=$baseHash" | Add-Content $vectorsLog
"local_sha256=$localHash" | Add-Content $vectorsLog
"baseline_bytes=$((Get-Item $baselineVectors).Length)" | Add-Content $vectorsLog
"local_bytes=$((Get-Item $localVectors).Length)" | Add-Content $vectorsLog
if ($baseHash -eq $localHash) { "vectors_byte_identical=yes" | Add-Content $vectorsLog }
else { "vectors_byte_identical=no" | Add-Content $vectorsLog }

# ---------- capability exam ----------
$examLog = Join-Path $out "exam-selftest.log"
$examTasks = Join-Path $out "exam-tasks.json"
"### occam exam selftest" | Set-Content $examLog
dotnet $coreDll exam selftest 2>&1 | Add-Content $examLog
"selftest_exit=$LASTEXITCODE" | Add-Content $examLog
"" | Add-Content $examLog
"### occam exam tasks (catalogue must be LF-only and platform-identical)" | Add-Content $examLog
# --out, not a shell redirect: PowerShell reassembles stdout line by line and would rewrite the
# newlines this check exists to verify.
dotnet $coreDll exam tasks --out $examTasks 2>&1 | Add-Content $examLog
"tasks_exit=$LASTEXITCODE" | Add-Content $examLog
"tasks_sha256=$((Get-FileHash $examTasks -Algorithm SHA256).Hash.ToLower())" | Add-Content $examLog
"tasks_bytes=$((Get-Item $examTasks).Length)" | Add-Content $examLog
$tasksBytes = [System.IO.File]::ReadAllBytes($examTasks)
if ($tasksBytes -contains 13) { "tasks_lf_only=no" | Add-Content $examLog } else { "tasks_lf_only=yes" | Add-Content $examLog }

# ---------- Native AOT publish ----------
$aotLog = Join-Path $out "aot-publish.log"
$publishDir = Join-Path $repo "artifacts/xplat/publish/$PlatformLabel"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
"### dotnet publish -c Release -r $Rid (Native AOT)" | Set-Content $aotLog
dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r $Rid -o $publishDir 2>&1 | Add-Content $aotLog
"publish_exit=$LASTEXITCODE" | Add-Content $aotLog
$sw.Stop()
"publish_wall_seconds=$([int]$sw.Elapsed.TotalSeconds)" | Add-Content $aotLog

$bin = Join-Path $publishDir "OccamMcp.Core.exe"
if (Test-Path $bin) {
    $item = Get-Item $bin
    @("binary=$bin", "size_bytes=$($item.Length)", "file_type=PE32+ executable (win-x64, Native AOT)") |
        Set-Content (Join-Path $out "binary-size.txt")
    (Get-FileHash $bin -Algorithm SHA256).Hash.ToLower() + "  " + $bin |
        Set-Content (Join-Path $out "hash.txt")

    $aotCanary = Join-Path $out "canary-aot.log"
    "### AOT binary: canary selftest" | Set-Content $aotCanary
    & $bin canary selftest 2>&1 | Add-Content $aotCanary
    "aot_selftest_exit=$LASTEXITCODE" | Add-Content $aotCanary
    "" | Add-Content $aotCanary
    "### AOT binary: canary vectors --verify" | Add-Content $aotCanary
    & $bin canary vectors --verify (Join-Path $repo "docs/testing/canary-vectors.json") 2>&1 | Add-Content $aotCanary
    "aot_vectors_exit=$LASTEXITCODE" | Add-Content $aotCanary
    "" | Add-Content $aotCanary
    "### AOT binary: canary smoke" | Add-Content $aotCanary
    & $bin canary smoke 2>&1 | Add-Content $aotCanary
    "aot_smoke_exit=$LASTEXITCODE" | Add-Content $aotCanary
    "" | Add-Content $aotCanary
    "### AOT binary: exam selftest" | Add-Content $aotCanary
    & $bin exam selftest 2>&1 | Add-Content $aotCanary
    "aot_exam_exit=$LASTEXITCODE" | Add-Content $aotCanary
}
else {
    @("binary=absent", "note=Native AOT publish did not produce a binary on this host; see aot-publish.log") |
        Set-Content (Join-Path $out "binary-size.txt")
    "no binary to hash" | Set-Content (Join-Path $out "hash.txt")
    "skipped: no AOT binary on this host" | Set-Content (Join-Path $out "canary-aot.log")
}

Pop-Location
Write-Output "RUN_COMPLETE $PlatformLabel $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))"
