#!/usr/bin/env node
import { runDataCommand } from "./lib/operator/occam-data-cli.mjs";

process.exitCode = await runDataCommand("search", process.argv.slice(2));
