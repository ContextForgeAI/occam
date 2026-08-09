#!/usr/bin/env node
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { runRemovalCli } from "./lib/operator/uninstall.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const occamHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");

runRemovalCli("uninstall", process.argv.slice(2), { occamHome })
  .then((code) => {
    process.exitCode = code;
  })
  .catch((err) => {
    console.error(err instanceof Error ? err.message : String(err));
    process.exitCode = 1;
  });
