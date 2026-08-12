#!/usr/bin/env node
/**
 * Install a stable user-scoped `occam` launcher.
 *
 * Used by get-ff-occam bootstrap after the verified release archive is installed.
 * Current release archives are self-contained; a source overlay is opt-in only.
 *
 *   node install-user-cli.mjs --home <OCCAM_HOME> [--no-overlay] [--json]
 *
 * PATH persistence on Windows is done via PowerShell User-scope env (no admin).
 * Current-process PATH is printed as `pathForCurrentProcess` / `PATH_PREPEND=<dir>`
 * for the shell wrapper to prepend (Node cannot mutate the parent PowerShell process).
 */
import { spawnSync } from "node:child_process";
import {
  chmodSync,
  existsSync,
  lstatSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  realpathSync,
  renameSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir, homedir, platform } from "node:os";
import { dirname, join, resolve, win32 as pathWin32, posix as pathPosix } from "node:path";
import { fileURLToPath } from "node:url";
import { writeInstallNodeBin } from "../resolve-node-runtime.mjs";

/** Files that must be present in every self-contained release archive. */
export const RELEASE_RUNTIME_FILES = Object.freeze([
  "scripts/install.sh",
  "scripts/install.ps1",
  "scripts/get-ff-occam.sh",
  "scripts/get-ff-occam.ps1",
  "scripts/occam.mjs",
  "scripts/occam.ps1",
  "scripts/occam",
  "scripts/occam-doctor.sh",
  "scripts/occam-doctor.ps1",
  "scripts/check-public-mcp-contract.mjs",
  "corpora/public-mcp-schema-fingerprint.txt",
  "scripts/occam-connect.mjs",
  "scripts/occam-disconnect.mjs",
  "scripts/occam-uninstall.mjs",
  "scripts/occam-onboard.mjs",
  "scripts/occam-help.mjs",
  "scripts/occam-skill-install.mjs",
  "scripts/sync-occam-skill-package.mjs",
  "scripts/occam-refresh-host.mjs",
  "scripts/occam-session.mjs",
  "scripts/hermes-smoke.mjs",
  "scripts/occam-chat.mjs",
  "scripts/lib/prepare-install-replace.mjs",
  "scripts/lib/install-target-inspect.mjs",
  "scripts/lib/archive-preflight.mjs",
  "scripts/lib/verify-release-signature.mjs",
  "scripts/lib/stop-occam-processes.mjs",
  "scripts/lib/resolve-rid.mjs",
  "scripts/lib/verify-install.mjs",
  "scripts/lib/mcp-stdio-client.mjs",
  "scripts/lib/experimental/local-chat/ollama-api.mjs",
  "scripts/lib/experimental/local-chat/tool-surface.mjs",
  "scripts/lib/experimental/local-chat/model-select.mjs",
  "scripts/lib/experimental/local-chat/session.mjs",
  "scripts/lib/experimental/local-chat/chat-loop.mjs",
  "scripts/lib/experimental/local-chat/ollama-endpoint.mjs",
  "scripts/lib/experimental/local-chat/live-friend-test.mjs",
  "scripts/lib/experimental/local-chat/package-friend-candidate.mjs",
  "scripts/lib/resolve-node-runtime.mjs",
  "scripts/lib/operator/connect-onboarding.mjs",
  "scripts/lib/operator/install-connect-flow.mjs",
  "scripts/lib/operator/install-ux.mjs",
  "scripts/lib/operator/post-install-ux.mjs",
  "scripts/lib/operator/onboard-flow.mjs",
  "scripts/lib/operator/onboard-config.mjs",
  "scripts/lib/operator/occam-cli-subcommands.mjs",
  "scripts/lib/operator/occam-cli-dispatch.mjs",
  "scripts/lib/operator/occam-command-registry.mjs",
  "scripts/lib/operator/install-user-cli.mjs",
  "scripts/lib/operator/tty.mjs",
  "scripts/lib/operator/control-actions.mjs",
  "scripts/lib/operator/control-loop.mjs",
  "scripts/lib/operator/update-check.mjs",
  "scripts/lib/operator/uninstall.mjs",
  "scripts/lib/operator/connect/index.mjs",
  "scripts/lib/operator/connect/kinds.mjs",
  "scripts/lib/operator/connect/launch-spec.mjs",
  "scripts/lib/operator/connect/verification.mjs",
  "scripts/lib/operator/connect/policy.mjs",
  "scripts/lib/operator/connect/ownership.mjs",
  "scripts/lib/operator/connect/config-engine.mjs",
  "scripts/lib/operator/connect/codecs.mjs",
  "scripts/lib/operator/connect/paths.mjs",
  "scripts/lib/operator/connect/config-file-adapter.mjs",
  "scripts/lib/operator/connect/process.mjs",
  "scripts/lib/operator/connect/registry.mjs",
  "scripts/lib/operator/connect/orchestrator.mjs",
  "scripts/lib/operator/connect/render.mjs",
  "scripts/lib/operator/connect/runtimes.mjs",
  "scripts/lib/operator/connect/occam-verify.mjs",
  "scripts/lib/stamp-node-runtime-env.mjs",
  "scripts/launch-mcp-host.mjs",
  "scripts/occam-wrapper.sh",
  "scripts/lib/operator/connect/adapters/cursor.mjs",
  "scripts/lib/operator/connect/adapters/claude-desktop.mjs",
  "scripts/lib/operator/connect/adapters/claude-code.mjs",
  "scripts/lib/operator/connect/adapters/codex.mjs",
  "scripts/lib/operator/connect/adapters/gemini.mjs",
  "scripts/lib/operator/connect/adapters/hermes.mjs",
  "scripts/lib/operator/connect/adapters/openclaw.mjs",
  "scripts/lib/operator/connect/adapters/vscode.mjs",
  "scripts/lib/operator/connect/adapters/cline.mjs",
  "scripts/lib/operator/connect/adapters/roo.mjs",
  "scripts/lib/operator/connect/adapters/windsurf.mjs",
  "scripts/lib/operator/connect/adapters/zed.mjs",
  "scripts/lib/operator/connect/adapters/opencode.mjs",
  "scripts/lib/operator/connect/adapters/goose.mjs",
  "scripts/lib/operator/connect/adapters/junie.mjs",
  "workers/package.json",
  "workers/http-extract/extract.mjs",
  "workers/browser-extract/browser-extract.mjs",
  "workers/css-extract/css-extract.mjs",
]);

/** Backward-compatible name for explicit mirror/development overlays. */
export const OPERATOR_OVERLAY_FILES = RELEASE_RUNTIME_FILES;

/** @param {string} root */
export function missingReleaseRuntimeFiles(root) {
  const abs = resolve(root);
  return RELEASE_RUNTIME_FILES.filter((rel) => !existsSync(join(abs, ...rel.split("/"))));
}

/**
 * Validate the complete extracted release tree before an existing install is replaced.
 * @param {string} root
 * @param {{ version: string, rid: string }} expected
 */
export function validateReleaseRoot(root, expected) {
  const abs = resolve(root);
  const problems = missingReleaseRuntimeFiles(abs).map((rel) => `missing ${rel}`);
  const hostName = expected.rid.startsWith("win-") ? "OccamMcp.Core.exe" : "OccamMcp.Core";
  if (!existsSync(join(abs, hostName))) problems.push(`missing ${hostName}`);

  const versionPath = join(abs, "VERSION");
  if (!existsSync(versionPath)) {
    problems.push("missing VERSION");
  } else {
    const actualVersion = readFileSync(versionPath, "utf8").trim();
    if (actualVersion !== expected.version) {
      problems.push(`VERSION mismatch (expected ${expected.version}, got ${actualVersion})`);
    }
  }

  const manifestPath = join(abs, "release-manifest.json");
  if (!existsSync(manifestPath)) {
    problems.push("missing release-manifest.json");
  } else {
    try {
      const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
      if (manifest.version !== expected.version) {
        problems.push(
          `inner manifest version mismatch (expected ${expected.version}, got ${manifest.version})`,
        );
      }
      if (manifest.rid !== expected.rid) {
        problems.push(`inner manifest RID mismatch (expected ${expected.rid}, got ${manifest.rid})`);
      }
      if (manifest.layout !== "level-b") {
        problems.push(`inner manifest layout mismatch (expected level-b, got ${manifest.layout})`);
      }
    } catch (error) {
      problems.push(`invalid release-manifest.json (${error instanceof Error ? error.message : error})`);
    }
  }
  return problems;
}

/**
 * @param {string} [home]
 */
export function resolveUserBinDir(home = homedir()) {
  return join(home, ".local", "bin");
}

/**
 * True when the install tree is missing any operator-overlay file.
 * Covers partial overlays that wrote connect without tty.mjs / chat helpers.
 * @param {string} occamHome
 * @param {{ files?: string[] }} [opts]
 */
export function needsOperatorOverlay(occamHome, opts = {}) {
  const files = opts.files ?? OPERATOR_OVERLAY_FILES;
  for (const rel of files) {
    if (!existsSync(join(occamHome, ...rel.split("/")))) return true;
  }
  return false;
}

/**
 * @param {string} occamHome absolute
 * @param {string} [nodeBin] absolute Node for GUI-PATH-safe user CLI
 */
export function renderWindowsCmdLauncher(occamHome, nodeBin = process.execPath) {
  // Always win32-normalize so Linux CI can assert Windows launcher text.
  const home = pathWin32.resolve(String(occamHome || ""));
  const node = pathWin32.resolve(String(nodeBin || "node"));
  // cmd.exe: quote paths; %* forwards args. OCCAM_HOME pinned to this install.
  // uninstall/disconnect: stage scripts outside OCCAM_HOME first so Node does not
  // keep module handles that block Windows directory removal (EPERM/EBUSY).
  return [
    "@echo off",
    "setlocal",
    "rem Auto-generated by Occam install - do not edit.",
    `set "OCCAM_HOME=${home}"`,
    `set "OCCAM_NODE_BIN=${node}"`,
    `if not exist "%OCCAM_NODE_BIN%" (echo Occam's Node runtime is no longer available at: %OCCAM_NODE_BIN% 1>&2 & echo Reinstall Occam or set OCCAM_NODE_BIN. 1>&2 & exit /b 1)`,
    'if /I "%~1"=="chat" (',
    "  shift",
    '  "%OCCAM_NODE_BIN%" "%OCCAM_HOME%\\scripts\\occam-chat.mjs" %*',
    "  exit /b %ERRORLEVEL%",
    ")",
    'if /I "%~1"=="uninstall" goto :occam_removal',
    'if /I "%~1"=="disconnect" goto :occam_removal',
    '"%OCCAM_NODE_BIN%" "%OCCAM_HOME%\\scripts\\occam.mjs" %*',
    "exit /b %ERRORLEVEL%",
    ":occam_removal",
    'set "OCCAM_STAGE=%TEMP%\\occam-removal-%RANDOM%%RANDOM%"',
    'mkdir "%OCCAM_STAGE%" >nul 2>&1',
    'xcopy /E /I /Y /Q "%OCCAM_HOME%\\scripts" "%OCCAM_STAGE%\\scripts\\" >nul',
    'if errorlevel 1 (',
    '  echo Failed to stage Occam removal scripts outside OCCAM_HOME. 1>&2',
    '  exit /b 1',
    ")",
    'set "OCCAM_UNINSTALL_REEXEC=1"',
    'if /I "%~1"=="uninstall" ("%OCCAM_NODE_BIN%" "%OCCAM_STAGE%\\scripts\\occam-uninstall.mjs" %*) else ("%OCCAM_NODE_BIN%" "%OCCAM_STAGE%\\scripts\\occam-disconnect.mjs" %*)',
    "set ERR=%ERRORLEVEL%",
    'rd /s /q "%OCCAM_STAGE%" >nul 2>&1',
    "exit /b %ERR%",
    "",
  ].join("\r\n");
}

/**
 * PowerShell prefers .ps1 over .cmd when both names resolve on PATH.
 * Ship both so `occam` works in pwsh and cmd.exe.
 * @param {string} occamHome absolute
 * @param {string} [nodeBin]
 */
export function renderWindowsPs1Launcher(occamHome, nodeBin = process.execPath) {
  const home = pathWin32.resolve(String(occamHome || ""));
  const node = pathWin32.resolve(String(nodeBin || "node"));
  const lit = home.replace(/'/g, "''");
  const nodeLit = node.replace(/'/g, "''");
  return [
    "# Auto-generated by Occam install — do not edit.",
    `$env:OCCAM_HOME = '${lit}'`,
    `$env:OCCAM_NODE_BIN = '${nodeLit}'`,
    "if (-not (Test-Path -LiteralPath $env:OCCAM_NODE_BIN)) {",
    "  [Console]::Error.WriteLine(\"Occam's Node runtime is no longer available at: $($env:OCCAM_NODE_BIN)\")",
    "  [Console]::Error.WriteLine('Reinstall Occam or set OCCAM_NODE_BIN.')",
    "  exit 1",
    "}",
    "if ($args.Count -ge 1 -and $args[0] -eq 'chat') {",
    "  $chatArgs = @()",
    "  if ($args.Count -gt 1) { $chatArgs = $args[1..($args.Count - 1)] }",
    "  & $env:OCCAM_NODE_BIN (Join-Path $env:OCCAM_HOME 'scripts\\occam-chat.mjs') @chatArgs",
    "  exit $LASTEXITCODE",
    "}",
    "if ($args.Count -ge 1 -and ($args[0] -eq 'uninstall' -or $args[0] -eq 'disconnect')) {",
    "  $stage = Join-Path $env:TEMP ('occam-removal-' + [guid]::NewGuid().ToString('N'))",
    "  New-Item -ItemType Directory -Force -Path (Join-Path $stage 'scripts') | Out-Null",
    "  Copy-Item -Recurse -Force (Join-Path $env:OCCAM_HOME 'scripts\\*') (Join-Path $stage 'scripts')",
    "  $env:OCCAM_UNINSTALL_REEXEC = '1'",
    "  $script = if ($args[0] -eq 'uninstall') { 'occam-uninstall.mjs' } else { 'occam-disconnect.mjs' }",
    "  & $env:OCCAM_NODE_BIN (Join-Path $stage \"scripts\\$script\") @args",
    "  $code = $LASTEXITCODE",
    "  Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue",
    "  exit $code",
    "}",
    "& $env:OCCAM_NODE_BIN (Join-Path $env:OCCAM_HOME 'scripts\\occam.mjs') @args",
    "exit $LASTEXITCODE",
    "",
  ].join("\r\n");
}

/**
 * @param {string} occamHome absolute
 * @param {string} [nodeBin]
 */
export function renderUnixLauncher(occamHome, nodeBin = process.execPath) {
  const home = pathPosix.resolve(String(occamHome || "").replace(/\\/g, "/"));
  const node = pathPosix.resolve(String(nodeBin || "node").replace(/\\/g, "/"));
  return [
    "#!/usr/bin/env bash",
    "# Auto-generated by Occam install - do not edit.",
    "set -euo pipefail",
    `export OCCAM_HOME=${shellSingleQuote(home)}`,
    `export OCCAM_NODE_BIN=${shellSingleQuote(node)}`,
    'if [ ! -x "$OCCAM_NODE_BIN" ]; then',
    '  echo "Occam Node runtime is no longer available at: $OCCAM_NODE_BIN" >&2',
    '  echo "Reinstall Occam or set OCCAM_NODE_BIN." >&2',
    "  exit 1",
    "fi",
    // Experimental chat: exec directly so Ctrl+C/EOF hit occam-chat (no wrapper hang).
    'if [ "${1:-}" = "chat" ]; then',
    "  shift",
    '  exec "$OCCAM_NODE_BIN" "$OCCAM_HOME/scripts/occam-chat.mjs" "$@"',
    "fi",
    'exec "$OCCAM_NODE_BIN" "$OCCAM_HOME/scripts/occam.mjs" "$@"',
    "",
  ].join("\n");
}

/** @param {string} s */
export function shellSingleQuote(s) {
  return `'${String(s).replace(/'/g, `'\\''`)}'`;
}

/**
 * Split PATH-like string into entries (preserve empties only as separators).
 * @param {string} pathValue
 * @param {string} [delimiter]
 */
export function splitPathEntries(pathValue, delimiter = platform() === "win32" ? ";" : ":") {
  return String(pathValue || "")
    .split(delimiter)
    .map((p) => p.trim())
    .filter((p) => p.length > 0);
}

/**
 * @param {string[]} entries
 * @param {string} dir
 * @param {{ caseInsensitive?: boolean }} [opts]
 */
export function pathHasDir(entries, dir, opts = {}) {
  const target = normalizePathKey(dir, opts.caseInsensitive === true);
  return entries.some((e) => normalizePathKey(e, opts.caseInsensitive === true) === target);
}

/**
 * @param {string[]} entries
 * @param {string} dir
 * @param {{ caseInsensitive?: boolean }} [opts]
 * @returns {{ entries: string[], changed: boolean }}
 */
export function appendPathDir(entries, dir, opts = {}) {
  if (pathHasDir(entries, dir, opts)) {
    return { entries: [...entries], changed: false };
  }
  return { entries: [...entries, dir], changed: true };
}

/**
 * Prepend dir (idempotent). Preferred for the Occam user bin so it wins over
 * older docs that put OCCAM_HOME/scripts on PATH.
 * @param {string[]} entries
 * @param {string} dir
 * @param {{ caseInsensitive?: boolean }} [opts]
 * @returns {{ entries: string[], changed: boolean }}
 */
export function prependPathDir(entries, dir, opts = {}) {
  if (pathHasDir(entries, dir, opts)) {
    return { entries: [...entries], changed: false };
  }
  return { entries: [dir, ...entries], changed: true };
}

/** @param {string} p @param {boolean} caseInsensitive */
function normalizePathKey(p, caseInsensitive) {
  let n = p.replace(/[/\\]+$/, "");
  try {
    n = resolve(n);
  } catch {
    // keep raw
  }
  return caseInsensitive ? n.toLowerCase() : n;
}

/** @param {string} name @param {string} body */
export function looksLikeOwnedUserLauncher(name, body) {
  const text = String(body || "");
  if (name === "occam.ps1") {
    const homeMatch = text.match(/^\$env:OCCAM_HOME = '((?:[^']|'')*)'\r?$/m);
    const nodeMatch = text.match(/^\$env:OCCAM_NODE_BIN = '((?:[^']|'')*)'\r?$/m);
    if (!homeMatch || !nodeMatch) return false;
    const home = homeMatch[1].replace(/''/g, "'");
    const node = nodeMatch[1].replace(/''/g, "'");
    return (
      pathWin32.isAbsolute(home) &&
      pathWin32.isAbsolute(node) &&
      text === renderWindowsPs1Launcher(home, node)
    );
  }
  if (name === "occam.cmd") {
    const homeMatch = text.match(/^set "OCCAM_HOME=([^\r\n]*)"\r?$/m);
    const nodeMatch = text.match(/^set "OCCAM_NODE_BIN=([^\r\n]*)"\r?$/m);
    if (!homeMatch || !nodeMatch) return false;
    const home = homeMatch[1];
    const node = nodeMatch[1];
    if (!pathWin32.isAbsolute(home) || !pathWin32.isAbsolute(node)) return false;
    const current = renderWindowsCmdLauncher(home, node);
    const previous = current.replace("rem Auto-generated by Occam install - do not edit.\r\n", "");
    return text === current || text === previous;
  }
  if (name !== "occam") return false;
  const homeMatch = text.match(/^export OCCAM_HOME=([^\r\n]+)$/m);
  const nodeMatch = text.match(/^export OCCAM_NODE_BIN=([^\r\n]+)$/m);
  if (!homeMatch || !nodeMatch) return false;
  const decodeShellLiteral = (literal) => {
    if (!literal.startsWith("'") || !literal.endsWith("'")) return null;
    const decoded = literal.slice(1, -1).split("'\\''").join("'");
    return shellSingleQuote(decoded) === literal ? decoded : null;
  };
  const home = decodeShellLiteral(homeMatch[1]);
  const node = decodeShellLiteral(nodeMatch[1]);
  if (!home || !node || !pathPosix.isAbsolute(home) || !pathPosix.isAbsolute(node)) return false;
  const current = renderUnixLauncher(home, node);
  const previous = current.replace("# Auto-generated by Occam install - do not edit.\n", "");
  return text === current || text === previous;
}

/**
 * Write one or two launchers as a transaction. Existing unrelated paths fail
 * before staging or changing any launcher.
 * @param {string} binDir
 * @param {string} occamHome
 * @param {{ platform?: string, nodeBin?: string, fsOps?: Record<string, Function> }} [opts]
 */
export function writeUserLauncher(binDir, occamHome, opts = {}) {
  const plat = opts.platform ?? platform();
  const nodeBin = opts.nodeBin || process.execPath;
  const io = {
    chmodSync,
    existsSync,
    lstatSync,
    mkdirSync,
    readFileSync,
    renameSync,
    unlinkSync,
    writeFileSync,
    ...(opts.fsOps || {}),
  };
  io.mkdirSync(binDir, { recursive: true });

  const definitions =
    plat === "win32"
      ? [
          {
            name: "occam.cmd",
            path: join(binDir, "occam.cmd"),
            body: renderWindowsCmdLauncher(occamHome, nodeBin),
          },
          {
            name: "occam.ps1",
            path: join(binDir, "occam.ps1"),
            body: renderWindowsPs1Launcher(occamHome, nodeBin),
          },
        ]
      : [
          {
            name: "occam",
            path: join(binDir, "occam"),
            body: renderUnixLauncher(occamHome, nodeBin),
          },
        ];

  for (const item of definitions) {
    item.previousBody = null;
    let stat;
    try {
      stat = io.lstatSync(item.path);
    } catch (error) {
      if (error && typeof error === "object" && error.code === "ENOENT") continue;
      throw new Error(
        `cannot inspect existing launcher ${item.path}: ${error instanceof Error ? error.message : error}`,
      );
    }
    if (!stat.isFile() || stat.isSymbolicLink()) {
      throw new Error(`refusing to replace unrelated launcher path: ${item.path}`);
    }
    const body = io.readFileSync(item.path, "utf8");
    if (!looksLikeOwnedUserLauncher(item.name, body)) {
      throw new Error(`refusing to overwrite unrelated launcher: ${item.path}`);
    }
    item.previousBody = body;
  }

  const changes = definitions.filter((item) => item.previousBody !== item.body);
  const nonce = `${process.pid}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  for (const [index, item] of changes.entries()) {
    item.tempPath = join(binDir, `.${item.name}.tmp-${nonce}-${index}`);
    item.backupPath = join(binDir, `.${item.name}.bak-${nonce}-${index}`);
    item.backedUp = false;
    item.installed = false;
  }

  const unlinkIfPresent = (file) => {
    if (!file || !io.existsSync(file)) return;
    io.unlinkSync(file);
  };

  try {
    for (const item of changes) {
      io.writeFileSync(item.tempPath, item.body, "utf8");
      if (plat !== "win32") io.chmodSync(item.tempPath, 0o755);
    }
    for (const item of changes) {
      if (item.previousBody !== null) {
        io.renameSync(item.path, item.backupPath);
        item.backedUp = true;
      }
    }
    for (const item of changes) {
      io.renameSync(item.tempPath, item.path);
      item.installed = true;
    }
  } catch (error) {
    const rollbackErrors = [];
    for (const item of [...changes].reverse()) {
      try {
        if (item.installed) unlinkIfPresent(item.path);
        if (item.backedUp && io.existsSync(item.backupPath)) {
          io.renameSync(item.backupPath, item.path);
        }
      } catch (rollbackError) {
        rollbackErrors.push(
          rollbackError instanceof Error ? rollbackError.message : String(rollbackError),
        );
      }
      try {
        unlinkIfPresent(item.tempPath);
      } catch {
        // Preserve the original error; temp files are never launcher targets.
      }
    }
    const detail = error instanceof Error ? error.message : String(error);
    const rollbackDetail = rollbackErrors.length
      ? `; rollback incomplete: ${rollbackErrors.join("; ")}`
      : "";
    throw new Error(`failed to install Occam launcher transaction: ${detail}${rollbackDetail}`);
  }

  for (const item of changes) {
    try {
      unlinkIfPresent(item.backupPath);
    } catch {
      // The new launcher is complete; a stale hidden backup is safer than deleting blindly.
    }
  }
  try {
    writeInstallNodeBin(occamHome, nodeBin);
  } catch {
    // Best-effort compatibility marker; launchers already embed absolute Node.
  }

  const launchers = definitions.map((item) => item.path);
  return {
    launcherPath: plat === "win32" ? launchers[1] : launchers[0],
    kind: plat === "win32" ? "cmd+ps1" : "shell",
    launchers,
    changed: changes.length > 0,
  };
}

/**
 * Persist dir on Windows User PATH (prepend, idempotent). No machine PATH.
 * Prepend so the intentional launcher wins over older docs that put scripts/ on PATH.
 * @param {string} dir
 * @param {{ dryRun?: boolean, run?: (scriptText: string, pathDir: string) => { status: number, stdout: string, stderr: string } }} [opts]
 */
export function ensureWindowsUserPath(dir, opts = {}) {
  const abs = pathWin32.resolve(String(dir || ""));
  if (opts.dryRun) {
    return { changed: true, pathValue: abs, dryRun: true };
  }

  const ps1 = `
$ErrorActionPreference = 'Stop'
$dir = [System.IO.Path]::GetFullPath($env:OCCAM_PATH_DIR)
$user = [Environment]::GetEnvironmentVariable('Path','User')
$parts = @()
if ($user) { $parts = @($user -split ';' | Where-Object { $_ -and $_.Trim() -ne '' }) }
$hit = $false
foreach ($p in $parts) {
  try {
    if ([System.IO.Path]::GetFullPath($p).Equals($dir, [System.StringComparison]::OrdinalIgnoreCase)) { $hit = $true; break }
  } catch {}
}
if (-not $hit) {
  # Prepend — justified: PowerShell resolves the first occam.ps1 on PATH.
  $new = if ($parts.Count -gt 0) { (@($dir) + $parts) -join ';' } else { $dir }
  [Environment]::SetEnvironmentVariable('Path', $new, 'User')
  Write-Output 'CHANGED'
} else {
  # Already present: move to front if not already first (idempotent relocate).
  $first = $null
  if ($parts.Count -gt 0) {
    try { $first = [System.IO.Path]::GetFullPath($parts[0]) } catch { $first = $parts[0] }
  }
  if ($first -and $first.Equals($dir, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Output 'UNCHANGED'
  } else {
    $rest = @()
    foreach ($p in $parts) {
      try {
        if (-not [System.IO.Path]::GetFullPath($p).Equals($dir, [System.StringComparison]::OrdinalIgnoreCase)) {
          $rest += $p
        }
      } catch { $rest += $p }
    }
    $new = (@($dir) + $rest) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $new, 'User')
    Write-Output 'CHANGED'
  }
}
`.trim();

  const run =
    opts.run ??
    ((scriptText, pathDir) => {
      const tmp = mkdtempSync(join(tmpdir(), "occam-path-"));
      const file = join(tmp, "ensure-user-path.ps1");
      writeFileSync(file, scriptText, "utf8");
      const r = spawnSync(
        "powershell.exe",
        ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", file],
        {
          encoding: "utf8",
          env: { ...process.env, OCCAM_PATH_DIR: pathDir },
        },
      );
      return { status: r.status ?? 1, stdout: r.stdout || "", stderr: r.stderr || "" };
    });

  const r = run(ps1, abs);
  if (r.status !== 0) {
    throw new Error(`failed to update User PATH: ${r.stderr || r.stdout || r.status}`);
  }
  // Exact line match — do NOT use /CHANGED/ (matches inside UNCHANGED).
  const line = String(r.stdout || "")
    .split(/\r?\n/)
    .map((s) => s.trim())
    .filter(Boolean)
    .pop();
  const changed = line === "CHANGED";
  return { changed, pathValue: abs, dryRun: false };
}

/**
 * @param {string} baseUrl explicit HTTPS source or a local root for development/private mirrors
 * @param {string} occamHome
 * @param {{ files?: string[], fetchText?: (url: string) => Promise<string> }} [opts]
 */
export async function applyOperatorOverlay(baseUrl, occamHome, opts = {}) {
  const files = opts.files ?? OPERATOR_OVERLAY_FILES;
  const root = String(baseUrl || "").replace(/\/$/, "");
  const isHttp = /^https?:\/\//i.test(root);
  let localRoot = root;
  if (root.toLowerCase().startsWith("file:")) {
    localRoot = fileURLToPath(root);
  }

  const fetchText =
    opts.fetchText ??
    (async (relOrUrl) => {
      if (isHttp) {
        const res = await fetch(relOrUrl);
        if (!res.ok) throw new Error(`HTTP ${res.status} for ${relOrUrl}`);
        return await res.text();
      }
      return readFileSync(join(localRoot, ...String(relOrUrl).split("/")), "utf8");
    });

  const written = [];
  for (const rel of files) {
    const text = await fetchText(isHttp ? `${root}/${rel}` : rel);
    const dest = join(occamHome, ...rel.split("/"));
    mkdirSync(dirname(dest), { recursive: true });
    writeFileSync(dest, text, "utf8");
    if (
      rel.endsWith("/occam") ||
      rel === "scripts/occam" ||
      rel.endsWith("occam-doctor.sh")
    ) {
      try {
        chmodSync(dest, 0o755);
      } catch {
        /* ignore */
      }
    }
    written.push(rel);
  }
  return { written };
}

/**
 * @param {{
 *   occamHome: string,
 *   overlay?: boolean,
 *   launcher?: boolean,
 *   persistPath?: boolean,
 *   baseUrl?: string,
 *   homeDir?: string,
 *   platform?: string,
 * }} opts
 */
export async function installUserCli(opts) {
  const occamHome = resolve(opts.occamHome);
  const homeDir = opts.homeDir ?? homedir();
  const plat = opts.platform ?? platform();
  const binDir = resolveUserBinDir(homeDir);
  const baseUrl = opts.baseUrl || process.env.OCCAM_OVERLAY_BASE_URL?.trim() || "";

  /** @type {string[]} */
  const actions = [];
  let overlayWritten = [];
  // A source overlay is explicit only. Release bootstraps pass --no-overlay and
  // install solely from the already verified archive.
  const forceOverlay = Boolean(opts.baseUrl && String(opts.baseUrl).trim());
  if (opts.overlay !== false && (forceOverlay || needsOperatorOverlay(occamHome))) {
    if (!baseUrl) {
      throw new Error(
        "release archive is incomplete; reinstall from a current release (no source overlay was requested)",
      );
    }
    const r = await applyOperatorOverlay(baseUrl, occamHome);
    overlayWritten = r.written;
    actions.push(`overlay:${overlayWritten.length}`);
  }

  let launcherPath = "";
  if (opts.launcher !== false) {
    const r = writeUserLauncher(binDir, occamHome, { platform: plat });
    launcherPath = r.launcherPath;
    actions.push(`launcher:${r.kind}`);
  }

  let pathPersisted = false;
  if (opts.persistPath !== false) {
    if (plat === "win32") {
      const r = ensureWindowsUserPath(binDir);
      pathPersisted = r.changed;
      actions.push(r.changed ? "user-path:changed" : "user-path:unchanged");
    } else {
      // Unix: many profiles already include ~/.local/bin; we still report it for the shell to export.
      actions.push("user-path:shell-export");
    }
  }

  return {
    ok: true,
    occamHome,
    binDir,
    launcherPath,
    overlayWritten,
    pathPersisted,
    /** Parent shell must append this to process PATH (Node cannot mutate parent). */
    pathForCurrentProcess: binDir,
    actions,
  };
}

function parseArgs(argv) {
  let home = "";
  let checkReleaseRoot = "";
  let expectedVersion = "";
  let expectedRid = "";
  let overlay = true;
  let launcher = true;
  let persistPath = true;
  let json = false;
  let baseUrl = "";
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--home") home = argv[++i] || "";
    else if (a === "--check-release-root") checkReleaseRoot = argv[++i] || "";
    else if (a === "--version") expectedVersion = argv[++i] || "";
    else if (a === "--rid") expectedRid = argv[++i] || "";
    else if (a === "--base-url") baseUrl = argv[++i] || "";
    else if (a === "--no-overlay") overlay = false;
    else if (a === "--no-launcher") launcher = false;
    else if (a === "--no-path") persistPath = false;
    else if (a === "--json") json = true;
    else if (a === "-h" || a === "--help") {
      console.log(
        "usage: node install-user-cli.mjs --home <OCCAM_HOME> [--base-url URL] [--no-overlay] [--no-launcher] [--no-path] [--json]\n       node install-user-cli.mjs --check-release-root <DIR> --version <VER> --rid <RID>",
      );
      process.exit(0);
    }
  }
  if (!home && !checkReleaseRoot) {
    console.error("error: --home or --check-release-root is required");
    process.exit(2);
  }
  if (checkReleaseRoot && (!expectedVersion || !expectedRid)) {
    console.error("error: --check-release-root requires --version and --rid");
    process.exit(2);
  }
  return {
    home,
    checkReleaseRoot,
    expectedVersion,
    expectedRid,
    overlay,
    launcher,
    persistPath,
    json,
    baseUrl,
  };
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.checkReleaseRoot) {
    const problems = validateReleaseRoot(args.checkReleaseRoot, {
      version: args.expectedVersion,
      rid: args.expectedRid,
    });
    if (problems.length > 0) {
      console.error(`error: release archive is incomplete:\n  ${problems.join("\n  ")}`);
      process.exit(1);
    }
    if (args.json) {
      console.log(JSON.stringify({ ok: true, root: resolve(args.checkReleaseRoot) }, null, 2));
    }
    return;
  }
  const result = await installUserCli({
    occamHome: args.home,
    overlay: args.overlay,
    launcher: args.launcher,
    persistPath: args.persistPath,
    baseUrl: args.baseUrl || undefined,
  });
  if (args.json) {
    console.log(JSON.stringify(result, null, 2));
  } else {
    console.log(`occam-user-cli: ok home=${result.occamHome}`);
    console.log(`occam-user-cli: bin=${result.binDir}`);
    console.log(`occam-user-cli: launcher=${result.launcherPath}`);
    console.log(`occam-user-cli: PATH_PREPEND=${result.pathForCurrentProcess}`);
    if (result.overlayWritten.length) {
      console.log(`occam-user-cli: overlay_files=${result.overlayWritten.length}`);
    }
  }
}

/**
 * True when this file is the Node entrypoint. Uses realpath so macOS temp
 * paths (/tmp → /private/tmp, /var → /private/var) still match import.meta.url.
 * A plain path.resolve() compare silently no-ops the CLI after a successful
 * temp-helper stage — the public Mac install regression after MODULE_NOT_FOUND.
 */
export function isDirectCliInvocation(argv1 = process.argv[1], metaUrl = import.meta.url) {
  if (!argv1) return false;
  try {
    return realpathSync(argv1) === realpathSync(fileURLToPath(metaUrl));
  } catch {
    return resolve(argv1) === fileURLToPath(metaUrl);
  }
}

if (isDirectCliInvocation()) {
  main().catch((err) => {
    console.error(err instanceof Error ? err.message : String(err));
    process.exit(1);
  });
}
