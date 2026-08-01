$ErrorActionPreference = "Stop"

$fixtureRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $fixtureRoot "..\..\..")).Path

Push-Location $repoRoot
try {
    node (Join-Path $fixtureRoot "reproduce.mjs")
    node (Join-Path $fixtureRoot "reproduce-representative.mjs")
}
finally {
    Pop-Location
}
