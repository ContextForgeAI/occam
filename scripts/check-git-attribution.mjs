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

/** Exact AI-tool display names (not substrings of ordinary human names). */
const FORBIDDEN_EXACT_NAME =
  /^(cursor|cursoragent|codex|openai|anthropic|claude code|claude bot|claude\.ai)$/i;

const FORBIDDEN_EMAIL =
  /(cursoragent@cursor\.com|@cursor\.com$|@cursor\.sh$|@openai\.com$|@anthropic\.com$)/i;

/** Trailer keys that attribute authorship to AI tools. */
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
  if (n && FORBIDDEN_EXACT_NAME.test(n)) return `name=${n}`;
  // Bare "Claude" is only forbidden with an Anthropic automation email.
  if (/^claude$/i.test(n) && /@anthropic\.com$/i.test(e)) return `name=${n}`;
  if (e && FORBIDDEN_EMAIL.test(e)) return `email=${e}`;
  if (e) {
    const local = e.split("@")[0] || "";
    if (/^(cursor|cursoragent|codex|openai|anthropic)([._-]|$)/i.test(local)) {
      return `email=${e}`;
    }
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

function defaultRangeSpec() {
  const event = process.env.GITHUB_EVENT_NAME || "";
  const base = process.env.GITHUB_BASE_REF;
  const sha = process.env.GITHUB_SHA;
  if (event === "pull_request" && base && sha) {
    // Prefer merge-base range; fall back to origin/base..sha then reachable tip.
    return {
      primary: `origin/${base}..${sha}`,
      fallbackReachable: sha,
    };
  }
  if (event === "push" && process.env.GITHUB_EVENT_BEFORE && sha) {
    const before = process.env.GITHUB_EVENT_BEFORE;
    if (/^0+$/.test(before)) {
      // First push of a branch: scan full reachable history from tip.
      return { primary: null, fallbackReachable: sha };
    }
    return {
      primary: `${before}..${sha}`,
      // Force-push / rewritten history: old tip may be absent or unrelated.
      fallbackReachable: sha,
    };
  }
  for (const candidate of ["public/main", "origin/main", "main"]) {
    try {
      git(["rev-parse", "--verify", candidate]);
      return { primary: `${candidate}..HEAD`, fallbackReachable: "HEAD" };
    } catch {
      /* try next */
    }
  }
  return { primary: null, fallbackReachable: "HEAD" };
}

function parseLog(raw) {
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

function loadCommitsFromArgs(gitArgs) {
  const fmt = "%H%x1f%an%x1f%ae%x1f%cn%x1f%ce%x1f%B%x1e";
  return parseLog(git(["log", ...gitArgs, `--format=${fmt}`]));
}

function loadCommits(rangeOrSpec) {
  if (typeof rangeOrSpec === "string") {
    const range = rangeOrSpec;
    if (!range.includes("..")) {
      // Explicit single rev without "..": check that commit only (CLI convenience).
      return loadCommitsFromArgs(["-1", range || "HEAD"]);
    }
    return loadCommitsFromArgs([range]);
  }

  const spec = rangeOrSpec || defaultRangeSpec();
  if (spec.primary) {
    try {
      // Ensure both ends exist when possible; force-push may omit "before".
      const before = spec.primary.split("..")[0];
      if (before) git(["cat-file", "-e", `${before}^{commit}`]);
      return loadCommitsFromArgs([spec.primary]);
    } catch {
      /* fall through */
    }
  }
  const tip = spec.fallbackReachable || "HEAD";
  // Full ancestry from tip — used after force-push / first push / missing base.
  return loadCommitsFromArgs([tip]);
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

  const rangeLabel = args.range || "(auto)";
  let commits;
  try {
    commits = args.range ? loadCommits(args.range) : loadCommits(defaultRangeSpec());
  } catch (err) {
    console.error(`git-attribution: cannot read range ${rangeLabel}: ${err.message || err}`);
    return 1;
  }

  if (commits.length === 0) {
    console.log(`git-attribution: OK — no commits in ${rangeLabel}`);
    return 0;
  }

  const failures = [];
  for (const c of commits) {
    const result = evaluateCommit(c);
    if (result.errors.length) failures.push(result);
  }

  if (failures.length) {
    console.error(
      `git-attribution: FAILED (${failures.length}/${commits.length} in ${rangeLabel})`,
    );
    for (const f of failures) {
      console.error(`  ${String(f.hash).slice(0, 12)}`);
      for (const e of f.errors) console.error(`    - ${e}`);
    }
    return 1;
  }

  console.log(`git-attribution: OK — ${commits.length} commit(s) in ${rangeLabel}`);
  return 0;
}

const isDirectRun = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isDirectRun) {
  process.exit(main());
}
