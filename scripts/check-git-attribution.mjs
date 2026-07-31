#!/usr/bin/env node
/**
 * Reject commits whose Git metadata attributes authorship to AI tools.
 *
 * Inspects author, committer, and attribution trailers only — not file content.
 *
 * Usage:
 *   node scripts/check-git-attribution.mjs
 *   node scripts/check-git-attribution.mjs --range A..B
 *   node scripts/check-git-attribution.mjs --stdin
 *
 * Exit 0 = pass, 1 = fail.
 */
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

const FORBIDDEN_IDENTITY =
  /^(cursor|cursoragent|codex|claude|openai|anthropic)(\b|[._-])/i;

const FORBIDDEN_EMAIL =
  /(cursoragent@cursor\.com|@cursor\.com$|@cursor\.sh$|@openai\.com$|@anthropic\.com$)/i;

const FORBIDDEN_TRAILER =
  /^(Co-authored-by|Made-with|Generated-by|Assisted-by|AI-generated-by):\s*.*(Cursor|cursoragent|Codex|Claude|OpenAI|Anthropic)\b/im;

function parseArgs(argv) {
  const out = { range: null, stdin: false, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--help" || a === "-h") out.help = true;
    else if (a === "--stdin") out.stdin = true;
    else if (a === "--range") out.range = argv[++i];
    else if (a.startsWith("--range=")) out.range = a.slice("--range=".length);
  }
  return out;
}

function git(args) {
  return execFileSync("git", args, {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trimEnd();
}

function identityForbidden(name, email) {
  const n = (name || "").trim();
  const e = (email || "").trim();
  if (n && FORBIDDEN_IDENTITY.test(n)) return `name=${n}`;
  if (e && FORBIDDEN_EMAIL.test(e)) return `email=${e}`;
  if (e) {
    const local = e.split("@")[0] || "";
    if (FORBIDDEN_IDENTITY.test(local)) return `email=${e}`;
  }
  return null;
}

export function findForbiddenTrailers(message) {
  const hits = [];
  for (const line of String(message || "").split(/\r?\n/)) {
    if (FORBIDDEN_TRAILER.test(line)) hits.push(line.trim());
  }
  return hits;
}

export function evaluateCommit({
  hash,
  authorName,
  authorEmail,
  committerName,
  committerEmail,
  message,
}) {
  const errors = [];
  const badAuthor = identityForbidden(authorName, authorEmail);
  if (badAuthor) errors.push(`forbidden author (${badAuthor})`);
  const badCommitter = identityForbidden(committerName, committerEmail);
  if (badCommitter) errors.push(`forbidden committer (${badCommitter})`);
  for (const trailer of findForbiddenTrailers(message)) {
    errors.push(`forbidden trailer: ${trailer}`);
  }
  return { hash: hash || "(message)", errors };
}

function defaultRange() {
  const event = process.env.GITHUB_EVENT_NAME || "";
  const base = process.env.GITHUB_BASE_REF;
  const sha = process.env.GITHUB_SHA;
  if (event === "pull_request" && base && sha) {
    return `origin/${base}..${sha}`;
  }
  if (event === "push" && process.env.GITHUB_EVENT_BEFORE && sha) {
    const before = process.env.GITHUB_EVENT_BEFORE;
    if (/^0+$/.test(before)) return sha;
    return `${before}..${sha}`;
  }
  for (const candidate of ["public/main", "origin/main", "main"]) {
    try {
      git(["rev-parse", "--verify", candidate]);
      return `${candidate}..HEAD`;
    } catch {
      /* try next */
    }
  }
  return "HEAD";
}

function loadCommits(range) {
  const fmt = "%H%x1f%an%x1f%ae%x1f%cn%x1f%ce%x1f%B%x1e";
  const raw =
    range === "HEAD" || !String(range).includes("..")
      ? git(["log", "-1", `--format=${fmt}`, range || "HEAD"])
      : git(["log", range, `--format=${fmt}`]);
  if (!raw.trim()) return [];
  return raw
    .split("\x1e")
    .filter((chunk) => chunk.trim())
    .map((chunk) => {
      const parts = chunk.replace(/^\n/, "").split("\x1f");
      return {
        hash: parts[0],
        authorName: parts[1],
        authorEmail: parts[2],
        committerName: parts[3],
        committerEmail: parts[4],
        message: parts[5] || "",
      };
    });
}

export function main(argv = process.argv.slice(2)) {
  const args = parseArgs(argv);
  if (args.help) {
    console.log(`Usage:
  node scripts/check-git-attribution.mjs [--range A..B]
  node scripts/check-git-attribution.mjs --stdin`);
    return 0;
  }

  if (args.stdin) {
    const message = readFileSync(0, "utf8");
    const result = evaluateCommit({ message });
    if (result.errors.length) {
      console.error("git-attribution: FAILED (commit message)");
      for (const e of result.errors) console.error(`  - ${e}`);
      return 1;
    }
    console.log("git-attribution: OK (message)");
    return 0;
  }

  const range = args.range || defaultRange();
  let commits;
  try {
    commits = loadCommits(range);
  } catch (err) {
    console.error(`git-attribution: cannot read range ${range}: ${err.message || err}`);
    return 1;
  }

  if (commits.length === 0) {
    console.log(`git-attribution: OK — no commits in ${range}`);
    return 0;
  }

  const failures = [];
  for (const c of commits) {
    const result = evaluateCommit(c);
    if (result.errors.length) failures.push(result);
  }

  if (failures.length) {
    console.error(`git-attribution: FAILED (${failures.length}/${commits.length} in ${range})`);
    for (const f of failures) {
      console.error(`  ${String(f.hash).slice(0, 12)}`);
      for (const e of f.errors) console.error(`    - ${e}`);
    }
    return 1;
  }

  console.log(`git-attribution: OK — ${commits.length} commit(s) in ${range}`);
  return 0;
}

const isDirectRun = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isDirectRun) {
  process.exit(main());
}
