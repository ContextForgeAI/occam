#!/usr/bin/env node
/**
 * prepare-commit-msg hook helper: strip forbidden AI attribution trailers.
 * Usage: node scripts/git-hooks/strip-ai-attribution-trailers.mjs <path-to-commit-msg>
 */
import { readFileSync, writeFileSync } from "node:fs";
import { findForbiddenTrailers } from "../check-git-attribution.mjs";

const file = process.argv[2];
if (!file) {
  console.error("usage: strip-ai-attribution-trailers.mjs <commit-msg-file>");
  process.exit(2);
}

const original = readFileSync(file, "utf8");
const lines = original.split(/\r?\n/);
const kept = lines.filter((line) => findForbiddenTrailers(line).length === 0);
const next = kept.join("\n");
if (next !== original) {
  writeFileSync(file, next);
}
