#!/usr/bin/env node
/**
 * Find and stop FF-Occam MCP host processes that lock the published binary.
 * Targets: OccamMcp.Core (+ .exe; current AssemblyName), legacy FFOccamMcp.Core,
 * and node launchers running launch-mcp-host.mjs.
 *
 * When executed directly (`node scripts/lib/stop-occam-processes.mjs`), stops hosts
 * under OCCAM_HOME or the repository root. Used by ci-release-build before publish.
 */
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { resolveRid } from "./resolve-rid.mjs";

/** Current AssemblyName first; legacy tarball/process names retained. */
const HOST_BASE_NAMES = ["OccamMcp.Core", "FFOccamMcp.Core"];

/**
 * Path-boundary check used before any process can become a stop target.
 * @param {string} root
 * @param {string} candidate
 * @param {NodeJS.Platform|string} [platformName]
 */
export function isPathSameOrInside(root, candidate, platformName = process.platform) {
  if (!root || !candidate) return false;
  const pathApi = platformName === "win32" ? path.win32 : path.posix;
  const rootResolved = pathApi.resolve(root);
  const candidateResolved = pathApi.resolve(candidate);
  const rel = pathApi.relative(rootResolved, candidateResolved);
  return rel === "" || (rel !== ".." && !rel.startsWith(`..${pathApi.sep}`) && !pathApi.isAbsolute(rel));
}

/**
 * Match a root in a raw command line only at argument/path boundaries. A plain
 * substring match would make `ff-occam` also match `ff-occam-old`.
 * @param {string} commandLine
 * @param {string} root
 * @param {NodeJS.Platform|string} [platformName]
 */
export function commandLineReferencesRoot(commandLine, root, platformName = process.platform) {
  if (!commandLine || !root) return false;
  const pathApi = platformName === "win32" ? path.win32 : path.posix;
  const resolvedRoot = pathApi.resolve(root).replace(/[\\/]+$/, "");
  const escapedRoot = resolvedRoot.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const separator = platformName === "win32" ? "[\\\\/]" : "/";
  const expression = new RegExp(
    `(?:^|[\\s\"'=])${escapedRoot}(?:${separator}|$|[\\s\"'])`,
    platformName === "win32" ? "i" : "",
  );
  return expression.test(commandLine);
}

/**
 * @param {string} baseName
 * @returns {string}
 */
function withExe(baseName) {
  return process.platform === "win32" ? `${baseName}.exe` : baseName;
}

/**
 * Prefer the currently published AssemblyName; fall back to legacy names.
 * @param {string} root OCCAM_HOME
 * @returns {string}
 */
export function publishExePath(root) {
  const rid = resolveRid();
  const publishDir = path.join(root, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish");
  for (const base of HOST_BASE_NAMES) {
    const candidate = path.join(publishDir, withExe(base));
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }
  return path.join(publishDir, withExe(HOST_BASE_NAMES[0]));
}

/**
 * Host binary at the install / OCCAM_HOME root (release tarball layout).
 * @param {string} root
 */
export function installHostExePath(root) {
  const abs = path.resolve(root);
  for (const base of HOST_BASE_NAMES) {
    const candidate = path.join(abs, withExe(base));
    if (fs.existsSync(candidate)) return candidate;
  }
  return path.join(abs, withExe(HOST_BASE_NAMES[0]));
}

/**
 * @param {string} filePath
 * @returns {boolean}
 */
export function isFileLocked(filePath) {
  if (!fs.existsSync(filePath)) return false;
  try {
    const fd = fs.openSync(filePath, "r+");
    fs.closeSync(fd);
    return false;
  } catch (err) {
    const code = /** @type {NodeJS.ErrnoException} */ (err).code;
    return code === "EBUSY" || code === "EPERM" || code === "EACCES";
  }
}

/**
 * @param {string} root
 * @returns {boolean}
 */
export function isPublishExeLocked(root) {
  return isFileLocked(publishExePath(root));
}

/**
 * @param {string} root install tree
 * @returns {boolean}
 */
export function isInstallHostLocked(root) {
  return isFileLocked(installHostExePath(root));
}

/**
 * @typedef {{ pid: number, name: string, commandLine: string, executablePath?: string }} OccamProcess
 */

/**
 * @param {string} root
 * @param {{ includeDotnet?: boolean }} [opts]
 * @returns {OccamProcess[]}
 */
export function listOccamHostProcesses(root, opts = {}) {
  const includeDotnet = opts.includeDotnet === true;
  const rootResolved = path.resolve(root);

  if (process.platform === "win32") {
    // PowerShell finds only host-shaped candidates. Node applies the exact
    // path-boundary check before any PID can become a stop target.
    const ps = `
$ErrorActionPreference = 'SilentlyContinue'
Get-CimInstance Win32_Process | ForEach-Object {
  $path = $_.ExecutablePath
  $cmd = $_.CommandLine
  $isHost = ($_.Name -eq 'OccamMcp.Core.exe') -or ($_.Name -eq 'FFOccamMcp.Core.exe')
  $isLauncher = $cmd -and ($cmd -match 'launch-mcp-host\\.mjs')
  $isDotnet = ${includeDotnet ? "$true" : "$false"} -and $cmd -and ($cmd -match 'FFOccamMcp\\.Core\\.csproj|OccamMcp\\.Core')
  if ($isHost -or $isLauncher -or $isDotnet) {
    [PSCustomObject]@{ pid = $_.ProcessId; name = $_.Name; commandLine = $cmd; executablePath = $path }
  }
} | ConvertTo-Json -Compress
`.trim();
    try {
      const out = execFileSync(
        "powershell",
        ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps],
        { encoding: "utf8", maxBuffer: 4 * 1024 * 1024 },
      ).trim();
      if (!out) {
        return [];
      }
      const parsed = JSON.parse(out);
      const rows = Array.isArray(parsed) ? parsed : [parsed];
      return rows
        .filter((r) => r?.pid)
        .map((r) => ({
          pid: Number(r.pid),
          name: String(r.name ?? ""),
          commandLine: String(r.commandLine ?? ""),
          executablePath: String(r.executablePath ?? ""),
        }))
        .filter(
          (r) =>
            isPathSameOrInside(rootResolved, r.executablePath, "win32") ||
            commandLineReferencesRoot(r.commandLine, rootResolved, "win32"),
        );
    } catch {
      return [];
    }
  }

  const patterns = [...HOST_BASE_NAMES, "launch-mcp-host.mjs"];
  if (includeDotnet) {
    patterns.push("FFOccamMcp.Core.csproj");
  }
  const grep = patterns.map((p) => p.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|");
  try {
    const out = execFileSync("ps", ["-eo", "pid=,comm=,args="], {
      encoding: "utf8",
      maxBuffer: 8 * 1024 * 1024,
    });
    const seen = new Set();
    /** @type {OccamProcess[]} */
    const found = [];
    for (const line of out.split("\n")) {
      const trimmed = line.trim();
      if (!trimmed || !new RegExp(grep).test(trimmed)) {
        continue;
      }
      const match = trimmed.match(/^(\d+)\s+(\S+)\s+(.*)$/);
      if (!match) {
        continue;
      }
      if (!commandLineReferencesRoot(match[3], rootResolved, process.platform)) {
        continue;
      }
      const pid = Number(match[1]);
      if (!pid || seen.has(pid)) {
        continue;
      }
      seen.add(pid);
      found.push({ pid, name: match[2], commandLine: match[3], executablePath: "" });
    }
    return found;
  } catch {
    return [];
  }
}

/**
 * @param {number} ms
 */
function sleepMs(ms) {
  if (ms <= 0) {
    return;
  }
  const end = Date.now() + ms;
  while (Date.now() < end) {
    // short spin — maintainer script only
  }
}

/**
 * @param {number} pid
 * @param {boolean} force
 */
function killPid(pid, force) {
  if (process.platform === "win32") {
    const args = force ? ["/PID", String(pid), "/F"] : ["/PID", String(pid)];
    spawnSync("taskkill", args, { stdio: "ignore" });
    return;
  }
  try {
    process.kill(pid, force ? "SIGKILL" : "SIGTERM");
  } catch {
    // already gone
  }
}

/**
 * Targeted stop by exact pid (INV-10). Never expands to process-name-wide termination.
 * @param {number} pid
 * @param {{ force?: boolean, graceMs?: number }} [opts]
 * @returns {{ stopped: boolean, pid: number }}
 */
export function stopOccamHostByPid(pid, opts = {}) {
  const force = opts.force !== false;
  const graceMs = opts.graceMs ?? 1500;
  if (!Number.isInteger(pid) || pid <= 0) {
    throw new Error("stopOccamHostByPid requires an exact positive pid");
  }
  killPid(pid, false);
  if (graceMs > 0) {
    sleepMs(graceMs);
  }
  if (force) {
    try {
      process.kill(pid, 0);
      killPid(pid, true);
    } catch {
      // already gone
    }
  }
  return { stopped: true, pid };
}

/**
 * @param {string} root
 * @param {{ dryRun?: boolean, force?: boolean, graceMs?: number, includeDotnet?: boolean }} [opts]
 * @returns {{ stopped: OccamProcess[], stillLocked: boolean }}
 */
export function stopOccamHostProcesses(root, opts = {}) {
  const dryRun = opts.dryRun === true;
  const force = opts.force !== false;
  const graceMs = opts.graceMs ?? 1500;
  const procs = listOccamHostProcesses(root, { includeDotnet: opts.includeDotnet });
  const stopped = [];

  if (dryRun) {
    return { stopped: procs, stillLocked: isPublishExeLocked(root) };
  }

  for (const proc of procs) {
    killPid(proc.pid, false);
    stopped.push(proc);
  }

  if (procs.length > 0 && graceMs > 0) {
    sleepMs(graceMs);
  }

  const remaining = listOccamHostProcesses(root, { includeDotnet: opts.includeDotnet });
  for (const proc of remaining) {
    if (force) {
      killPid(proc.pid, true);
      if (!stopped.some((s) => s.pid === proc.pid)) {
        stopped.push(proc);
      }
    }
  }

  if (remaining.length > 0 && graceMs > 0) {
    sleepMs(Math.min(graceMs, 1000));
  }

  return { stopped, stillLocked: isPublishExeLocked(root) || isInstallHostLocked(root) };
}

/**
 * Guess which AI apps may be holding the install host (human UX only).
 * @param {OccamProcess[]} procs
 * @returns {string[]}
 */
export function inferHoldingApps(procs) {
  /** @type {Set<string>} */
  const apps = new Set();
  for (const p of procs) {
    const blob = `${p.commandLine || ""}\n${p.executablePath || ""}`.toLowerCase();
    if (blob.includes("cursor")) apps.add("Cursor");
    if (blob.includes("claude")) apps.add("Claude Desktop / Claude Code");
    if (blob.includes("codex")) apps.add("Codex CLI");
    if (blob.includes("hermes")) apps.add("Hermes Agent");
    if (blob.includes("openclaw")) apps.add("OpenClaw");
    if (blob.includes("gemini")) apps.add("Gemini CLI");
  }
  return [...apps];
}

/**
 * Human-facing message when the install tree cannot be replaced safely.
 * @param {{ apps?: string[] }} [opts]
 */
export function renderInstallInUseMessage(opts = {}) {
  const apps = opts.apps?.length ? opts.apps : [];
  const lines = [
    "Occam is currently in use.",
    "",
    "Close or restart these AI apps before updating:",
  ];
  if (apps.length) {
    for (const a of apps) lines.push(`• ${a}`);
  } else {
    lines.push("• Any app that has Occam connected (Cursor, Claude Desktop, …)");
  }
  lines.push("");
  lines.push("Then run the installer again.");
  lines.push("");
  lines.push("No files were changed.");
  return lines.join("\n");
}

/**
 * Stop install-scoped Occam hosts and report whether the tree is free to replace.
 * Does not delete the install directory.
 *
 * @param {string} installDir
 * @param {{ dryRun?: boolean, force?: boolean, retries?: number, retryMs?: number }} [opts]
 */
export function prepareInstallTreeReplace(installDir, opts = {}) {
  const abs = path.resolve(installDir);
  if (!fs.existsSync(abs)) {
    return {
      ok: true,
      stopped: /** @type {OccamProcess[]} */ ([]),
      locked: false,
      apps: /** @type {string[]} */ ([]),
      message: "",
    };
  }

  const retries = opts.retries ?? 3;
  const retryMs = opts.retryMs ?? 800;
  /** @type {OccamProcess[]} */
  let stoppedAll = [];

  for (let i = 0; i < retries; i++) {
    const before = listOccamHostProcesses(abs);
    if (opts.dryRun === true) {
      const locked = isInstallHostLocked(abs) || before.length > 0;
      return {
        ok: !locked,
        stopped: before,
        locked,
        apps: inferHoldingApps(before),
        message: locked ? renderInstallInUseMessage({ apps: inferHoldingApps(before) }) : "",
        dryRun: true,
      };
    }

    if (before.length) {
      const { stopped } = stopOccamHostProcesses(abs, {
        force: opts.force !== false,
        graceMs: Math.min(retryMs, 1500),
        includeDotnet: false,
      });
      stoppedAll = [...stoppedAll, ...stopped];
    }

    if (!isInstallHostLocked(abs) && listOccamHostProcesses(abs).length === 0) {
      return {
        ok: true,
        stopped: stoppedAll,
        locked: false,
        apps: inferHoldingApps(stoppedAll),
        message: "",
      };
    }
    sleepMs(retryMs);
  }

  const remaining = listOccamHostProcesses(abs);
  const apps = inferHoldingApps(remaining.length ? remaining : stoppedAll);
  return {
    ok: false,
    stopped: stoppedAll,
    locked: true,
    apps,
    message: renderInstallInUseMessage({ apps }),
  };
}

function main() {
  const root = process.env.OCCAM_HOME?.trim() || path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
  const { stopped, stillLocked } = stopOccamHostProcesses(root, { force: true, includeDotnet: false });
  console.error(
    `stop-occam-processes: root=${root} stopped=${stopped.length} stillLocked=${stillLocked}`,
  );
  for (const proc of stopped) {
    console.error(`  stopped pid=${proc.pid} name=${proc.name}`);
  }
  process.exit(stillLocked ? 1 : 0);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
