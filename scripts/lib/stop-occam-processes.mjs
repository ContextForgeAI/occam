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
 * @param {string} root
 * @returns {boolean}
 */
export function isPublishExeLocked(root) {
  const exe = publishExePath(root);
  if (!fs.existsSync(exe)) {
    return false;
  }
  try {
    const fd = fs.openSync(exe, "r+");
    fs.closeSync(fd);
    return false;
  } catch (err) {
    const code = /** @type {NodeJS.ErrnoException} */ (err).code;
    return code === "EBUSY" || code === "EPERM" || code === "EACCES";
  }
}

/**
 * @typedef {{ pid: number, name: string, commandLine: string }} OccamProcess
 */

/**
 * @param {string} root
 * @param {{ includeDotnet?: boolean }} [opts]
 * @returns {OccamProcess[]}
 */
export function listOccamHostProcesses(root, opts = {}) {
  const includeDotnet = opts.includeDotnet === true;
  const normalizedRoot = path.resolve(root).replace(/\\/g, "\\\\");

  if (process.platform === "win32") {
    const hostNameClause = HOST_BASE_NAMES.map(
      (base) => `$_.Name -eq '${withExe(base).replace(/'/g, "''")}'`,
    ).join(" -or ");
    const hostCmdClause = HOST_BASE_NAMES.map(
      (base) => `($_.CommandLine -match '${base.replace(/\./g, "\\.")}')`,
    ).join(" -or ");
    const dotnetClause = includeDotnet ? ` -or (${hostCmdClause})` : "";
    const ps = `
$root = '${normalizedRoot.replace(/'/g, "''")}'
Get-CimInstance Win32_Process | Where-Object {
  (${hostNameClause}) -or
  ($_.CommandLine -and $_.CommandLine -match 'launch-mcp-host\\.mjs' -and $_.CommandLine -match [regex]::Escape($root))${dotnetClause}
} | ForEach-Object {
  [PSCustomObject]@{ pid = $_.ProcessId; name = $_.Name; commandLine = $_.CommandLine }
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
        }));
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
    const rootNorm = path.resolve(root);
    const seen = new Set();
    /** @type {OccamProcess[]} */
    const found = [];
    for (const line of out.split("\n")) {
      const trimmed = line.trim();
      if (!trimmed || !new RegExp(grep).test(trimmed)) {
        continue;
      }
      const mentionsHost = HOST_BASE_NAMES.some((base) => trimmed.includes(base));
      if (!trimmed.includes(rootNorm) && !mentionsHost) {
        continue;
      }
      const match = trimmed.match(/^(\d+)\s+(\S+)\s+(.*)$/);
      if (!match) {
        continue;
      }
      const pid = Number(match[1]);
      if (!pid || seen.has(pid)) {
        continue;
      }
      seen.add(pid);
      found.push({ pid, name: match[2], commandLine: match[3] });
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

  return { stopped, stillLocked: isPublishExeLocked(root) };
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
