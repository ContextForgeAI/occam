#!/usr/bin/env node
/**
 * Lightweight public-branding check for Occam docs.
 * Fails when user-facing prose reintroduces "FF-Occam MCP" as the product brand.
 * Compatibility identifiers (ff-occam, FFOccamMcp.Core, OCCAM_*, paths) are allowed.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const errors = [];

/** Paths relative to repo root that may still mention legacy product branding. */
const ALLOWED_PATH_PREFIXES = [
  "AGENTS.md",
  "CLAUDE.md",
  "CHANGELOG.md",
  "REVIEW_GUIDE.md",
  "THIRD_PARTY_NOTICES.md",
  "corpora/",
  "docs/rc2/",
  "docs/maintenance/",
  "docs/development/",
  "packages/",
  "skills/",
  "benchmarks/",
  "src/",
  "workers/",
  "scripts/",
  ".cursor/",
  ".github/ISSUE_TEMPLATE/",
];

const SCAN_ROOTS = ["README.md", "INSTALL.md", "SECURITY.md", "VISION.md", "CONTRIBUTING.md", "llms.txt", "docs"];

function allowed(rel) {
  const norm = rel.split(sep).join("/");
  return ALLOWED_PATH_PREFIXES.some((p) => norm === p || norm.startsWith(p));
}

function walk(fileOrDir, out = []) {
  const full = resolve(repoRoot, fileOrDir);
  let st;
  try {
    st = statSync(full);
  } catch {
    return out;
  }
  if (st.isFile()) {
    out.push(full);
    return out;
  }
  for (const entry of readdirSync(full, { withFileTypes: true })) {
    if (entry.name === "rc2" || entry.name === "maintenance" || entry.name === "development") {
      // Still scanned unless path allowed — development CURRENT_STATE etc. allowed via prefix.
    }
    const child = join(full, entry.name);
    if (entry.isDirectory()) walk(relative(repoRoot, child), out);
    else if (/\.(md|txt)$/i.test(entry.name)) out.push(child);
  }
  return out;
}

function linesOutsideFences(text) {
  let fence = null;
  return text.split(/\r?\n/).map((rawLine, index) => {
    const line = index === 0 ? rawLine.replace(/^\uFEFF/, "") : rawLine;
    const marker = line.match(/^\s{0,3}(`{3,}|~{3,})/);
    if (marker) {
      const kind = marker[1][0];
      if (fence === null) fence = kind;
      else if (fence === kind) fence = null;
      return "";
    }
    return fence === null ? line : "";
  });
}

const files = [];
for (const root of SCAN_ROOTS) walk(root, files);

const brandRe = /\bFF-Occam MCP\b/;
const repoNameAsBrand = /\bFFOccamMCP\b/;

for (const file of files) {
  const rel = relative(repoRoot, file).split(sep).join("/");
  if (allowed(rel)) continue;
  // Generated mirrors of root files — root is already scanned.
  if (
    rel === "docs/install.md" ||
    rel === "docs/llms.txt" ||
    rel === "docs/trust/security-policy.md" ||
    rel === "docs/developers/contributing.md" ||
    rel === "docs/developers/vision.md" ||
    rel === "docs/reference/mcp-api.md"
  ) {
    continue;
  }
  const visible = linesOutsideFences(readFileSync(file, "utf8")).join("\n");
  if (brandRe.test(visible)) {
    errors.push(`${rel}: public prose uses legacy brand "FF-Occam MCP" (use Occam / Occam MCP)`);
  }
  // Repo folder name as product brand in user docs
  if (repoNameAsBrand.test(visible) && !rel.startsWith("docs/architecture/")) {
    // Allow path examples that mention the local checkout folder only in code fences (already stripped).
    errors.push(`${rel}: public prose uses "FFOccamMCP" as product brand (keep only as path/compat)`);
  }
}

if (errors.length) {
  console.error(`docs-brand: FAILED (${errors.length})`);
  for (const e of errors) console.error(`  - ${e}`);
  process.exit(1);
}

console.log(`docs-brand: OK — scanned ${files.length} files`);
