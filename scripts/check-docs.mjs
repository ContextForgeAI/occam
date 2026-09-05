#!/usr/bin/env node
/**
 * Public documentation lint: links, anchors, tool registry, discoverability, honesty.
 *
 * Run: node scripts/check-docs.mjs
 * Sub-checks (also runnable standalone):
 *   node scripts/check-docs-discoverability.mjs
 *   node scripts/check-docs-honesty.mjs
 *   node scripts/check-docs-brand.mjs
 */
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { basename, dirname, extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { checkDiscoverability } from "./check-docs-discoverability.mjs";
import { checkHonesty } from "./check-docs-honesty.mjs";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const docsRoot = join(repoRoot, "docs");
const errors = [];
let linksChecked = 0;
let anchorsChecked = 0;

function mkdocsExcludedPatterns() {
  const lines = readFileSync(join(repoRoot, "mkdocs.yml"), "utf8").split(/\r?\n/);
  const patterns = [];
  let inBlock = false;
  for (const line of lines) {
    if (!inBlock) {
      if (/^exclude_docs:\s*\|\s*$/.test(line)) inBlock = true;
      continue;
    }
    if (!line.trim()) continue;
    if (!/^\s+/.test(line)) break;
    patterns.push(line.trim().replace(/\\/g, "/"));
  }
  return patterns;
}

function globPattern(pattern) {
  let source = "";
  for (let i = 0; i < pattern.length; i += 1) {
    const char = pattern[i];
    if (char === "*" && pattern[i + 1] === "*") {
      source += ".*";
      i += 1;
    } else if (char === "*") {
      source += "[^/]*";
    } else if (char === "?") {
      source += "[^/]";
    } else {
      source += char.replace(/[\\^$.*+?()[\]{}|]/g, "\\$&");
    }
  }
  return new RegExp(`^${source}$`);
}

const excludedDocMatchers = mkdocsExcludedPatterns().map(globPattern);

function isMkdocsExcluded(path) {
  const rel = relative(docsRoot, path).split(sep).join("/");
  if (!rel || rel.startsWith("../")) return false;
  return excludedDocMatchers.some((matcher) => matcher.test(rel));
}

function walk(root, predicate, out = []) {
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const full = join(root, entry.name);
    if (entry.isDirectory()) {
      walk(full, predicate, out);
    } else if (predicate(full)) {
      out.push(full);
    }
  }
  return out;
}

function repoPath(path) {
  return relative(repoRoot, path).split(sep).join("/");
}

function fail(file, message) {
  errors.push(`${repoPath(file)}: ${message}`);
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

function githubSlug(raw) {
  return raw
    .replace(/<[^>]+>/g, "")
    .replace(/!?\[([^\]]*)\]\([^)]+\)/g, "$1")
    .replace(/`([^`]*)`/g, "$1")
    .toLocaleLowerCase("en")
    .trim()
    .replace(/[^\p{L}\p{N}\s_-]/gu, "")
    .replace(/\s/g, "-");
}

const anchorCache = new Map();
function anchorsFor(file) {
  if (anchorCache.has(file)) return anchorCache.get(file);
  const counts = new Map();
  const anchors = new Set();
  for (const line of linesOutsideFences(readFileSync(file, "utf8"))) {
    const heading = line.match(/^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$/);
    if (!heading) continue;
    let title = heading[1].trim();
    const attrId = title.match(/\s*\{#([A-Za-z0-9_.-]+)\}\s*$/);
    if (attrId) {
      anchors.add(attrId[1].toLocaleLowerCase("en"));
      title = title.slice(0, attrId.index).trim();
    }
    const base = githubSlug(title);
    const count = counts.get(base) ?? 0;
    anchors.add(count === 0 ? base : `${base}-${count}`);
    counts.set(base, count + 1);
  }
  anchorCache.set(file, anchors);
  return anchors;
}

function exactCaseExists(path) {
  const rel = relative(repoRoot, path);
  if (rel.startsWith("..") || resolve(path) === repoRoot) return true;
  let current = repoRoot;
  for (const part of rel.split(sep)) {
    if (!existsSync(current) || !statSync(current).isDirectory()) return false;
    if (!readdirSync(current).includes(part)) return false;
    current = join(current, part);
  }
  return true;
}

function splitTarget(raw) {
  let target = raw.trim();
  if (target.startsWith("<") && target.includes(">")) {
    target = target.slice(1, target.indexOf(">"));
  } else {
    target = target.replace(/\s+["'][^"']*["']\s*$/, "");
  }
  const hash = target.indexOf("#");
  return hash >= 0
    ? { path: target.slice(0, hash), fragment: target.slice(hash + 1) }
    : { path: target, fragment: "" };
}

function validateTarget(sourceFile, rawTarget, label = "link", baseDir = dirname(sourceFile)) {
  const { path: encodedPath, fragment: encodedFragment } = splitTarget(rawTarget);
  if (/^(?:https?:|mailto:|data:)/i.test(encodedPath)) return;

  let decodedPath;
  let fragment;
  try {
    decodedPath = decodeURIComponent(encodedPath);
    fragment = decodeURIComponent(encodedFragment).toLocaleLowerCase("en");
  } catch {
    fail(sourceFile, `${label} has invalid percent encoding: ${rawTarget}`);
    return;
  }

  const targetFile = decodedPath
    ? resolve(baseDir, decodedPath.replace(/\//g, sep))
    : sourceFile;
  linksChecked += 1;
  if (!existsSync(targetFile)) {
    fail(sourceFile, `broken ${label}: ${rawTarget}`);
    return;
  }
  if (isMkdocsExcluded(targetFile)) {
    fail(sourceFile, `${label} targets a MkDocs-excluded document: ${rawTarget}`);
    return;
  }
  if (!exactCaseExists(targetFile)) {
    fail(sourceFile, `${label} has incorrect path casing: ${rawTarget}`);
  }

  if (
    fragment &&
    statSync(targetFile).isFile() &&
    [".md", ".txt"].includes(extname(targetFile).toLowerCase())
  ) {
    anchorsChecked += 1;
    if (!anchorsFor(targetFile).has(fragment)) {
      fail(sourceFile, `unknown anchor "#${encodedFragment}" in ${repoPath(targetFile)}`);
    }
  }
}

const docsMarkdown = walk(
  docsRoot,
  (path) => extname(path).toLowerCase() === ".md" && !isMkdocsExcluded(path),
);
const packageReadmes = [
  "packages/ff-occam/README.md",
  "packages/occam-mcp/README.md",
  "packages/occam-agent-sdk/README.md",
  "packages/occam-skill/README.md",
].map((path) => join(repoRoot, path));
const publicSkillCards = [
  "skills/occam/SKILL.md",
  "packages/occam-skill/skill/SKILL.md",
].map((path) => join(repoRoot, path));
const publicMarkdown = [
  join(repoRoot, "README.md"),
  join(repoRoot, "INSTALL.md"),
  join(repoRoot, "MCP_API_SPEC.md"),
  ...docsMarkdown,
  ...packageReadmes,
  ...publicSkillCards,
];
const linkDocuments = [...publicMarkdown, join(repoRoot, "llms.txt")];

for (const file of publicMarkdown) {
  const text = readFileSync(file, "utf8");
  const visible = linesOutsideFences(text).join("\n");
  const h1Count = visible.split(/\r?\n/).filter((line) => /^#\s+\S/.test(line)).length;
  if (h1Count !== 1) fail(file, `expected exactly one H1 outside code fences; found ${h1Count}`);
  if (/[\u0400-\u04ff]/u.test(visible)) {
    fail(file, "public documentation must be English-only");
  }
}

for (const file of linkDocuments) {
  const visible = linesOutsideFences(readFileSync(file, "utf8")).join("\n");
  const pattern = /!?\[[^\]]*\]\(([^)]+)\)/g;
  let match;
  while ((match = pattern.exec(visible)) !== null) {
    validateTarget(file, match[1]);
  }
}

const llmsPath = join(repoRoot, "llms.txt");
if (!existsSync(llmsPath)) {
  fail(llmsPath, "missing LLM documentation entry point");
} else {
  const lines = readFileSync(llmsPath, "utf8").split(/\r?\n/);
  const nonEmpty = lines.filter((line) => line.trim().length > 0);
  if (nonEmpty[0] !== "# Occam") fail(llmsPath, "first non-empty line must be '# Occam'");
  if (!nonEmpty[1]?.startsWith("> ")) fail(llmsPath, "H1 must be followed by a blockquote summary");
  const llmsText = lines.join("\n");
  for (const required of [
    "docs/index.md",
    "docs/quick-start.md",
    "docs/choosing-a-tool.md",
    "docs/tools/index.md",
    "docs/failure-codes.md",
    "docs/configuration.md",
    "docs/trust-and-safety.md",
    "docs/mcp-hosts.md",
    "MCP_API_SPEC.md",
  ]) {
    if (!llmsText.includes(`(${required})`)) fail(llmsPath, `missing required route: ${required}`);
  }
}

const indexPath = join(docsRoot, "index.md");
const indexText = readFileSync(indexPath, "utf8");
for (const file of docsMarkdown
  .filter((path) => dirname(path) === docsRoot && basename(path) !== "index.md")
  .map((path) => basename(path))) {
  if (!indexText.includes(`(${file}`) && !indexText.includes(`(${file}#`)) {
    fail(indexPath, `top-level documentation page is not linked: ${file}`);
  }
}
if (!indexText.includes("(tools/index.md)")) fail(indexPath, "missing route to the per-tool index");
if (!indexText.includes("get-ff-occam.ps1") || !indexText.includes("get-ff-occam.sh")) {
  fail(indexPath, "docs hero must advertise the guarded bootstrap (get-ff-occam.sh and .ps1)");
}

const readmePath = join(repoRoot, "README.md");
const readme = readFileSync(readmePath, "utf8");
for (const required of ["docs/index.md", "docs/quick-start.md", "llms.txt", "AGENTS.md", "INSTALL.md"]) {
  if (!readme.includes(`(${required})`) && !readme.includes(`](${required})`)) {
    // README uses both (path) and bare links; accept either markdown link form.
  }
  if (!readme.includes(required)) fail(readmePath, `missing entry-point reference: ${required}`);
}
if (!readme.includes("get-ff-occam.ps1") || !readme.includes("get-ff-occam.sh")) {
  fail(readmePath, "README must advertise the guarded bootstrap (get-ff-occam.sh and .ps1)");
}
if (/npm install -g ff-occam[^\n]*\n+\s*occam connect\b/i.test(readme)) {
  fail(readmePath, "do not advertise npm install -g followed by occam connect");
}

// Install docs should describe connect, not only "print snippet" as the primary wire path.
const installText = readFileSync(join(repoRoot, "INSTALL.md"), "utf8");
if (!installText.includes("occam connect")) {
  fail(join(repoRoot, "INSTALL.md"), "canonical install must document occam connect");
}
if (/Prints an MCP connection snippet/i.test(installText) && !installText.includes("fallback")) {
  fail(join(repoRoot, "INSTALL.md"), "install narrative still treats snippet printing as the primary wire path");
}

const registryPath = join(
  repoRoot,
  "src",
  "FFOccamMcp.Core",
  "Transport",
  "OccamMcpServerRegistration.cs",
);
const registry = readFileSync(registryPath, "utf8");
const registryBlock = registry.match(/OccamToolNames\s*=\s*\[(.*?)\];/s)?.[1] ?? "";
const coreTools = [...registryBlock.matchAll(/"(occam_[a-z0-9_]+)"/g)].map((match) => match[1]);
if (coreTools.length !== 15) {
  fail(registryPath, `expected 15 tools in the full-profile core catalog; parsed ${coreTools.length}`);
}

const profilePath = join(
  repoRoot,
  "src",
  "FFOccamMcp.Core",
  "Transport",
  "OccamToolProfile.cs",
);
const profileSource = readFileSync(profilePath, "utf8");
const readerBlock = profileSource.match(/ReaderTools\s*=\s*\[(.*?)\];/s)?.[1] ?? "";
const readerTools = [...readerBlock.matchAll(/"(occam_[a-z0-9_]+)"/g)].map(
  (match) => match[1],
);
if (readerTools.length !== 8) {
  fail(profilePath, `expected 8 tools in the default reader profile; parsed ${readerTools.length}`);
}

const version = readFileSync(join(repoRoot, "VERSION"), "utf8").trim();
for (const skillPath of publicSkillCards) {
  const skillVersion = readFileSync(skillPath, "utf8").match(/\bversion:\s*"([^"]+)"/)?.[1];
  if (skillVersion !== version) {
    fail(skillPath, `skill metadata version must match VERSION (${version}); got ${skillVersion ?? "missing"}`);
  }
}

const primaryPackagePath = join(repoRoot, "packages", "ff-occam", "package.json");
const primaryPackage = JSON.parse(readFileSync(primaryPackagePath, "utf8"));
if (primaryPackage.name !== "ff-occam") {
  fail(primaryPackagePath, `primary npm package must be ff-occam; got ${primaryPackage.name}`);
}
if (primaryPackage.bin?.occam) {
  fail(primaryPackagePath, "npm package must not claim the occam bin (that name is the release operator CLI)");
}
if (primaryPackage.version !== version) {
  fail(primaryPackagePath, `package version must match VERSION (${version}); got ${primaryPackage.version}`);
}
if (primaryPackage.dependencies?.["@ff-occam/mcp"] !== version) {
  fail(
    primaryPackagePath,
    `@ff-occam/mcp dependency must be pinned to ${version}; got ${primaryPackage.dependencies?.["@ff-occam/mcp"]}`,
  );
}

for (const path of [
  join(repoRoot, "INSTALL.md"),
  join(docsRoot, "install.md"),
  join(docsRoot, "connect", "after-install.md"),
  join(docsRoot, "faq.md"),
]) {
  const text = readFileSync(path, "utf8");
  const statesReaderCount =
    /\breader\b[^\n]{0,80}\b8\b/i.test(text) || /\b8\b[^\n]{0,80}\breader\b/i.test(text);
  const statesFullCount =
    /\bfull\b[^\n]{0,80}\b15\b/i.test(text) || /\b15\b[^\n]{0,80}\bfull\b/i.test(text);
  if (!statesReaderCount || !statesFullCount) {
    fail(path, "profile-aware install/tool-count prose must state reader=8 and full=15");
  }
  if (/(?:expect|showed)\s+\*\*15\*\*[^\n]*tools/i.test(text)) {
    fail(path, "install/tool health must not hard-require the full-profile 15-tool count");
  }
}

for (const path of [
  join(repoRoot, "README.md"),
  join(docsRoot, "index.md"),
  join(docsRoot, "faq.md"),
  join(repoRoot, "packages", "ff-occam", "README.md"),
]) {
  const text = readFileSync(path, "utf8");
  if (!text.includes(`ff-occam@${version}`)) {
    fail(path, `primary npm command must pin ff-occam@${version}`);
  }
  if (/(?:npm\s+install(?:\s+-g)?|npx)\s+ff-occam-mcp\b/i.test(text)) {
    fail(path, "primary npm command must use ff-occam, not ff-occam-mcp");
  }
  if (/npm install -g ff-occam[^\n]*\n+\s*occam connect\b/i.test(text)) {
    fail(path, "do not advertise npm install -g followed by occam connect");
  }
}

for (const path of publicMarkdown) {
  const text = readFileSync(path, "utf8");
  if (
    /\bnpm\b[^\n]{0,120}\b(?:internal|non-public|not public)\b/i.test(text) ||
    /\b(?:internal|non-public|not public)\b[^\n]{0,120}\bnpm\b/i.test(text)
  ) {
    fail(path, "npm is a public experimental RC channel; do not call it internal or non-public");
  }
  if (/\b15(?:\/51)?\b[^\n]{0,80}\bdefault(?:\s+MCP)?\s+tools\b/i.test(text)) {
    fail(path, "the default reader profile exposes 8 tools; 15 is the full-profile catalog");
  }
}

const toolIndexPath = join(docsRoot, "tools", "index.md");
const toolIndex = readFileSync(toolIndexPath, "utf8");
const combinedReference = [
  toolIndex,
  readFileSync(join(docsRoot, "tools-reference.md"), "utf8"),
  readFileSync(join(repoRoot, "MCP_API_SPEC.md"), "utf8"),
].join("\n");
for (const tool of coreTools) {
  const page = join(docsRoot, "tools", `${tool}.md`);
  if (!existsSync(page)) fail(toolIndexPath, `missing per-tool page: docs/tools/${tool}.md`);
  if (!toolIndex.includes(`(${tool}.md)`)) fail(toolIndexPath, `tool page is not linked: ${tool}.md`);
  if (!combinedReference.includes(`\`${tool}\``)) fail(toolIndexPath, `tool is absent from reference docs: ${tool}`);
}
for (const page of readdirSync(join(docsRoot, "tools"))
  .filter((name) => name.endsWith(".md") && name !== "index.md")) {
  if (!toolIndex.includes(`(${page})`)) fail(toolIndexPath, `orphan per-tool page: ${page}`);
}

const activeTextFiles = [
  ...publicMarkdown,
  join(repoRoot, "scripts", "lib", "operator", "help-catalog.mjs"),
  join(repoRoot, "scripts", "lib", "operator", "occam-command-registry.mjs"),
  join(repoRoot, "scripts", "lib", "resolve-rid.mjs"),
];
const staleNames = [
  "docs/02-installation.md",
  "docs/03-cursor-mcp.md",
  "docs/09-troubleshooting.md",
  "docs/12-cli-reference.md",
  "docs/19-occam-sessions.md",
  "docs/01-operator-journey.md",
  "docs/HOST_INTEGRATION.md",
  "docs/PLAYBOOK_TRUST_MODEL.md",
  "docs/AGENT-FIRST-MVP.md",
  "docs/gitea-actions-ci.md",
  "docs/tool_reference.md",
  "docs/environment.md",
];
for (const file of activeTextFiles) {
  const text = readFileSync(file, "utf8");
  for (const stale of staleNames) {
    if (text.includes(stale)) fail(file, `stale documentation path: ${stale}`);
  }
}

for (const file of activeTextFiles.filter((path) => extname(path) !== ".md" && extname(path) !== ".txt")) {
  const text = readFileSync(file, "utf8");
  const pattern = /docs\/[A-Za-z0-9_.\/-]+\.md(?:#[A-Za-z0-9_.\/-]+)?/g;
  for (const match of text.matchAll(pattern)) {
    validateTarget(file, match[0], "runtime documentation reference", repoRoot);
  }
}

const { errors: discErrors, warnings: discWarnings } = checkDiscoverability(repoRoot);
for (const warning of discWarnings) console.warn(`  warn: ${warning}`);
errors.push(...discErrors);

const honestyErrors = checkHonesty(repoRoot);
errors.push(...honestyErrors);

if (errors.length > 0) {
  console.error(`docs-check: FAILED (${errors.length} issue${errors.length === 1 ? "" : "s"})`);
  for (const error of errors) console.error(`  - ${error}`);
  process.exit(1);
}

console.log(
  `docs-check: OK — ${linkDocuments.length} documents, ${linksChecked} local links, ` +
    `${anchorsChecked} anchors, ${coreTools.length} core tools, discoverability + honesty gates`,
);
