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

const human = {
  authorName: "Pavel",
  authorEmail: "family.pavel.gemini@gmail.com",
  committerName: "Pavel",
  committerEmail: "family.pavel.gemini@gmail.com",
};

// Normal human commit
expectPass("human", {
  hash: "h1",
  ...human,
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
  ...human,
  message: "docs: polish homepage\n\nCo-authored-by: Cursor <cursoragent@cursor.com>\n",
});

// Co-authored-by Codex AI identity
expectFail("coauthor-codex", {
  hash: "h4b",
  ...human,
  message: "chore: experiment\n\nCo-authored-by: Codex <codex@openai.com>\n",
});

// Co-authored-by Claude AI identity
expectFail("coauthor-claude", {
  hash: "h4c",
  ...human,
  message: "chore: experiment\n\nCo-authored-by: Claude <claude@anthropic.com>\n",
});

// Generated-by / Made-with prohibited trailers
expectFail("generated-by-cursor", {
  hash: "h4d",
  ...human,
  message: "chore: experiment\n\nGenerated-by: Cursor\n",
});
expectFail("made-with-codex", {
  hash: "h4e",
  ...human,
  message: "chore: experiment\n\nMade-with: Codex\n",
});

// Ordinary documentation sentence mentioning Cursor in subject/body (not a trailer)
expectPass("docs-mention-cursor", {
  hash: "h5",
  ...human,
  message:
    "fix(install): honest Cursor configured state\n\nClarify when Cursor mcp.json is written but tools are not loaded yet.\n",
});

// Docs mentioning Codex / Claude as products
expectPass("docs-mention-codex-claude", {
  hash: "h5b",
  ...human,
  message:
    "docs: compare Cursor, Codex, and Claude host setup notes\n\nNo attribution trailers.\n",
});

// Commit about Cursor host adapter
expectPass("cursor-adapter", {
  hash: "h6",
  ...human,
  message: "feat(connect): add Cursor adapter and wire Wave 2 registry\n",
});

// Human whose given name is Claude (must not false-positive)
expectPass("human-named-claude", {
  hash: "h7",
  authorName: "Claude Dupont",
  authorEmail: "claude.dupont@example.org",
  committerName: "Claude Dupont",
  committerEmail: "claude.dupont@example.org",
  message: "fix: typo\n",
});

assert(
  findForbiddenTrailers("Co-authored-by: Cursor <cursoragent@cursor.com>").length === 1,
  "trailer detector",
);
assert(
  findForbiddenTrailers("Clarify Cursor mcp.json behavior").length === 0,
  "non-trailer Cursor mention",
);

console.log("check-git-attribution.selftest: OK");
