$ErrorActionPreference = "Stop"

$fixtureRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $fixtureRoot "..\..\..")).Path

Push-Location $repoRoot
try {
    node (Join-Path $fixtureRoot "reproduce.mjs")
}
finally {
    Pop-Location
}
