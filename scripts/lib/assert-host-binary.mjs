#!/usr/bin/env node
/**
 * Doctor gate: OccamMcp.Core must exist after doctor (especially with --skip-build).
 * Usage: node assert-host-binary.mjs <OCCAM_HOME> [--skip-build]
 */
import { assertHostBinaryPresent } from "./host-install-gate.mjs";
import { resolve } from "node:path";

const root = resolve(process.argv[2] ?? process.env.OCCAM_HOME ?? "..");
const skipBuild = process.argv.includes("--skip-build");
assertHostBinaryPresent(root, { skipBuild });
