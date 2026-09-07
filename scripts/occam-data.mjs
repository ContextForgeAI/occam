#!/usr/bin/env node
import { basename } from "node:path";
import { runDataCommand } from "./lib/operator/occam-data-cli.mjs";

const name = basename(process.argv[1] || "", ".mjs").replace(/^occam-/, "");
const command = ["read", "search", "digest"].includes(name)
  ? name
  : process.env.OCCAM_DATA_COMMAND?.trim() || process.argv[2];
const argv = ["read", "search", "digest"].includes(name)
  ? process.argv.slice(2)
  : process.argv.slice(3);

if (!["read", "search", "digest"].includes(command ?? "")) {
  console.error("usage: occam read|search|digest …");
  process.exit(2);
}

process.exitCode = await runDataCommand(command, argv);
