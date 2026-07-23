#Requires -Version 5.1
param(
    [switch]$Open,
    [string[]]$Skip
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$env:OCCAM_HOME = $Root

if ($Skip -and $Skip.Count -gt 0) {
    $env:OCCAM_VISUAL_MATRIX_SKIP = ($Skip -join ",")
}

$proj = Join-Path $Root "benchmarks\l0-gate\L0Gate.csproj"
$args = @("--visual-matrix")
if ($Open) { $args += "--open" }

Write-Host "Visual matrix QA (occam_probe + occam_transcode + recipe_a)" -ForegroundColor Cyan
Write-Host "Corpus: corpora\visual-matrix.jsonl" -ForegroundColor DarkGray
Write-Host "Output: artifacts\l0-runs\<timestamp>\ (subfolders per tool)" -ForegroundColor DarkGray
Write-Host "Read:   HOW-TO-READ.ru.md in the run folder" -ForegroundColor DarkGray

dotnet run --project $proj -- @args
exit $LASTEXITCODE
