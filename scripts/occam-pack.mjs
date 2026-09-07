#!/usr/bin/env node
import { runPackCommand } from "./lib/operator/occam-pack-cli.mjs";

process.exitCode = await runPackCommand(process.argv.slice(2));
