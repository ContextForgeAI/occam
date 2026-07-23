#!/usr/bin/env node
/**
 * occam skill install — copy portable FF-Occam skill to harness directories.
 *
 * Usage:
 *   node scripts/occam-skill-install.mjs [--platform all|cursor|claude|…]
 *   occam skill install [flags]
 */
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  SKILL_PLATFORMS,
  installOccamSkill,
} from "./lib/operator/install-occam-skill.mjs";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || path.join(scriptDir, "..");

function printHelp() {
  console.log(`occam skill install — portable FF-Occam skill for any agent harness

Usage:
  occam skill install [options]

Options:
  --platform <name>   ${SKILL_PLATFORMS.join(" | ")} (default: all)
  --global            Install to user-level skills dirs (default)
  --project           Install to current repo project skills dirs
  --target <dir>      Copy skill tree to a custom directory
  --dry-run           Print destinations without writing
  --json              Machine-readable output
  -h, --help          Show this help

Examples:
  occam skill install --platform cursor --project
  occam skill install --platform hermes
  occam skill install --target ~/my-agent/skills/occam
  npx @ff-occam/skill install --platform all

Skill source: $OCCAM_HOME/skills/occam or @ff-occam/skill package.
Wire MCP separately — see skills/occam/references/install.md`);
}

/**
 * @param {string[]} argv
 */
function parseArgs(argv) {
  /** @type {{
   *   platform: string,
   *   scope: 'global'|'project',
   *   target?: string,
   *   dryRun: boolean,
   *   json: boolean,
   *   help: boolean,
   * }} */
  const out = {
    platform: "all",
    scope: "global",
    dryRun: false,
    json: false,
    help: false,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "-h" || arg === "--help") {
      out.help = true;
    } else if (arg === "--json") {
      out.json = true;
    } else if (arg === "--dry-run") {
      out.dryRun = true;
    } else if (arg === "--global") {
      out.scope = "global";
    } else if (arg === "--project") {
      out.scope = "project";
    } else if (arg === "--platform") {
      out.platform = argv[++i] ?? "";
    } else if (arg === "--target") {
      out.target = argv[++i] ?? "";
    } else if (!arg.startsWith("-")) {
      // occam skill install → subcommand already stripped; ignore bare words
    } else {
      throw new Error(`unknown flag: ${arg}`);
    }
  }

  return out;
}

function main() {
  let argv = process.argv.slice(2);
  if (argv[0] === "install") {
    argv = argv.slice(1);
  }
  const args = parseArgs(argv);

  if (args.help) {
    printHelp();
    process.exit(0);
  }

  if (!SKILL_PLATFORMS.includes(args.platform)) {
    const msg = `unknown platform: ${args.platform}`;
    if (args.json) {
      console.log(JSON.stringify({ ok: false, error: msg }, null, 2));
    } else {
      console.error(`error: ${msg}`);
      printHelp();
    }
    process.exit(1);
  }

  const result = installOccamSkill({
    occamHome: defaultHome,
    platform: args.platform,
    scope: args.scope,
    target: args.target,
    dryRun: args.dryRun,
  });

  if (args.json) {
    console.log(JSON.stringify(result, null, 2));
    process.exit(result.ok ? 0 : 1);
  }

  if (!result.ok) {
    console.error(`error: ${result.error}`);
    process.exit(1);
  }

  const verb = args.dryRun ? "Would install" : "Installed";
  console.log(`${verb} FF-Occam skill v${result.version}`);
  console.log(`Source: ${result.source}`);
  for (const row of result.installed) {
    console.log(`  [${row.platform}] ${row.action} → ${row.dest}`);
  }
  if (result.agents) {
    console.log(`  [codex] ${result.agents.action} → ${result.agents.agentsPath}`);
  }
  console.log("\nNext: wire MCP (occam doctor, occam smoke) — see references/install.md in the skill dir.");
}

main();
