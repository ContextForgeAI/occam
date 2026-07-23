#!/usr/bin/env node
/**
 * Sync skills/occam → packages/occam-skill/skill for npm publish.
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const src = path.join(root, "skills", "occam");
const dest = path.join(root, "packages", "occam-skill", "skill");

function copyTree(from, to) {
  fs.mkdirSync(to, { recursive: true });
  for (const entry of fs.readdirSync(from, { withFileTypes: true })) {
    const s = path.join(from, entry.name);
    const d = path.join(to, entry.name);
    if (entry.isDirectory()) copyTree(s, d);
    else if (entry.isFile()) fs.copyFileSync(s, d);
  }
}

if (!fs.existsSync(path.join(src, "SKILL.md"))) {
  console.error("error: skills/occam/SKILL.md missing");
  process.exit(1);
}

if (fs.existsSync(dest)) fs.rmSync(dest, { recursive: true, force: true });
copyTree(src, dest);
console.log(`synced ${src} → ${dest}`);
