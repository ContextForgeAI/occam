#!/usr/bin/env node
import { runResearchCommand } from "./lib/operator/occam-research-cli.mjs";

process.exitCode = await runResearchCommand(process.argv.slice(2));
