#!/usr/bin/env node
/**
 * occam connect — detect MCP hosts/runtimes and connect with human-first UX.
 *
 *   node scripts/occam-connect.mjs [--json] [--detect-only] [--force] [--verbose]
 *   OCCAM_CONNECT=off|detect|auto
 *   OCCAM_CONNECT_ALL=1 — non-interactive connect-all
 */
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { homedir } from "node:os";
import { fileURLToPath } from "node:url";
import {
  createHostAdapters,
  runConnect,
  renderConnectTranscript,
  resolveConnectMode,
} from "./lib/operator/connect/index.mjs";
import { runConnectOnboarding, allowConnectAll } from "./lib/operator/connect-onboarding.mjs";
import { isInstallVerbose } from "./lib/operator/install-ux.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");

function parseArgs(argv) {
  let format = "tty";
  let detectOnly = false;
  let force = false;
  let skipOccamVerify = false;
  let verbose = false;
  /** @type {string[]} */
  const only = [];

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--json") format = "json";
    else if (arg === "--detect-only") detectOnly = true;
    else if (arg === "--force") force = true;
    else if (arg === "--verbose" || arg === "--debug") verbose = true;
    else if (arg === "--skip-occam-verify") skipOccamVerify = true;
    else if (arg === "--only") {
      const v = argv[++i];
      if (v) only.push(...v.split(",").map((s) => s.trim()).filter(Boolean));
    } else if (arg === "-h" || arg === "--help") {
      printHelp();
      process.exit(0);
    }
  }
  return { format, detectOnly, force, skipOccamVerify, only, verbose };
}

/**
 * Host lists come from the registry so help can never disagree with behaviour.
 */
function hostSummary() {
  const adapters = Object.values(createHostAdapters({ occamHome: defaultHome }));
  const named = (list) => list.map((a) => a.name).join(", ");
  const assisted = adapters.filter((a) => a.connectionMethod === "ASSISTED");
  const configurable = adapters.filter((a) => a.connectionMethod !== "ASSISTED");
  const lines = [
    `Auto-connect (validated): ${named(configurable.filter((a) => a.supportTier === "A"))}.`,
    `Opt-in with --only (implemented, not yet validated end-to-end): ${named(
      configurable.filter((a) => a.supportTier !== "A"),
    )}.`,
  ];
  if (assisted.length) lines.push(`Manual setup only: ${named(assisted)}.`);
  return lines.join("\n");
}

function printHelp() {
  console.log(`usage: node scripts/occam-connect.mjs [options]

${hostSummary()}

Options:
  --json                 Machine-readable report
  --detect-only          Detect hosts/runtimes; do not mutate configs
  --force                Re-apply even when registration already matches / overwrite unmanaged
  --only IDS             Comma list, e.g. hermes,cursor,claude-desktop,vscode,zed
  --verbose              Engineering detail (confidence, levels, launcher paths)
  --skip-occam-verify    Skip Occam stdio tools/list (tests only)
  -h, --help             Show help

Env:
  OCCAM_HOME             Install root (default: repo/install dir)
  OCCAM_CONNECT          auto|off|detect — CI ignores auto/on without FORCE
  OCCAM_CONNECT_FORCE    1 — allow host mutation when CI=1 (explicit override)
  OCCAM_CONNECT_ALL      1 — non-interactive connect-all for multiple hosts
`);
}

/**
 * Persist last connect report for bootstrap (avoid dual manual-snippet messaging).
 * Written under ~/.occam — not the install/repo tree.
 * @param {object} report
 */
function writeConnectLast(report) {
  try {
    const dir = join(homedir(), ".occam");
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, "connect-last.json"), JSON.stringify(report, null, 2), "utf8");
  } catch {
    // best-effort only
  }
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const verbose = opts.verbose || isInstallVerbose(process.env, process.argv);
  const interactive = process.stdin.isTTY === true && process.stdout.isTTY === true;

  // JSON / explicit --only / detect-only-with-json: keep raw engine path.
  if (opts.format === "json") {
    /** @type {ReturnType<typeof resolveConnectMode>} */
    let connectMode = resolveConnectMode(process.env);
    if (opts.detectOnly) {
      connectMode = { mode: "detect-only", mutateHosts: false, reason: "--detect-only" };
    }
    const report = await runConnect({
      occamHome: defaultHome,
      connectMode,
      force: opts.force,
      only: opts.only.length ? opts.only : undefined,
      skipOccamVerify: opts.skipOccamVerify,
    });
    writeConnectLast(report);
    console.log(JSON.stringify(report, null, 2));
    const hardFail = report.connections.some(
      (c) => c.apply && c.apply.ok === false && c.readyState?.requiresUserAction !== true,
    );
    const occamFail = report.occamVerify && report.occamVerify.ok === false && !report.occamVerify.skipped;
    const rollbackFail = report.connections.some((c) => c.rollback && c.rollback.ok === false);
    process.exit(hardFail || occamFail || rollbackFail ? 1 : 0);
  }

  // Human path — shared with installer first-run.
  /** @type {string[]} */
  const emitted = [];
  const result = await runConnectOnboarding({
    occamHome: defaultHome,
    setupMode: "auto",
    verbose,
    interactive,
    forceConnect: opts.force,
    skipOccamVerify: opts.skipOccamVerify,
    connectAll: allowConnectAll(process.env),
    detectOnly: opts.detectOnly,
    only: opts.only.length ? opts.only : undefined,
    source: "connect",
    emit: (line) => {
      emitted.push(line);
      console.log(line);
    },
  });

  if (result.connectReport) {
    writeConnectLast(result.connectReport);
  }

  // Final summary (discovery/progress already emitted).
  if (result.transcript) {
    // Avoid duplicating discovery lines already printed via emit.
    const summary = result.transcript;
    console.log("");
    console.log(summary);
  }

  const report = result.connectReport;
  if (!report) {
    process.exit(0);
  }
  const hardFail = report.connections.some(
    (c) => c.apply && c.apply.ok === false && c.readyState?.requiresUserAction !== true,
  );
  const occamFail = report.occamVerify && report.occamVerify.ok === false && !report.occamVerify.skipped;
  const rollbackFail = report.connections.some((c) => c.rollback && c.rollback.ok === false);
  process.exit(hardFail || occamFail || rollbackFail ? 1 : 0);
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : String(err));
  process.exit(1);
});
