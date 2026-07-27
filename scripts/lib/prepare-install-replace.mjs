#!/usr/bin/env node
/**
 * Prepare an existing Occam install tree for replacement (stop install-scoped hosts).
 *
 *   node prepare-install-replace.mjs --dir <OCCAM_INSTALL_DIR> [--json] [--dry-run]
 *
 * Exit 0 when the tree is free to replace; exit 2 when still in use (human message on stderr).
 * Does not delete the install directory.
 */
import { realpathSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { prepareInstallTreeReplace } from "./stop-occam-processes.mjs";

function parseArgs(argv) {
  let dir = "";
  let json = false;
  let dryRun = false;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--dir") dir = argv[++i] || "";
    else if (a === "--json") json = true;
    else if (a === "--dry-run") dryRun = true;
    else if (a === "-h" || a === "--help") {
      console.log("usage: node prepare-install-replace.mjs --dir <INSTALL_DIR> [--json] [--dry-run]");
      process.exit(0);
    }
  }
  if (!dir) {
    console.error("error: --dir is required");
    process.exit(2);
  }
  return { dir: resolve(dir), json, dryRun };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const result = prepareInstallTreeReplace(args.dir, { dryRun: args.dryRun });
  if (args.json) {
    console.log(JSON.stringify(result, null, 2));
  } else if (!result.ok && result.message) {
    console.error(result.message);
  }
  process.exit(result.ok ? 0 : 2);
}

function isDirectCliInvocation(argv1 = process.argv[1], metaUrl = import.meta.url) {
  if (!argv1) return false;
  try {
    return realpathSync(argv1) === realpathSync(fileURLToPath(metaUrl));
  } catch {
    return resolve(argv1) === fileURLToPath(metaUrl);
  }
}

if (isDirectCliInvocation()) {
  main();
}
