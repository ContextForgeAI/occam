#Requires -Version 5.1
<#
.SYNOPSIS
  Occam — one-liner bootstrap for Windows (irm | iex).

.DESCRIPTION
  Node 20+ only — NO git, NO .NET SDK on the install machine.
  Mirrors scripts/get-ff-occam.sh.
  Quiet by default. Set OCCAM_VERBOSE=1 for doctor/smoke internals.

.EXAMPLE
  irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex

.EXAMPLE
  $env:OCCAM_SETUP='manual'
  irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
#>
$ErrorActionPreference = "Stop"

# User-facing glyphs via codepoints only. Windows PowerShell 5.1 `irm | iex` often
# mis-decodes UTF-8 script bodies when the HTTP response omits charset=utf-8
# (literal U+2713 becomes mojibake "â" / U+2026 becomes "â¦"). Node later prints
# UTF-8 correctly — do not put raw multi-byte UTF-8 in Write-Host string literals.
$script:OccamOk = [string][char]0x2713       # check mark
$script:OccamFail = [string][char]0x2717     # ballot X
$script:OccamEllipsis = [string][char]0x2026 # horizontal ellipsis
$script:OccamBullet = [string][char]0x2022   # bullet
$script:OccamEmDash = [string][char]0x2014   # em dash

$Version = if ($env:OCCAM_VERSION) { $env:OCCAM_VERSION } else { "1.0.0-rc.2" }
$Rid = if ($env:OCCAM_RID) { $env:OCCAM_RID } else { "win-x64" }
$InstallDir = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else {
  Join-Path $env:USERPROFILE ".local\share\ff-occam"
}
# Legacy fallback for connection snippet only — never printed as a selected host before connect.
$HostTarget = if ($env:OCCAM_HOST) { $env:OCCAM_HOST } else { "" }
$AllowHttp = if ($env:OCCAM_RELEASE_ALLOW_HTTP) { $env:OCCAM_RELEASE_ALLOW_HTTP } else { "0" }
$SetupMode = if ($env:OCCAM_SETUP) { $env:OCCAM_SETUP.Trim().ToLowerInvariant() } else { "" }
$VerboseInstall = ($env:OCCAM_VERBOSE -eq "1" -or $env:OCCAM_VERBOSE -eq "true" -or
                   $env:OCCAM_DEBUG -eq "1" -or $env:OCCAM_DEBUG -eq "true")

$ReleaseBase = if ($env:OCCAM_RELEASE_BASE) {
  $env:OCCAM_RELEASE_BASE
} else {
  "https://github.com/ContextForgeAI/occam/releases/download/v$Version"
}
$ReleaseUrl = if ($env:OCCAM_RELEASE_URL) {
  $env:OCCAM_RELEASE_URL
} else {
  "$ReleaseBase/ff-occam-$Version-$Rid.tar.gz"
}
$ManifestUrl = if ($env:OCCAM_RELEASE_MANIFEST_URL) {
  $env:OCCAM_RELEASE_MANIFEST_URL
} else {
  "$ReleaseBase/ff-occam-$Version-$Rid-manifest.json"
}

$MinNodeMajor = 20

function Assert-UrlScheme([string]$Url) {
  if ($Url -match '^https://') { return }
  if ($Url -match '^http://') {
    if ($AllowHttp -eq "1") {
      Write-Warning ("OCCAM_RELEASE_ALLOW_HTTP=1 " + $script:OccamEmDash + " HTTP release URL")
      return
    }
    throw "release URL must be HTTPS, or set OCCAM_RELEASE_ALLOW_HTTP=1"
  }
  throw "invalid release URL: $Url"
}

function Test-NodeVersion {
  $node = Get-Command node -ErrorAction SilentlyContinue
  if (-not $node) { throw "required command not found: node (Node.js $MinNodeMajor+ required)" }
  $major = [int]((node -p "process.versions.node.split('.')[0]").Trim())
  if ($major -lt $MinNodeMajor) {
    throw "Node.js $MinNodeMajor+ required (found $(node -v))"
  }
  if ($VerboseInstall) { Write-Host "node: $(node -v)" }
}

function Test-TrulyInteractive {
  if ($env:CI -or $env:GITHUB_ACTIONS) { return $false }
  if (-not [Environment]::UserInteractive) { return $false }
  if ([Console]::IsInputRedirected) { return $false }
  if ([Console]::IsOutputRedirected) { return $false }
  return $true
}

function Resolve-SetupMode {
  # Contract: unset|auto|1 → auto; manual|2 → manual; ask → menu only if truly interactive else auto.
  if ($SetupMode -eq "" -or $SetupMode -eq "auto" -or $SetupMode -eq "1") {
    $script:SetupMode = "auto"
    if ($VerboseInstall) {
      if ($env:OCCAM_SETUP) { Write-Host "setup: auto (from OCCAM_SETUP)" }
      else { Write-Host "setup: auto (default)" }
    }
    return
  }
  if ($SetupMode -eq "manual" -or $SetupMode -eq "2") {
    $script:SetupMode = "manual"
    if ($VerboseInstall) { Write-Host "setup: manual (from OCCAM_SETUP)" }
    return
  }
  if ($SetupMode -ne "ask") {
    throw "OCCAM_SETUP must be auto|manual|ask (got $SetupMode)"
  }

  if (-not (Test-TrulyInteractive)) {
    $script:SetupMode = "auto"
    if ($VerboseInstall) { Write-Host "setup: auto (ask ignored: non-interactive)" }
    return
  }

  Write-Host ""
  Write-Host "  First-run setup"
  Write-Host ("  [1] Auto   " + $script:OccamEmDash + " detect and connect supported AI apps")
  Write-Host ("  [2] Manual " + $script:OccamEmDash + " choose which AI app to connect")
  Write-Host ""
  $choice = Read-Host "  Setup [1]"
  if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }
  if ($choice -match '^(2|manual)$') { $script:SetupMode = "manual" } else { $script:SetupMode = "auto" }
  if ($VerboseInstall) { Write-Host "setup: $($script:SetupMode)" }
}

function Get-Sha256Hex([string]$Path) {
  (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Download-File([string]$Url, [string]$Dest) {
  Assert-UrlScheme $Url
  if ($VerboseInstall) { Write-Host "download: $Url" }
  try {
    Invoke-WebRequest -Uri $Url -OutFile $Dest -UseBasicParsing
  } catch {
    Write-Error @"
download failed $($script:OccamEmDash) is the release tarball published?
  url: $Url
  maintainer: tag v$Version and ensure GitHub Release assets exist
  see: INSTALL.md
"@
    throw
  }
}

# Install ~/.local/bin/occam.cmd + occam.ps1 (+ User PATH). Overlay connect CLI from
# public main when the release tarball predates occam-connect. Prepends bin onto
# current-process PATH so PowerShell resolves this launcher first.
function Install-OccamUserCommand([string]$OccamHome) {
  $helper = Join-Path $OccamHome "scripts\lib\operator\install-user-cli.mjs"
  $helperTmp = $null
  if (-not (Test-Path $helper)) {
    $helperTmp = Join-Path ([System.IO.Path]::GetTempPath()) ("occam-install-user-cli-" + [guid]::NewGuid().ToString("N") + ".mjs")
    $helperUrl = if ($env:OCCAM_OVERLAY_BASE_URL) {
      ($env:OCCAM_OVERLAY_BASE_URL.TrimEnd("/") + "/scripts/lib/operator/install-user-cli.mjs")
    } else {
      "https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/lib/operator/install-user-cli.mjs"
    }
    try {
      Invoke-WebRequest -Uri $helperUrl -OutFile $helperTmp -UseBasicParsing
      $helper = $helperTmp
    } catch {
      Write-Host ($script:OccamFail + " Could not install the occam command (download failed).") -ForegroundColor Red
      Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for details." -ForegroundColor Yellow
      throw
    }
  }

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $cliArgs = @($helper, "--home", $OccamHome, "--json")
    if ($env:OCCAM_OVERLAY_BASE_URL) {
      $cliArgs += @("--base-url", $env:OCCAM_OVERLAY_BASE_URL.TrimEnd("/").TrimEnd("\"))
    }
    $jsonOut = & node @cliArgs *>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) {
      Write-Host ($script:OccamFail + " Could not install the occam command.") -ForegroundColor Red
      Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for details." -ForegroundColor Yellow
      if ($jsonOut) { $jsonOut | Select-Object -Last 30 | ForEach-Object { Write-Host $_ } }
      exit $LASTEXITCODE
    }
    if ($VerboseInstall -and $jsonOut) {
      Write-Host ($jsonOut | Out-String)
    }
  } finally {
    $ErrorActionPreference = $prevEap
    if ($helperTmp) { Remove-Item -Force $helperTmp -ErrorAction SilentlyContinue }
  }

  $binDir = $null
  try {
    $parsed = ($jsonOut | Out-String) | ConvertFrom-Json
    $binDir = [string]$parsed.pathForCurrentProcess
    if (-not $binDir) { $binDir = [string]$parsed.binDir }
  } catch {
    $binDir = Join-Path $env:USERPROFILE ".local\bin"
  }

  if ($binDir) {
    $binDir = [System.IO.Path]::GetFullPath($binDir)
    $parts = @($env:PATH -split ';' | Where-Object { $_ -and $_.Trim() -ne '' })
    $hit = $false
    foreach ($p in $parts) {
      try {
        if ([System.IO.Path]::GetFullPath($p).Equals($binDir, [StringComparison]::OrdinalIgnoreCase)) {
          $hit = $true
          break
        }
      } catch {}
    }
    if (-not $hit) {
      # Prepend — same policy as User PATH persistence (see install-user-cli.mjs).
      $env:PATH = (@($binDir) + $parts) -join ';'
    } else {
      # Already present later in PATH — move to front for this process so we win
      # over an older OCCAM_HOME/scripts entry from prior manual installs.
      $rest = @($parts | Where-Object {
        try { -not [System.IO.Path]::GetFullPath($_).Equals($binDir, [StringComparison]::OrdinalIgnoreCase) }
        catch { $true }
      })
      $env:PATH = (@($binDir) + $rest) -join ';'
    }
  }

  # Drop cached command lookup so a stale miss from before PATH mutation is not reused.
  if (Get-Command occam -ErrorAction SilentlyContinue) {
    Remove-Item -Path "Function:occam" -ErrorAction SilentlyContinue
  }
  $cmd = Get-Command occam -ErrorAction SilentlyContinue
  if (-not $cmd) {
    Write-Host ($script:OccamFail + " occam command is not available on PATH after install.") -ForegroundColor Red
    Write-Host "Open a new PowerShell, or run: `$env:PATH = '$binDir;' + `$env:PATH" -ForegroundColor Yellow
    exit 1
  }
}

# Run a legacy-tarball child step. Pre-quiet release packs ignore -Quiet/--quiet
# flags (PowerShell also silently drops unknown -Quiet), so default mode MUST
# capture stdout/stderr. Checks still run; diagnostics show only on failure or verbose.
function Invoke-LegacyInstallStep {
  param(
    [Parameter(Mandatory = $true)][string]$Label,
    [Parameter(Mandatory = $true)][scriptblock]$Action
  )
  if ($VerboseInstall) {
    & $Action
    if ($LASTEXITCODE -ne 0) {
      Write-Host ($script:OccamFail + " $Label failed") -ForegroundColor Red
      Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for details." -ForegroundColor Yellow
      exit $LASTEXITCODE
    }
    return
  }

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    # *>&1 is required: doctor uses Write-Host (Information stream), which 2>&1 does not capture.
    $output = & $Action *>&1 | ForEach-Object { "$_" }
  } finally {
    $ErrorActionPreference = $prevEap
  }
  $code = $LASTEXITCODE
  if ($null -eq $code) { $code = 0 }
  if ($code -ne 0) {
    Write-Host ($script:OccamFail + " $Label failed") -ForegroundColor Red
    Write-Host "Re-run with `$env:OCCAM_VERBOSE=1 for full diagnostics." -ForegroundColor Yellow
    if ($output) {
      Write-Host ""
      $output | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
    }
    exit $code
  }
}

# Fail-closed: never pass "" / null / drive-root into destructive filesystem cmdlets.
function Assert-SafeInstallPath([string]$Path, [string]$Label) {
  if ([string]::IsNullOrWhiteSpace($Path)) {
    throw "$Label path is empty (internal installer error)"
  }
  $full = [System.IO.Path]::GetFullPath($Path)
  $root = [System.IO.Path]::GetPathRoot($full)
  if ([string]::IsNullOrWhiteSpace($root)) {
    throw "$Label path is invalid: $Path"
  }
  $normFull = $full.TrimEnd('\', '/')
  $normRoot = $root.TrimEnd('\', '/')
  if ($normFull -eq $normRoot) {
    throw ($Label + " path resolves to a drive root " + $script:OccamEmDash + " refusing: $Path")
  }
}

# Stop install-scoped Occam hosts before replacing the tree. Never deletes.
# Under `irm | iex`, $PSScriptRoot / $PSCommandPath are empty — never Join-Path them.
function Invoke-PrepareInstallReplace([string]$Dir) {
  Assert-SafeInstallPath $Dir "install"
  if (-not (Test-Path -LiteralPath $Dir)) { return $true }

  $helper = $null
  $helperTmp = $null
  # Prefer helpers already on disk (install tree, or file-mode bootstrap beside scripts/lib).
  # Do NOT Join-Path $PSScriptRoot when empty — that throws under irm|iex (Path="").
  $candidates = New-Object System.Collections.Generic.List[string]
  if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $candidates.Add((Join-Path $PSScriptRoot "lib\prepare-install-replace.mjs"))
  }
  $candidates.Add((Join-Path $Dir "scripts\lib\prepare-install-replace.mjs"))
  foreach ($c in $candidates) {
    if (-not [string]::IsNullOrWhiteSpace($c) -and (Test-Path -LiteralPath $c)) {
      $helper = $c
      break
    }
  }
  if (-not $helper) {
    $base = if ($env:OCCAM_OVERLAY_BASE_URL) {
      $env:OCCAM_OVERLAY_BASE_URL.TrimEnd("/").TrimEnd("\")
    } else {
      "https://raw.githubusercontent.com/ContextForgeAI/occam/main"
    }
    $helperDir = Join-Path ([System.IO.Path]::GetTempPath()) ("occam-prepare-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $helperDir -Force | Out-Null
    $helperTmp = $helperDir
    try {
      Invoke-WebRequest -Uri ($base + "/scripts/lib/prepare-install-replace.mjs") -OutFile (Join-Path $helperDir "prepare-install-replace.mjs") -UseBasicParsing
      Invoke-WebRequest -Uri ($base + "/scripts/lib/stop-occam-processes.mjs") -OutFile (Join-Path $helperDir "stop-occam-processes.mjs") -UseBasicParsing
      Invoke-WebRequest -Uri ($base + "/scripts/lib/resolve-rid.mjs") -OutFile (Join-Path $helperDir "resolve-rid.mjs") -UseBasicParsing
      $helper = Join-Path $helperDir "prepare-install-replace.mjs"
    } catch {
      Write-Host @"
Occam is currently in use.

Close or restart these AI apps before updating:
$($script:OccamBullet) Any app that has Occam connected (Cursor, Claude Desktop, $($script:OccamEllipsis))

Then run the installer again.

No files were changed.
"@
      if ($helperTmp) { Remove-Item -Recurse -Force $helperTmp -ErrorAction SilentlyContinue }
      exit 2
    }
  }

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $jsonOut = & node $helper --dir $Dir --json *>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $prevEap
    if ($helperTmp) { Remove-Item -Recurse -Force $helperTmp -ErrorAction SilentlyContinue }
  }

  if ($code -eq 0) { return $true }

  $msg = $null
  try {
    $parsed = ($jsonOut | Out-String) | ConvertFrom-Json
    $msg = [string]$parsed.message
  } catch {}
  if (-not $msg) {
    $msg = @"
Occam is currently in use.

Close or restart these AI apps before updating:
$($script:OccamBullet) Any app that has Occam connected (Cursor, Claude Desktop, $($script:OccamEllipsis))

Then run the installer again.

No files were changed.
"@
  }
  Write-Host $msg
  exit 2
}

function Replace-OccamInstallTree([string]$TargetDir, [string]$StagedDir) {
  Assert-SafeInstallPath $TargetDir "install"
  Assert-SafeInstallPath $StagedDir "staging"
  $backup = $null
  $attempts = 3
  for ($i = 1; $i -le $attempts; $i++) {
    Invoke-PrepareInstallReplace $TargetDir | Out-Null

    if (-not (Test-Path -LiteralPath $TargetDir)) {
      break
    }

    $backup = "$TargetDir.pre-replace-$([guid]::NewGuid().ToString('N').Substring(0,8))"
    Assert-SafeInstallPath $backup "backup"
    try {
      Move-Item -LiteralPath $TargetDir -Destination $backup -Force -ErrorAction Stop
      break
    } catch {
      $backup = $null
      if ($i -eq $attempts) {
        Write-Host @"
Occam is currently in use.

The existing install could not be moved aside (file lock).
Close or restart these AI apps before updating:
$($script:OccamBullet) Cursor
$($script:OccamBullet) Claude Desktop
$($script:OccamBullet) Any other app with Occam connected

Then run the installer again.

No files were changed.
"@
        exit 2
      }
      Start-Sleep -Milliseconds 700
    }
  }

  try {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Get-ChildItem -LiteralPath $StagedDir | ForEach-Object {
      Move-Item -LiteralPath $_.FullName -Destination $TargetDir -Force -ErrorAction Stop
    }
  } catch {
    if ($backup -and (Test-Path -LiteralPath $backup)) {
      Assert-SafeInstallPath $TargetDir "install"
      Assert-SafeInstallPath $backup "backup"
      Remove-Item -Recurse -Force $TargetDir -ErrorAction SilentlyContinue
      Move-Item -LiteralPath $backup -Destination $TargetDir -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Install failed while replacing files." -ForegroundColor Red
    Write-Host "The previous Occam install was restored when possible." -ForegroundColor Yellow
    throw
  }

  if ($backup) {
    Assert-SafeInstallPath $backup "backup"
    Remove-Item -Recurse -Force $backup -ErrorAction SilentlyContinue
  }
}

Resolve-SetupMode
Test-NodeVersion

if (-not (Get-Command tar.exe -ErrorAction SilentlyContinue)) {
  throw "required command not found: tar.exe"
}

if ($VerboseInstall) {
  Write-Host ""
  Write-Host "install_dir: $InstallDir"
  if ($HostTarget) { Write-Host "host_hint: $HostTarget" }
  Write-Host "release_url: $ReleaseUrl"
  Write-Host ""
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("ff-occam-get-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
try {
  $manifestPath = Join-Path $tmp "manifest.json"
  $tarballPath = Join-Path $tmp "release.tar.gz"

  Download-File $ManifestUrl $manifestPath
  $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
  $expectedSha = [string]$manifest.sha256
  if ([string]::IsNullOrWhiteSpace($expectedSha)) { throw "manifest missing sha256" }
  $expectedSha = $expectedSha.ToLowerInvariant()

  Download-File $ReleaseUrl $tarballPath
  $actualSha = Get-Sha256Hex $tarballPath
  if ($actualSha -ne $expectedSha) {
    throw "sha256 mismatch`n  expected: $expectedSha`n  actual:   $actualSha"
  }
  if ($VerboseInstall) {
    Write-Host "sha256: OK"
    Write-Host "release: version=$($manifest.version) rid=$($manifest.rid)"
  }

  $parent = Split-Path -Parent $InstallDir
  if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }

  $extractTmp = Join-Path $tmp "extract"
  New-Item -ItemType Directory -Path $extractTmp -Force | Out-Null
  tar.exe -xzf $tarballPath -C $extractTmp
  $inner = Get-ChildItem $extractTmp | Select-Object -First 1
  if ($null -eq $inner) { throw "empty tarball" }
  $staged = $extractTmp
  if ($inner.PSIsContainer -and (@(Get-ChildItem $extractTmp).Count -eq 1)) {
    $staged = $inner.FullName
  }

  # Download/stage first → stop install-scoped hosts → swap. Never delete before stage is ready.
  Replace-OccamInstallTree -TargetDir $InstallDir -StagedDir $staged
  if ($VerboseInstall) { Write-Host "extracted: $InstallDir" }
} finally {
  Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

$env:OCCAM_HOME = $InstallDir
Set-Location $InstallDir
if ($HostTarget) { $env:OCCAM_HOST = $HostTarget }

$postUx = Join-Path $InstallDir "scripts\lib\operator\post-install-ux.mjs"
$postArgs = @($postUx, "--setup", $SetupMode, "--version", $Version, "--download-ok")
if ($VerboseInstall) { $postArgs += "--verbose" }

if (Test-Path $postUx) {
  Install-OccamUserCommand $InstallDir
  & node @postArgs
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
  # Legacy release tarball (no post-install-ux.mjs): quiet by capturing child I/O.
  # Published v1.0.0-rc.2 packs predate quiet doctor/verify/smoke flags.
  Write-Host ""
  Write-Host "Occam $Version"
  Write-Host ""
  Write-Host "Installing Occam"
  Write-Host ($script:OccamOk + " Download verified")
  $env:OCCAM_INSTALL_QUIET = if ($VerboseInstall) { "0" } else { "1" }
  $env:OCCAM_BANNER = "0"
  $env:WT_OCCAM_BANNER = "0"

  Write-Host ("  Installing runtime" + $script:OccamEllipsis)
  $doctorPs1 = Join-Path $InstallDir "scripts\occam-doctor.ps1"
  Invoke-LegacyInstallStep -Label "Runtime setup (doctor)" -Action {
    & $doctorPs1 -SkipBuild
  }
  Write-Host ($script:OccamOk + " Runtime installed")
  Write-Host ($script:OccamOk + " Browser ready")

  Write-Host ("  Running self-check" + $script:OccamEllipsis)
  $verifyJs = Join-Path $InstallDir "scripts\lib\verify-install.mjs"
  Invoke-LegacyInstallStep -Label "Host verify" -Action {
    & node $verifyJs --skip-build --version $Version
  }

  $smokeJs = Join-Path $InstallDir "scripts\hermes-smoke.mjs"
  Invoke-LegacyInstallStep -Label "Self-check" -Action {
    & node $smokeJs
  }
  Write-Host ($script:OccamOk + " Self-check passed")

  # Overlay brings current connect CLI + onboarding from public main.
  Install-OccamUserCommand $InstallDir

  # Continue into the SAME connect onboarding as modern post-install-ux.
  $connectJs = Join-Path $InstallDir "scripts\occam-connect.mjs"
  if (Test-Path $connectJs) {
    $connectArgs = @($connectJs)
    if ($VerboseInstall) { $connectArgs += "--verbose" }
    & node @connectArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  } else {
    Write-Host ""
    Write-Host "Occam is installed."
    Write-Host ""
    Write-Host "Connect an AI app later with:"
    Write-Host "  occam connect"
  }
}
