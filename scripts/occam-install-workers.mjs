#!/usr/bin/env node
/**
 * Install / repair Node deps for Occam workers (HTTP extract path).
 *
 *   occam install-workers [--force] [--with-browser] [--quiet]
 *   node scripts/occam-install-workers.mjs …
 *
 * Does not publish the AOT host. Playwright Chromium is opt-in via --with-browser
 * (HTTP `occam read` works after npm alone).
 *
 * Marker: WORKERS_INSTALL_OK
 */
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ensureWorkersDeps } from "./lib/ensure-workers-deps.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const root = process.env.OCCAM_HOME?.trim() || join(here, "..");

function parseArgs(argv) {
  return {
    force: argv.includes("--force"),
    withBrowser: argv.includes("--with-browser"),
    quiet: argv.includes("--quiet"),
    help: argv.includes("--help") || argv.includes("-h"),
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.help) {
    console.log(`Usage: occam install-workers [--force] [--with-browser] [--quiet]

  Install npm workspace deps under workers/ (repair if node_modules is empty/corrupt).
  --force         Re-run npm even when markers already resolve
  --with-browser  Also run: npx playwright install chromium
  --quiet         Less npm noise
`);
    process.exit(0);
  }

  process.env.OCCAM_HOME = root;
  const result = ensureWorkersDeps(root, { force: args.force, quiet: args.quiet });
  if (!result.ok) {
    console.error(`[occam install-workers] FAIL ${result.detail}`);
    console.error("Hint: need Node 20+ and network access to the npm registry.");
    process.exit(1);
  }

  if (args.withBrowser) {
    const browserWorker = join(root, "workers", "browser-extract");
    if (!existsSync(join(browserWorker, "package.json"))) {
      console.error("[occam install-workers] FAIL missing workers/browser-extract");
      process.exit(1);
    }
    if (!args.quiet) {
      console.error("[occam install-workers] playwright install chromium ...");
    }
    const npxCli = join(dirname(process.execPath), "node_modules", "npm", "bin", "npx-cli.js");
    const pw = existsSync(npxCli)
      ? spawnSync(process.execPath, [npxCli, "playwright", "install", "chromium"], {
          cwd: browserWorker,
          env: process.env,
          stdio: args.quiet ? "ignore" : "inherit",
          shell: false,
        })
      : spawnSync("npx", ["playwright", "install", "chromium"], {
          cwd: browserWorker,
          env: process.env,
          stdio: args.quiet ? "ignore" : "inherit",
          shell: process.platform === "win32",
        });
    if ((pw.status ?? 1) !== 0) {
      console.error(`[occam install-workers] FAIL playwright install (exit ${pw.status ?? 1})`);
      process.exit(pw.status ?? 1);
    }
  }

  console.log("WORKERS_INSTALL_OK");
  process.exit(0);
}

main();
