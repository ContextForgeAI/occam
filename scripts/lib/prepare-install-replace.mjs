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
import { inspectInstallTarget } from "./install-target-inspect.mjs";

function parseArgs(argv) {
  let dir = "";
  let rid = "";
  let json = false;
  let dryRun = false;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--dir") dir = argv[++i] || "";
    else if (a === "--rid") rid = argv[++i] || "";
    else if (a === "--json") json = true;
    else if (a === "--dry-run") dryRun = true;
    else if (a === "-h" || a === "--help") {
      console.log(
        "usage: node prepare-install-replace.mjs --dir <INSTALL_DIR> [--rid RID] [--json] [--dry-run]",
      );
      process.exit(0);
    }
  }
  if (!dir) {
    console.error("error: --dir is required");
    process.exit(2);
  }
  return { dir: resolve(dir), rid, json, dryRun };
}

/**
 * Prove an existing target is an owned Level B release before stopping processes.
 * @param {string} dir
 * @param {{ rid?: string, dryRun?: boolean, prepare?: typeof prepareInstallTreeReplace }} [opts]
 */
export function prepareOwnedInstallTreeReplace(dir, opts = {}) {
  const inspection = inspectInstallTarget(dir, { rid: opts.rid });
  if (inspection.action === "absent") {
    return { ok: true, stopped: [], locked: false, ownership: inspection };
  }
  if (inspection.action !== "remove") {
    return {
      ok: false,
      stopped: [],
      locked: false,
      failureKind: "unowned_install_target",
      ownership: inspection,
      message: `Refusing to replace OCCAM_INSTALL_DIR.\n\n${inspection.reason}\n\nNo files were changed.`,
    };
  }
  const prepare = opts.prepare ?? prepareInstallTreeReplace;
  const result = prepare(inspection.path, { dryRun: opts.dryRun === true });
  return { ...result, ownership: inspection };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const result = prepareOwnedInstallTreeReplace(args.dir, {
    rid: args.rid || undefined,
    dryRun: args.dryRun,
  });
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
