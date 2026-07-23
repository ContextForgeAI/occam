#!/usr/bin/env node

import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const docsRoot = join(repoRoot, "docs");
const errors = [];
let linksChecked = 0;
let anchorsChecked = 0;

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
    const base = githubSlug(heading[1]);
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

const docsMarkdown = walk(docsRoot, (path) => extname(path).toLowerCase() === ".md");
const packageReadmes = [
  "packages/occam-mcp/README.md",
  "packages/occam-agent-sdk/README.md",
  "packages/occam-skill/README.md",
].map((path) => join(repoRoot, path));
const publicMarkdown = [
  join(repoRoot, "README.md"),
  join(repoRoot, "INSTALL.md"),
  join(repoRoot, "MCP_API_SPEC.md"),
  ...docsMarkdown,
  ...packageReadmes,
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
  if (nonEmpty[0] !== "# FF-Occam MCP") fail(llmsPath, "first non-empty line must be '# FF-Occam MCP'");
  if (!nonEmpty[1]?.startsWith("> ")) fail(llmsPath, "H1 must be followed by a blockquote summary");
  const llmsText = lines.join("\n");
  for (const required of [
    "docs/index.md",
    "docs/choosing-a-tool.md",
    "docs/tools/index.md",
    "docs/failure-codes.md",
    "docs/configuration.md",
    "MCP_API_SPEC.md",
  ]) {
    if (!llmsText.includes(`(${required})`)) fail(llmsPath, `missing required route: ${required}`);
  }
}

const indexPath = join(docsRoot, "index.md");
const indexText = readFileSync(indexPath, "utf8");
for (const file of readdirSync(docsRoot, { withFileTypes: true })
  .filter((entry) => entry.isFile() && entry.name.endsWith(".md") && entry.name !== "index.md")
  .map((entry) => entry.name)) {
  if (!indexText.includes(`(${file}`) && !indexText.includes(`(${file}#`)) {
    fail(indexPath, `top-level documentation page is not linked: ${file}`);
  }
}
if (!indexText.includes("(tools/index.md)")) fail(indexPath, "missing route to the per-tool index");

const readmePath = join(repoRoot, "README.md");
const readme = readFileSync(readmePath, "utf8");
for (const required of ["docs/index.md", "llms.txt", "AGENTS.md"]) {
  if (!readme.includes(`(${required})`)) fail(readmePath, `missing entry-point link: ${required}`);
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
  fail(registryPath, `expected 15 always-on tools; parsed ${coreTools.length}`);
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

if (errors.length > 0) {
  console.error(`docs-check: FAILED (${errors.length} issue${errors.length === 1 ? "" : "s"})`);
  for (const error of errors) console.error(`  - ${error}`);
  process.exit(1);
}

console.log(
  `docs-check: OK — ${linkDocuments.length} documents, ${linksChecked} local links, ` +
    `${anchorsChecked} anchors, ${coreTools.length} core tools`,
);
