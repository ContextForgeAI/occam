#!/usr/bin/env node
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { runControlLoop } from "./lib/operator/control-loop.mjs";
import { dispatchSubcommand } from "./lib/operator/occam-cli-dispatch.mjs";
import {
  CLI_SUBCOMMANDS,
  findSubcommand,
  formatSubcommandUsage,
} from "./lib/operator/occam-cli-subcommands.mjs";
import { runControlAction, showStatus } from "./lib/operator/control-actions.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");
process.env.OCCAM_HOME = defaultHome;

/**
 * @param {string[]} argv
 */
function parseGlobalArgs(argv) {
  const args = [...argv];
  let json = false;
  let help = false;

  while (args[0]?.startsWith("-")) {
    const flag = args.shift();
    if (flag === "--json") {
      json = true;
    } else if (flag === "-h" || flag === "--help") {
      help = true;
    } else {
      break;
    }
  }

  return { json, help, args };
}

/**
 * @param {{ json: boolean }} opts
 */
function printUsage(opts) {
  const text = formatSubcommandUsage();
  if (opts.json) {
    console.log(JSON.stringify({ usage: text, commands: CLI_SUBCOMMANDS.map((c) => c.name) }, null, 2));
    return;
  }

  console.log(text);
}

async function main() {
  const { json, help, args } = parseGlobalArgs(process.argv.slice(2));

  if (help && args.length === 0) {
    printUsage({ json });
    process.exit(0);
  }

  const subName = args[0]?.toLowerCase();
  const passthrough = args.slice(1);

  if (!subName) {
    if (process.stdin.isTTY && !json && process.env.CI !== "1" && process.env.CI !== "true") {
      process.exit(await runControlLoop(defaultHome));
    }

    printUsage({ json });
    process.exit(1);
  }

  const sub = findSubcommand(subName);
  if (!sub) {
    if (json) {
      console.log(JSON.stringify({ error: `unknown command: ${subName}` }, null, 2));
    } else {
      console.error(`error: unknown command '${subName}'`);
      printUsage({ json: false });
    }
    process.exit(1);
  }

  if (sub.delegate === "internal") {
    if (sub.internalAction === "control") {
      process.exit(await runControlLoop(defaultHome, { json }));
    }

    if (sub.internalAction === "status") {
      const status = await showStatus(defaultHome);
      if (json) {
        console.log(JSON.stringify(status.data ?? status, null, 2));
      } else {
        console.log(status.message);
        if (status.data?.update) {
          console.log(status.data.update.upgradeHint);
        }
      }
      process.exit(status.ok ? 0 : 1);
    }

    if (sub.internalAction === "update") {
      const result = await runControlAction("update", defaultHome);
      if (json) {
        console.log(JSON.stringify(result.data ?? result, null, 2));
      } else {
        console.log(result.message);
      }
      process.exit(result.ok ? 0 : 1);
    }

    console.error(`error: unhandled internal action ${sub.internalAction}`);
    process.exit(1);
  }

  const code = dispatchSubcommand(sub, defaultHome, passthrough);
  process.exit(code);
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
