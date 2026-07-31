#!/usr/bin/env node
/**
 * Self-test for scripts/check-git-attribution.mjs
 * Run: node scripts/check-git-attribution.selftest.mjs
 */
import { evaluateCommit, findForbiddenTrailers } from "./check-git-attribution.mjs";

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

function expectFail(label, commit) {
  const r = evaluateCommit(commit);
  assert(r.errors.length > 0, `${label}: expected failure, got pass`);
}

function expectPass(label, commit) {
  const r = evaluateCommit(commit);
  assert(r.errors.length === 0, `${label}: expected pass, got ${r.errors.join("; ")}`);
}

// Normal human commit
expectPass("human", {
  hash: "h1",
  authorName: "Pavel",
  authorEmail: "family.pavel.gemini@gmail.com",
  committerName: "Pavel",
  committerEmail: "family.pavel.gemini@gmail.com",
  message: "docs: mention Cursor as a supported MCP host\n",
});

// Cursor author
expectFail("cursor-author", {
  hash: "h2",
  authorName: "Cursor",
  authorEmail: "cursoragent@cursor.com",
  committerName: "Pavel",
  committerEmail: "family.pavel.gemini@gmail.com",
  message: "fix: something\n",
});

// Cursor committer
expectFail("cursor-committer", {
  hash: "h3",
  authorName: "Pavel",
  authorEmail: "family.pavel.gemini@gmail.com",
  committerName: "Cursor",
  committerEmail: "cursoragent@cursor.com",
  message: "fix: something\n",
});

// Co-authored-by Cursor
expectFail("coauthor-cursor", {
  hash: "h4",
  authorName: "Pavel",
  authorEmail: "family.pavel.gemini@gmail.com",
  committerName: "Pavel",
  committerEmail: "family.pavel.gemini@gmail.com",
  message: "docs: polish homepage\n\nCo-authored-by: Cursor <cursoragent@cursor.com>\n",
});

// Ordinary documentation sentence mentioning Cursor in subject/body (not a trailer)
expectPass("docs-mention-cursor", {
  hash: "h5",
  authorName: "Pavel",
  authorEmail: "family.pavel.gemini@gmail.com",
  committerName: "Pavel",
  committerEmail: "family.pavel.gemini@gmail.com",
  message:
    "fix(install): honest Cursor configured state\n\nClarify when Cursor mcp.json is written but tools are not loaded yet.\n",
});

// Commit about Cursor host adapter
expectPass("cursor-adapter", {
  hash: "h6",
  authorName: "Pavel",
  authorEmail: "family.pavel.gemini@gmail.com",
  committerName: "Pavel",
  committerEmail: "family.pavel.gemini@gmail.com",
  message: "feat(connect): add Cursor adapter and wire Wave 2 registry\n",
});

assert(
  findForbiddenTrailers("Co-authored-by: Cursor <cursoragent@cursor.com>").length === 1,
  "trailer detector"
);
assert(
  findForbiddenTrailers("Clarify Cursor mcp.json behavior").length === 0,
  "non-trailer Cursor mention"
);

console.log("check-git-attribution.selftest: OK");
