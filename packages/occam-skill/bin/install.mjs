#!/usr/bin/env node
import { installOccamSkill, SKILL_PLATFORMS } from "../lib/install.mjs";

function printHelp() {
  console.log(`@ff-occam/skill install — portable FF-Occam skill for any agent harness

Usage:
  npx @ff-occam/skill install [options]

Options:
  --platform <name>   ${SKILL_PLATFORMS.join(" | ")} (default: all)
  --global            User-level skills dirs (default)
  --project           Current repo project skills dirs
  --target <dir>      Custom destination
  --dry-run           Print plan only
  --json
  -h, --help

Wire MCP separately: npx @ff-occam/mcp — see installed skill references/install.md`);
}

function parseArgs(argv) {
  const out = {
    platform: "all",
    scope: "global",
    dryRun: false,
    json: false,
    help: false,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "-h" || arg === "--help") out.help = true;
    else if (arg === "--json") out.json = true;
    else if (arg === "--dry-run") out.dryRun = true;
    else if (arg === "--global") out.scope = "global";
    else if (arg === "--project") out.scope = "project";
    else if (arg === "--platform") out.platform = argv[++i] ?? "";
    else if (arg === "--target") out.target = argv[++i] ?? "";
    else throw new Error(`unknown flag: ${arg}`);
  }

  return out;
}

let argv = process.argv.slice(2);
if (argv[0] === "install") argv = argv.slice(1);

const args = parseArgs(argv);

if (args.help) {
  printHelp();
  process.exit(0);
}

if (!SKILL_PLATFORMS.includes(args.platform)) {
  const msg = `unknown platform: ${args.platform}`;
  if (args.json) console.log(JSON.stringify({ ok: false, error: msg }, null, 2));
  else console.error(`error: ${msg}`);
  process.exit(1);
}

const result = installOccamSkill({
  occamHome: process.env.OCCAM_HOME,
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
for (const row of result.installed) {
  console.log(`  [${row.platform}] ${row.action} → ${row.dest}`);
}
if (result.agents) {
  console.log(`  [codex] ${result.agents.action} → ${result.agents.agentsPath}`);
}
