#Requires -Version 5.1
param(
    [string]$BaseUrl = "http://127.0.0.1:5051",
    [int]$PollSeconds = 120
)

$ErrorActionPreference = "Stop"

$body = @{
    urls = @("https://example.com/", "https://www.iana.org/domains/reserved")
    backend_policy = "http"
    playbook_policy = "off"
    fit_markdown = $true
} | ConvertTo-Json

Write-Host "POST $BaseUrl/v1/batch/submit"
$submit = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/batch/submit" -ContentType "application/json" -Body $body
$jobId = $submit.job_id
Write-Host "job_id=$jobId accepted=$($submit.accepted_count)"

$deadline = (Get-Date).AddSeconds($PollSeconds)
do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod -Uri "$BaseUrl/v1/batch/$jobId/status"
    Write-Host "state=$($status.state) done=$($status.progress.done)/$($status.progress.total)"
} while ($status.state -notin @("done", "failed") -and (Get-Date) -lt $deadline)

$results = Invoke-RestMethod -Uri "$BaseUrl/v1/batch/$jobId/results?limit=50"
$results.items | ForEach-Object {
    $flag = if ($_.ok) { "OK" } else { "FAIL $($_.failure.code)" }
    Write-Host "$flag $($_.url)"
}

if ($status.state -ne "done") {
    exit 1
}

Write-Host "BATCH_SMOKE_OK"
