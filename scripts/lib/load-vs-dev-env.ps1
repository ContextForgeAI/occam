<#
.SYNOPSIS
    Dot-source helper: loads the Visual Studio x64 C++ toolchain into the current shell.

.DESCRIPTION
    Native AOT publish needs the MSVC linker (link.exe) plus its environment. A plain
    PowerShell session lacks it, so `dotnet publish` fails at the native link step with
    "vswhere.exe is not recognized" / link.exe exit 123.

    Dot-source this file, then call Enter-OccamVsDevEnv. It is a no-op when:
      - not on Windows, or
      - the toolchain is already loaded (link.exe resolves on PATH).

    On failure to locate Visual Studio it writes a warning and returns $false rather than
    throwing, so non-AOT callers keep working.

.EXAMPLE
    . "$PSScriptRoot\lib\load-vs-dev-env.ps1"
    Enter-OccamVsDevEnv
#>

function Enter-OccamVsDevEnv {
    [CmdletBinding()]
    param()

    if ($env:OS -ne "Windows_NT") {
        return $true  # POSIX clang/ld toolchain — nothing to load.
    }

    # Already in a developer shell? Don't re-enter.
    if (Get-Command link.exe -ErrorAction SilentlyContinue) {
        return $true
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        Write-Warning "vswhere.exe not found — install VS Build Tools with 'Desktop development with C++' for AOT publish."
        return $false
    }

    $vsPath = & $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -property installationPath
    if ([string]::IsNullOrWhiteSpace($vsPath)) {
        Write-Warning "No VS install with the x64 C++ toolchain (VC.Tools.x86.x64). Add it via the VS Installer."
        return $false
    }

    $devShell = Join-Path $vsPath "Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
    if (-not (Test-Path $devShell)) {
        Write-Warning "DevShell module not found at $devShell"
        return $false
    }

    Import-Module $devShell
    Enter-VsDevShell -VsInstallPath $vsPath -SkipAutomaticLocation `
        -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null

    # Enter-VsDevShell loads MSVC (link.exe, INCLUDE, LIB) but not the VS Installer dir.
    # The AOT ILCompiler targets shell out to `vswhere.exe` by bare name to find the
    # linker, so that dir must be on PATH too.
    $installerDir = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
    if (Test-Path (Join-Path $installerDir "vswhere.exe")) {
        $env:PATH = "$installerDir;$env:PATH"
    }

    Write-Host "Loaded VS x64 developer environment: $vsPath" -ForegroundColor DarkGray
    return $true
}
