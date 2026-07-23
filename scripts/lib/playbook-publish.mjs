#!/usr/bin/env node
/**
 * Maintainer CLI — export sanitized community playbook bundle (PB4c).
 * Zero MCP surface; requires --ack-community-review.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { publishPlaybook } from "../../workers/shared/lib/playbook-publish-sanitize.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));

function printHelp() {
  process.stdout.write(`Usage: occam-playbook-publish --input <path> --ack-community-review [--output <dir>] [--summary <text>]

Export a sanitized community playbook bundle for manual PR (no auto-upload).

Required:
  --input <path>              Local tier playbook JSON (e.g. ~/.occam/playbooks/local/{id}.playbook.json)
  --ack-community-review      Confirm maintainer review before export

Optional:
  --output <dir>              Export directory (default: $OCCAM_HOME/artifacts/playbook-publish/{id}/)
  --summary <text>            Maintainer note included in PULL_REQUEST.md

Environment:
  OCCAM_HOME                  Repo root (default: parent of scripts/)

Exit codes:
  0  export ok
  1  validation / secrets_detected / ack_required
  2  usage error
`);
}

function parseArgs(argv) {
  const positional = [];
  let input = null;
  let output = null;
  let summary = null;
  let ack = false;
  let help = false;

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--help" || arg === "-h") {
      help = true;
      continue;
    }
    if (arg === "--ack-community-review") {
      ack = true;
      continue;
    }
    if (arg === "--input") {
      input = argv[++i] ?? null;
      continue;
    }
    if (arg.startsWith("--input=")) {
      input = arg.slice("--input=".length);
      continue;
    }
    if (arg === "--output") {
      output = argv[++i] ?? null;
      continue;
    }
    if (arg.startsWith("--output=")) {
      output = arg.slice("--output=".length);
      continue;
    }
    if (arg === "--summary") {
      summary = argv[++i] ?? null;
      continue;
    }
    if (arg.startsWith("--summary=")) {
      summary = arg.slice("--summary=".length);
      continue;
    }
    if (arg.startsWith("-")) {
      throw new Error(`Unknown option: ${arg}`);
    }
    positional.push(arg);
  }

  if (!input && positional.length > 0) {
    input = positional[0];
  }

  return { input, output, summary, ack, help };
}

function resolveOccamHome() {
  if (process.env.OCCAM_HOME?.trim()) {
    return process.env.OCCAM_HOME.trim();
  }
  return join(scriptDir, "..", "..");
}

function main() {
  let parsed;
  try {
    parsed = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`${err instanceof Error ? err.message : String(err)}\n`);
    printHelp();
    process.exit(2);
  }

  if (parsed.help) {
    printHelp();
    process.exit(0);
  }

  if (!parsed.input) {
    process.stderr.write("error: --input is required\n");
    printHelp();
    process.exit(2);
  }

  const occamHome = resolveOccamHome();
  process.env.OCCAM_HOME ??= occamHome;

  let inputPath = parsed.input;
  if (!inputPath.startsWith("/") && !/^[A-Za-z]:[\\/]/.test(inputPath)) {
    inputPath = join(process.cwd(), inputPath);
  }

  try {
    readFileSync(inputPath);
  } catch {
    process.stderr.write(`error: input not found: ${inputPath}\n`);
    process.exit(2);
  }

  const result = publishPlaybook({
    inputPath,
    outputDir: parsed.output,
    ackCommunityReview: parsed.ack,
    summary: parsed.summary,
    occamHome,
  });

  if (!result.ok) {
    process.stderr.write(`${result.failureCode}: ${result.message}\n`);
    process.exit(1);
  }

  process.stdout.write(
    `${JSON.stringify(
      {
        ok: true,
        playbookId: result.playbookId,
        exportPath: result.exportPath,
        pullRequestTemplatePath: result.pullRequestTemplatePath,
        manifestRowPath: result.manifestRowPath,
        communityTargetPath: result.communityTargetPath,
        manifestRow: result.manifestRow,
        nextSteps: result.nextSteps,
      },
      null,
      2,
    )}\n`,
  );
}

main();
