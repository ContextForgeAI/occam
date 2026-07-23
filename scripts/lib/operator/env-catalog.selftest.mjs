import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, extname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const envDocPath = join(repoRoot, "docs", "configuration.md");

const SCAN_DIRS = [
  join(repoRoot, "src"),
  join(repoRoot, "workers"),
  join(repoRoot, "scripts"),
  join(repoRoot, "packages"),
];
const SCAN_EXT = new Set([".cs", ".mjs", ".js", ".sh", ".ps1"]);
const SKIP_DIR_NAMES = new Set(["node_modules", "obj", ".git", "dist", "coverage"]);
const DEAD_VAR_ALLOWLIST = new Set(["PLAYWRIGHT_BROWSERS_PATH"]);

function walkFiles(root, out) {
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const full = join(root, entry.name);
    if (entry.isDirectory()) {
      if (SKIP_DIR_NAMES.has(entry.name)) continue;
      walkFiles(full, out);
      continue;
    }
    if (SCAN_EXT.has(extname(full).toLowerCase())) {
      out.push(full);
    }
  }
}

function collectUsedVars(files) {
  const used = new Set();
  const patterns = [
    /process\.env(?:\.([A-Z][A-Z0-9_]+)|\[['"]([A-Z][A-Z0-9_]+)['"]\])/g,
    /Environment\.GetEnvironmentVariable\(\s*"([A-Z][A-Z0-9_]+)"\s*\)/g,
    /\$env:([A-Z][A-Z0-9_]+)/g,
    /\$\{([A-Z][A-Z0-9_]+)\}/g,
    /\$([A-Z][A-Z0-9_]+)/g,
    /\b(OCCAM_[A-Z0-9_]+|WT_[A-Z0-9_]+|PLAYWRIGHT_BROWSERS_PATH)\b/g,
  ];

  for (const file of files) {
    const text = readFileSync(file, "utf8");
    for (const regex of patterns) {
      let match;
      while ((match = regex.exec(text)) !== null) {
        const candidate = match[1] || match[2];
        if (candidate?.startsWith("OCCAM_") || candidate?.startsWith("WT_") || candidate === "PLAYWRIGHT_BROWSERS_PATH") {
          used.add(candidate);
        }
      }
    }
  }

  return used;
}

function collectDocumentedVars(markdown) {
  const vars = new Set();
  const regex = /`([A-Z][A-Z0-9_]+)`/g;
  let match;
  while ((match = regex.exec(markdown)) !== null) {
    const candidate = match[1];
    if (candidate.startsWith("OCCAM_") || candidate.startsWith("WT_") || candidate === "PLAYWRIGHT_BROWSERS_PATH") {
      vars.add(candidate);
    }
  }
  return vars;
}

function main() {
  const envDoc = readFileSync(envDocPath, "utf8");
  const documented = collectDocumentedVars(envDoc);
  assert.ok(!documented.has("OCCAM_LEGACY_ROOT"), "OCCAM_LEGACY_ROOT must not be documented.");

  const files = [];
  for (const dir of SCAN_DIRS) {
    if (statSync(dir).isDirectory()) {
      walkFiles(dir, files);
    }
  }

  const used = collectUsedVars(files);
  const deadInDocs = [...documented]
    .filter((v) => !used.has(v) && !DEAD_VAR_ALLOWLIST.has(v))
    .sort();

  assert.deepEqual(
    deadInDocs,
    [],
    `Documented vars not used in code: ${deadInDocs.join(", ")}`,
  );

  console.log("env-catalog.selftest: OK");
}

main();
