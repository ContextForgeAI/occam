#!/usr/bin/env node
import { runBriefCommand } from "./lib/operator/occam-brief-cli.mjs";

process.exitCode = await runBriefCommand(process.argv.slice(2));
