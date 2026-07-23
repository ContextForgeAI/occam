<#
  Samples a process tree's RAM (working set) and cumulative CPU time at a fixed interval,
  appending one CSV line per sample until a stop-file appears. Used by resource-profile.mjs
  to measure an engine's RSS/CPU cost from OUTSIDE the process (the honest way — no in-process
  instrumentation in the AOT product hot path).

  CSV columns: unixMs,rssBytes,cpu100ns,procCount
    cpu100ns  = summed Kernel+User time across the tree
    procCount = number of live processes in the tree (orphan/worker-leak signal)

  Usage:
    powershell -File proc-tree-sampler.ps1 -RootPid 1234 -OutFile samples.csv -StopFile stop.flag [-IntervalMs 150]
#>
param(
  [Parameter(Mandatory = $true)][int]$RootPid,
  [Parameter(Mandatory = $true)][string]$OutFile,
  [Parameter(Mandatory = $true)][string]$StopFile,
  [int]$IntervalMs = 150
)

$ErrorActionPreference = "SilentlyContinue"

function Get-TreeSnapshot([int]$root) {
  $all = Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, WorkingSetSize, KernelModeTime, UserModeTime
  $byParent = @{}
  $byId = @{}
  foreach ($p in $all) {
    $byId[[int]$p.ProcessId] = $p
    $pp = [int]$p.ParentProcessId
    if (-not $byParent.ContainsKey($pp)) { $byParent[$pp] = New-Object System.Collections.Generic.List[int] }
    $byParent[$pp].Add([int]$p.ProcessId)
  }
  $seen = New-Object System.Collections.Generic.HashSet[int]
  $stack = New-Object System.Collections.Stack
  [void]$stack.Push($root)
  $rss = [long]0
  $cpu = [long]0
  while ($stack.Count -gt 0) {
    $id = [int]$stack.Pop()
    if (-not $seen.Add($id)) { continue }
    if ($byId.ContainsKey($id)) {
      $rss += [long]$byId[$id].WorkingSetSize
      $cpu += [long]$byId[$id].KernelModeTime + [long]$byId[$id].UserModeTime
    }
    if ($byParent.ContainsKey($id)) { foreach ($c in $byParent[$id]) { [void]$stack.Push($c) } }
  }
  return @($rss, $cpu, $seen.Count)
}

"unixMs,rssBytes,cpu100ns,procCount" | Out-File -Encoding ascii $OutFile
while (-not (Test-Path $StopFile)) {
  $snap = Get-TreeSnapshot $RootPid
  $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
  "$now,$($snap[0]),$($snap[1]),$($snap[2])" | Out-File -Append -Encoding ascii $OutFile
  Start-Sleep -Milliseconds $IntervalMs
}
