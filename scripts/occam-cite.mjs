#!/usr/bin/env node
import { runCiteCommand } from "./lib/operator/occam-cite-cli.mjs";

process.exitCode = await runCiteCommand(process.argv.slice(2));
