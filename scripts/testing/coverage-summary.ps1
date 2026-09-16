# Summarises a Cobertura report: repo-wide totals plus the namespace under test.
# Reports both numbers on purpose — a single blended figure would hide that the canary module is
# covered while the rest of the (pre-existing, gate-tested) host assembly is not.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Namespace = "OccamMcp.Core.Canary.",
    [string]$Label = ""
)

[xml]$doc = Get-Content -LiteralPath $Path -Raw

$all = @{ Lines = 0; Covered = 0; Branches = 0; BranchesCovered = 0 }
$scoped = @{ Lines = 0; Covered = 0; Branches = 0; BranchesCovered = 0 }
$perClass = @()

foreach ($class in $doc.SelectNodes("//class")) {
    $name = $class.name
    $lines = $class.SelectNodes("lines/line")
    $classLines = 0
    $classCovered = 0
    $classBranches = 0
    $classBranchesCovered = 0

    foreach ($line in $lines) {
        $classLines++
        if ([int]$line.hits -gt 0) { $classCovered++ }
        if ($line.branch -eq "True" -and $line.'condition-coverage') {
            # condition-coverage looks like "80% (4/5)"
            if ($line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
                $classBranchesCovered += [int]$Matches[1]
                $classBranches += [int]$Matches[2]
            }
        }
    }

    $all.Lines += $classLines
    $all.Covered += $classCovered
    $all.Branches += $classBranches
    $all.BranchesCovered += $classBranchesCovered

    if ($name -like "$Namespace*") {
        $scoped.Lines += $classLines
        $scoped.Covered += $classCovered
        $scoped.Branches += $classBranches
        $scoped.BranchesCovered += $classBranchesCovered
        $perClass += [pscustomobject]@{
            Class    = $name.Replace($Namespace, "")
            Lines    = $classLines
            Covered  = $classCovered
            LinePct  = if ($classLines -gt 0) { [math]::Round(100.0 * $classCovered / $classLines, 1) } else { 0 }
            BranchPct = if ($classBranches -gt 0) { [math]::Round(100.0 * $classBranchesCovered / $classBranches, 1) } else { $null }
        }
    }
}

function Pct($covered, $total) {
    if ($total -le 0) { return 0 }
    return [math]::Round(100.0 * $covered / $total, 2)
}

if ($Label) { Write-Output "platform: $Label" }
Write-Output "report: $Path"
Write-Output ""
Write-Output "namespace $Namespace"
Write-Output ("  line coverage   : {0}% ({1}/{2})" -f (Pct $scoped.Covered $scoped.Lines), $scoped.Covered, $scoped.Lines)
Write-Output ("  branch coverage : {0}% ({1}/{2})" -f (Pct $scoped.BranchesCovered $scoped.Branches), $scoped.BranchesCovered, $scoped.Branches)
Write-Output ""
Write-Output "whole OccamMcp.Core assembly (includes ~300 pre-existing files covered by the L0 gate, not by xunit)"
Write-Output ("  line coverage   : {0}% ({1}/{2})" -f (Pct $all.Covered $all.Lines), $all.Covered, $all.Lines)
Write-Output ("  branch coverage : {0}% ({1}/{2})" -f (Pct $all.BranchesCovered $all.Branches), $all.BranchesCovered, $all.Branches)
Write-Output ""
Write-Output "per class in scope:"
$perClass | Sort-Object LinePct | Format-Table -AutoSize | Out-String -Width 120 | Write-Output
