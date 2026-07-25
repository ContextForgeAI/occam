#!/usr/bin/env node
/**
 * occam connect — detect MCP hosts/runtimes and auto-connect Tier A adapters.
 *
 *   node scripts/occam-connect.mjs [--json] [--detect-only] [--force]
 *   OCCAM_CONNECT=off|detect|auto
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

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");

function parseArgs(argv) {
  let format = "tty";
  let detectOnly = false;
  let force = false;
  let skipOccamVerify = false;
  /** @type {string[]} */
  const only = [];

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--json") format = "json";
    else if (arg === "--detect-only") detectOnly = true;
    else if (arg === "--force") force = true;
    else if (arg === "--skip-occam-verify") skipOccamVerify = true;
    else if (arg === "--only") {
      const v = argv[++i];
      if (v) only.push(...v.split(",").map((s) => s.trim()).filter(Boolean));
    } else if (arg === "-h" || arg === "--help") {
      printHelp();
      process.exit(0);
    }
  }
  return { format, detectOnly, force, skipOccamVerify, only };
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
  --skip-occam-verify    Skip Occam stdio tools/list (tests only)
  -h, --help             Show help

Env:
  OCCAM_HOME             Install root (default: repo/install dir)
  OCCAM_CONNECT          auto|off|detect — CI ignores auto/on without FORCE
  OCCAM_CONNECT_FORCE    1 — allow host mutation when CI=1 (explicit override)
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

  if (opts.format === "json") {
    console.log(JSON.stringify(report, null, 2));
  } else {
    console.log(renderConnectTranscript(report));
  }

  // Exit 0 for install/detect success paths; 1 only when mutate attempted and
  // failed hard. A host that merely needs a user action is not a failure.
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
