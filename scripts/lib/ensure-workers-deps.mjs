/**
 * Ensure `workers/` npm workspace deps are installed and resolvable.
 *
 * Doctor historically skipped install when `workers/node_modules` merely *existed*
 * (even empty/corrupt) → `ERR_MODULE_NOT_FOUND` on `@mozilla/readability` at first read.
 *
 * Prefer `npm ci` when `package-lock.json` is present; otherwise `npm install`.
 *
 *   node scripts/lib/ensure-workers-deps.mjs [--check] [--force] [--quiet]
 *   import { workersDepsHealthy, ensureWorkersDeps } from "./ensure-workers-deps.mjs"
 *
 * Exit 0 when healthy (or after a successful install). Exit 1 on failure.
 * Marker (stdout, install path only): WORKERS_DEPS_OK
 */
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const defaultRoot = join(here, "..", "..");

/** Packages that must resolve for HTTP extract (the common crash surface). */
export const HTTP_WORKER_MARKERS = [
  "@mozilla/readability",
  "jsdom",
  "turndown",
  "undici",
];

/**
 * Run npm without shell:true (Windows: npm.cmd + shell:false → EINVAL / DEP0190).
 * Prefer `node …/npm-cli.js`; fall back to PATH `npm` with shell only on win32.
 * @param {string[]} args
 * @param {{ cwd: string, env?: NodeJS.ProcessEnv, stdio?: import("node:child_process").StdioOptions }} opts
 */
export function runNpm(args, opts) {
  const npmCli = join(dirname(process.execPath), "node_modules", "npm", "bin", "npm-cli.js");
  if (existsSync(npmCli)) {
    return spawnSync(process.execPath, [npmCli, ...args], {
      cwd: opts.cwd,
      env: opts.env ?? process.env,
      stdio: opts.stdio ?? "inherit",
      shell: false,
    });
  }
  return spawnSync("npm", args, {
    cwd: opts.cwd,
    env: opts.env ?? process.env,
    stdio: opts.stdio ?? "inherit",
    shell: process.platform === "win32",
  });
}

/**
 * @param {string} workersRoot
 * @param {string[]} markers
 */
export function workersDepsHealthy(workersRoot, markers = HTTP_WORKER_MARKERS) {
  const httpPkg = join(workersRoot, "http-extract", "package.json");
  if (!existsSync(httpPkg)) {
    return false;
  }
  if (!existsSync(join(workersRoot, "node_modules"))
    && !existsSync(join(workersRoot, "http-extract", "node_modules"))) {
    return false;
  }
  try {
    const require = createRequire(httpPkg);
    for (const name of markers) {
      require.resolve(name);
    }
    return true;
  } catch {
    return false;
  }
}

/**
 * @param {string} occamHome
 * @param {{ force?: boolean, quiet?: boolean, npmArgs?: string[] }} [opts]
 * @returns {{ ok: boolean, skipped: boolean, usedCi: boolean, detail: string }}
 */
export function ensureWorkersDeps(occamHome, opts = {}) {
  const workersRoot = join(occamHome, "workers");
  const pkg = join(workersRoot, "package.json");
  if (!existsSync(pkg)) {
    return { ok: false, skipped: false, usedCi: false, detail: "missing workers/package.json" };
  }

  const force = opts.force === true
    || process.env.OCCAM_WORKERS_FORCE_INSTALL === "1"
    || process.env.OCCAM_WORKERS_FORCE_INSTALL === "true";
  const quiet = opts.quiet === true;

  if (!force && workersDepsHealthy(workersRoot)) {
    return { ok: true, skipped: true, usedCi: false, detail: "already healthy" };
  }

  const lock = join(workersRoot, "package-lock.json");
  const usedCi = existsSync(lock);
  const npmCmd = usedCi ? "ci" : "install";
  const args = [npmCmd, "--no-fund", "--no-audit", ...(opts.npmArgs ?? [])];
  if (quiet) {
    args.push("--silent");
  }

  if (!quiet) {
    console.error(`[occam.workers] npm ${npmCmd} (workspace root) ...`);
  }

  // Prefer node + npm-cli.js (Windows: avoids npm.cmd EINVAL / DEP0190).
  const result = runNpm(args, {
    cwd: workersRoot,
    env: process.env,
    stdio: quiet ? "ignore" : "inherit",
  });

  if ((result.status ?? 1) !== 0) {
    return {
      ok: false,
      skipped: false,
      usedCi,
      detail: `npm ${npmCmd} failed (exit ${result.status ?? 1})`,
    };
  }

  if (!workersDepsHealthy(workersRoot)) {
    return {
      ok: false,
      skipped: false,
      usedCi,
      detail: "npm finished but HTTP worker markers still do not resolve",
    };
  }

  return { ok: true, skipped: false, usedCi, detail: `npm ${npmCmd} ok` };
}

function parseArgs(argv) {
  return {
    check: argv.includes("--check"),
    force: argv.includes("--force"),
    quiet: argv.includes("--quiet"),
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const root = process.env.OCCAM_HOME?.trim() || defaultRoot;
  const workersRoot = join(root, "workers");

  if (args.check) {
    const ok = workersDepsHealthy(workersRoot);
    if (!ok) {
      console.error("[occam.workers] deps unhealthy (missing or unresolvable HTTP markers)");
      process.exit(1);
    }
    console.log("WORKERS_DEPS_OK");
    process.exit(0);
  }

  const result = ensureWorkersDeps(root, { force: args.force, quiet: args.quiet });
  if (!result.ok) {
    console.error(`[occam.workers] FAIL ${result.detail}`);
    process.exit(1);
  }

  console.log("WORKERS_DEPS_OK");
  if (!args.quiet && result.skipped) {
    console.error("[occam.workers] skipped install (markers resolve)");
  }
  process.exit(0);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main();
}
