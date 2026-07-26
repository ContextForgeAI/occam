#!/usr/bin/env node
/**
 * Honesty phrase gate — deny risky trust/marketing claims in public docs.
 * Negation contexts ("does NOT prove truth", "not a consensus proof") are allowed.
 *
 * Run standalone: node scripts/check-docs-honesty.mjs
 * Also invoked from scripts/check-docs.mjs
 */
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/** @typedef {{ id: string, pattern: RegExp, hint: string }} DenyRule */

/** Maintainable deny list — extend from TRUST-MODEL §13 / HONESTY-SCHEMA-MAP */
export const DENY_RULES = /** @type {DenyRule[]} */ ([
  { id: "consensus-proof", pattern: /\bconsensus proof\b/i, hint: "Use multi-source comparison; never consensus proof (OD-8)" },
  { id: "proves-truth", pattern: /\bproves?\s+(?:the\s+)?truth\b/i, hint: "Integrity vs key, not truth" },
  { id: "trusted-timestamp", pattern: /\btrusted timestamp\b/i, hint: "Time anchor is reported, not trusted time" },
  { id: "origin-authenticity", pattern: /\borigin authenticity\b/i, hint: "Receipt does not prove origin served bytes" },
  {
    id: "cryptographic-attest",
    pattern: /\bcryptographic(?:ally)?\s+attest(?:ation)?\b/i,
    hint: "occam_attest is heuristic citation assessment (OD-7)",
  },
  { id: "verified-true", pattern: /\bverified true\b/i, hint: "verified = integrity vs supplied key" },
  { id: "proves-page-said", pattern: /\bproves?\s+(?:the\s+)?page\s+said\b/i, hint: "Receipt binds compiled bytes only" },
  { id: "tamper-proof", pattern: /\btamper-proof\b/i, hint: "Prefer tamper-evident" },
  {
    id: "third-party-verifiable",
    pattern: /\bthird-party verifiable\b/i,
    hint: "Verifier needs out-of-band key; unqualified claim forbidden",
  },
  { id: "signed-by-occam", pattern: /\bsigned by Occam\b/i, hint: "Signed by local install key, not vendor identity" },
  { id: "multi-node-consensus", pattern: /\bmulti-node consensus\b/i, hint: "Same-process crosscheck only" },
  { id: "n-of-m", pattern: /\bN-of-M\b/i, hint: "No quorum semantics shipped" },
  { id: "signed-bundles-capsule", pattern: /\bcapsules?\s+are\s+signed\s+bundles?\b/i, hint: "Capsule wrapper is unsigned" },
  {
    id: "cosign-verified-install",
    pattern: /\bcosign-verified install\b/i,
    hint: "Installers do not enforce Cosign (OD-2)",
  },
  {
    id: "signed-supply-chain",
    pattern: /\bsupply chain is signed\b/i,
    hint: "SHA-256 manifest only; Cosign bundle unused by install",
  },
  {
    id: "cryptographic-provenance",
    pattern: /\bcryptographically verified provenance\b/i,
    hint: "Local self-signed integrity only",
  },
  {
    id: "proves-extraction-happened",
    pattern: /\bproves?\s+(?:that\s+)?extraction happened\b/i,
    hint: "Proves bytes relative to key, not fetch occurrence",
  },
]);

/**
 * Negation window before a match — if any pattern matches, the deny rule is suppressed.
 * @type {RegExp[]}
 */
export const NEGATION_CONTEXT = [
  /\b(?:does|do|did)\s+not\b/i,
  /\b(?:is|are|was|were)\s+not\b/i,
  /\bnot\s+a\b/i,
  /\bnot\s+an\b/i,
  /\bnever\b/i,
  /\bforbidden\b/i,
  /\bdo not\b/i,
  /\bdon't\b/i,
  /\bdoesn't\b/i,
  /\bno\b/i,
  /\bwithout\b/i,
  /\bnot\s+proof\b/i,
  /\bnot\s+cryptographic\b/i,
  /\bnot\s+consensus\b/i,
  /\bnot\s+ga\b/i,
  /\bnot\s+enforced\b/i,
  /\bnot\s+prove\b/i,
  /\bnot\s+proves\b/i,
  /\bnot\s+trusted\b/i,
  /\bnot\s+verify\b/i,
  /\bnot\s+verifiable\b/i,
  /\bnot\s+attestation\b/i,
  /\bnot\s+the\s+same\b/i,
  /\bnot\s+a\s+consensus\b/i,
  /\bnot\s+consensus\s+proof\b/i,
  /\bnot\s+proof\s+of\b/i,
  /\bnot\s+cryptographic\s+attest/i,
  /\bnot\s+origin\b/i,
  /\bnot\s+truth\b/i,
  /\bnot\s+ga\s+1\.0\b/i,
  /\bnot\s+a\s+ga\b/i,
  /\bnot\s+enforce\b/i,
  /\bnot\s+cosign\b/i,
  /\bnon-ga\b/i,
  /\bnon\s+ga\b/i,
  /\bexperimental\b/i,
  /\blimits?\b/i,
  /\bforbidden readings?\b/i,
  /\boverclaim\b/i,
  /\boverstates?\b/i,
  /\bname overclaims?\b/i,
  /\bneither\b/i,
  /\bneither proves\b/i,
  /\bnot prove\b/i,
  /\bnot\s+proven\b/i,
  /\bnot\s+cryptographically\b/i,
  /\bnot\s+a\s+cryptographic\b/i,
  /\bnot\s+attest(?:ation)?\b/i,
  /\bnot\s+consensus\b/i,
  /\bnot\s+consensus\s+proof\b/i,
  /\bnot\s+proof\s+of\b/i,
  /\bnot\s+cryptographic\s+attest/i,
  /\bnot\s+origin\b/i,
  /\bnot\s+truth\b/i,
  /\bnot\s+proof\b/i,
  /\bnot\s+trusted\b/i,
  /\bnot\s+externally\b/i,
  /\bnot\s+accepted\b/i,
  /\bnot\s+evaluate\b/i,
  /\bnot\s+a\s+ga\b/i,
  /\bnot\s+ga\s+1\.0\b/i,
  /\bnot\s+enforce\b/i,
  /\bnot\s+cosign\b/i,
  /\bnot\s+semantic\b/i,
  /\bnot\s+factual\b/i,
  /\bnot\s+vendor\b/i,
];

const SCAN_ROOTS = [
  join(repoRoot, "README.md"),
  join(repoRoot, "INSTALL.md"),
  join(repoRoot, "llms.txt"),
  join(repoRoot, "MCP_API_SPEC.md"),
  join(repoRoot, "docs"),
];

const SKIP_DIR_NAMES = new Set(["rc2", "maintenance", "development"]);

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

function collectMarkdownFiles(root) {
  const out = [];
  function walk(dir) {
    if (!existsSync(dir)) return;
    const st = statSync(dir);
    if (st.isFile()) {
      const ext = extname(dir).toLowerCase();
      if (ext === ".md" || ext === ".txt") out.push(dir);
      return;
    }
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (SKIP_DIR_NAMES.has(entry.name)) continue;
        walk(join(dir, entry.name));
      } else if (/\.(md|txt)$/i.test(entry.name)) {
        out.push(join(dir, entry.name));
      }
    }
  }
  for (const rootPath of SCAN_ROOTS) walk(rootPath);
  return [...new Set(out)];
}

function normalizeMarkdown(s) {
  return s
    .replace(/\*{1,2}([^*]+)\*{1,2}/g, "$1")
    .replace(/_{1,2}([^_]+)_{1,2}/g, "$1")
    .replace(/`/g, "");
}

function negatedContext(text, index) {
  const windowStart = Math.max(0, index - 160);
  const before = normalizeMarkdown(text.slice(windowStart, index));
  if (NEGATION_CONTEXT.some((re) => re.test(before))) return true;

  const beforeTrimmed = before.trimEnd();
  if (/\b(?:not|never|neither)\b[\s—–-]*$/i.test(beforeTrimmed)) return true;
  if (/\bnot\s+cryptographic[\s—–-]*$/i.test(beforeTrimmed)) return true;

  const lineStart = text.lastIndexOf("\n", index - 1) + 1;
  const lineEnd = text.indexOf("\n", index);
  const line = text.slice(lineStart, lineEnd === -1 ? text.length : lineEnd);
  const relIdx = normalizeMarkdown(text.slice(lineStart, index)).length;
  const beforeInLine = normalizeMarkdown(line).slice(0, relIdx);
  if (NEGATION_CONTEXT.some((re) => re.test(beforeInLine))) return true;
  if (/\b(?:not|never|neither)\b[\s—–-]*$/i.test(beforeInLine.trimEnd())) return true;

  if (lineStart > 0) {
    const prevLineStart = text.lastIndexOf("\n", lineStart - 2) + 1;
    const prevLine = normalizeMarkdown(text.slice(prevLineStart, lineStart - 1));
    if (/\b(?:not|never|neither)\b[\s—–-]*$/i.test(prevLine.trimEnd())) return true;
    if (/\bnot\s+cryptographic[\s—–-]*$/i.test(prevLine.trimEnd())) return true;
  }

  return false;
}

/** Allowlists for intentional forbidden-claim inventories and honest "Not" tables. */
function allowedInventoryContext(text, index, matchText, line) {
  const sectionBefore = text.slice(Math.max(0, index - 1400), index);
  if (
    /Forbidden phrasing|Do not write|Claims we do not make|What it does not promise|Limitations — eight sentences|Common misconception|Related surfaces \(honest labels\)|Honest meaning/i.test(
      sectionBefore,
    )
  ) {
    return true;
  }
  if (/what it does not promise/i.test(sectionBefore) && /^\s*\|/.test(line)) {
    return true;
  }
  const cols = line.split("|");
  if (cols.length >= 4) {
    const notCol = cols[cols.length - 2] ?? "";
    if (notCol.toLowerCase().includes(matchText.toLowerCase())) return true;
  }
  return false;
}

/**
 * @param {string} [root]
 * @returns {string[]}
 */
export function checkHonesty(root = repoRoot) {
  const errors = [];
  const files = collectMarkdownFiles(root);

  for (const file of files) {
    const rel = relative(root, file).split(sep).join("/");
    const visible = linesOutsideFences(readFileSync(file, "utf8")).join("\n");

    for (const rule of DENY_RULES) {
      const re = new RegExp(
        rule.pattern.source,
        rule.pattern.flags.includes("g") ? rule.pattern.flags : `${rule.pattern.flags}g`,
      );
      let match;
      while ((match = re.exec(visible)) !== null) {
        const lineStart = visible.lastIndexOf("\n", match.index - 1) + 1;
        const lineEnd = visible.indexOf("\n", match.index);
        const line = visible.slice(lineStart, lineEnd === -1 ? visible.length : lineEnd);
        if (negatedContext(visible, match.index)) continue;
        if (allowedInventoryContext(visible, match.index, match[0], line)) continue;
        const lineNo = visible.slice(0, match.index).split("\n").length;
        errors.push(
          `${rel}:${lineNo}: HONESTY-${rule.id}: "${match[0]}" — ${rule.hint}`,
        );
      }
    }
  }

  return errors;
}

function main() {
  const errors = checkHonesty();
  if (errors.length) {
    console.error(`docs-honesty: FAILED (${errors.length})`);
    for (const e of errors) console.error(`  - ${e}`);
    process.exit(1);
  }
  console.log(`docs-honesty: OK — ${DENY_RULES.length} deny rules, ${collectMarkdownFiles(repoRoot).length} files`);
}

const isMain =
  process.argv[1] &&
  import.meta.url === pathToFileURL(resolve(process.argv[1])).href;

if (isMain) main();
