/**
 * PB4c community publish sanitization — mirrors PlaybookCommunityHygiene + PlaybookCommunitySanitizer.
 * Rejects secrets and denylisted selectors before export; caps lessons/agent_notes on successful export.
 */

import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

export const FORBIDDEN_KEYS = new Set([
  "cookie",
  "cookies",
  "authorization",
  "set-cookie",
  "set_cookie",
  "bearer",
  "bearer_token",
  "api_key",
  "apikey",
  "password",
  "secret_key",
  "session_token",
  "access_token",
  "refresh_token",
]);

export const FORBIDDEN_HEADER_NAMES = new Set([
  "cookie",
  "authorization",
  "proxy-authorization",
  "x-api-key",
  "api-key",
]);

export const FORBIDDEN_NOTE_MARKERS = [
  "cookie:",
  "authorization:",
  "bearer ",
  "password=",
  "sid=",
];

export const DENYLIST_SELECTORS = new Set(["body", "html", "*"]);

export const MAX_LESSONS = 10;
export const MAX_AGENT_NOTES = 2000;

function walkForbiddenKeys(value) {
  if (value === null || typeof value !== "object") {
    return false;
  }
  if (Array.isArray(value)) {
    return value.some(walkForbiddenKeys);
  }
  for (const [key, child] of Object.entries(value)) {
    if (FORBIDDEN_KEYS.has(key.toLowerCase())) {
      return true;
    }
    if (walkForbiddenKeys(child)) {
      return true;
    }
  }
  return false;
}

export function containsForbiddenKeys(input) {
  try {
    const doc = typeof input === "string" ? JSON.parse(input) : input;
    return walkForbiddenKeys(doc);
  } catch {
    return true;
  }
}

function collectSelectors(doc) {
  const selectors = [];
  const extract = doc?.extract;
  if (!extract || typeof extract !== "object") {
    return selectors;
  }
  for (const field of ["contentSelectors", "domStripSelectors"]) {
    const list = extract[field];
    if (Array.isArray(list)) {
      for (const item of list) {
        if (typeof item === "string" && item.trim()) {
          selectors.push(item.trim());
        }
      }
    }
  }
  return selectors;
}

export function isDenylistedSelector(selector) {
  const normalized = String(selector).trim().toLowerCase();
  if (DENYLIST_SELECTORS.has(normalized)) {
    return true;
  }
  return normalized.includes("[style*=");
}

function checkAgentNotes(doc, violations) {
  const notes = doc?.agent_notes;
  if (typeof notes !== "string" || !notes.trim()) {
    return;
  }
  for (const marker of FORBIDDEN_NOTE_MARKERS) {
    if (notes.toLowerCase().includes(marker)) {
      violations.push("forbidden_agent_notes");
      return;
    }
  }
}

function checkRequestHeaders(doc, violations) {
  const headers = doc?.request?.headers;
  if (!headers || typeof headers !== "object" || Array.isArray(headers)) {
    return;
  }
  for (const key of Object.keys(headers)) {
    if (FORBIDDEN_HEADER_NAMES.has(key.toLowerCase())) {
      violations.push(`forbidden_header:${key}`);
    }
  }
}

function checkSelectors(doc, violations) {
  for (const selector of collectSelectors(doc)) {
    if (isDenylistedSelector(selector)) {
      violations.push(`denylist_selector:${selector}`);
    }
  }
}

/** Returns violation strings; empty array means export-safe input. */
export function findViolations(input) {
  let doc;
  try {
    doc = typeof input === "string" ? JSON.parse(input) : structuredClone(input);
  } catch {
    return ["invalid_json"];
  }

  const violations = [];
  if (containsForbiddenKeys(doc)) {
    violations.push("forbidden_key");
  }
  checkRequestHeaders(doc, violations);
  checkSelectors(doc, violations);
  checkAgentNotes(doc, violations);
  return violations;
}

function stripForbiddenHeaders(doc) {
  const headers = doc?.request?.headers;
  if (!headers || typeof headers !== "object" || Array.isArray(headers)) {
    return;
  }
  for (const key of Object.keys(headers)) {
    if (FORBIDDEN_HEADER_NAMES.has(key.toLowerCase())) {
      delete headers[key];
    }
  }
  if (Object.keys(headers).length === 0 && doc.request) {
    delete doc.request.headers;
    if (Object.keys(doc.request).length === 0) {
      delete doc.request;
    }
  }
}

/** Sanitize caps and strip removable headers (post-violation-check export path only). */
export function sanitizeForExport(input) {
  const doc = typeof input === "string" ? JSON.parse(input) : structuredClone(input);

  stripForbiddenHeaders(doc);

  if (Array.isArray(doc.lessons) && doc.lessons.length > MAX_LESSONS) {
    doc.lessons = doc.lessons.slice(-MAX_LESSONS);
  }

  if (typeof doc.agent_notes === "string" && doc.agent_notes.length > MAX_AGENT_NOTES) {
    doc.agent_notes = doc.agent_notes.slice(0, MAX_AGENT_NOTES);
  }

  doc.schema_version ??= "1.0";
  const meta = doc.meta && typeof doc.meta === "object" ? doc.meta : {};
  meta.updated = new Date().toISOString().slice(0, 10);
  meta.author = meta.author ?? "community-contributor";
  meta.confidence ??= 0.7;
  doc.meta = meta;

  return doc;
}

export function sha256Hex(content) {
  return createHash("sha256").update(content, "utf8").digest("hex");
}

export function buildManifestRow({ id, hosts, schemaVersion, fileName, sha256, signedAt }) {
  const row = {
    id,
    hosts,
    schema_version: schemaVersion ?? "1.0",
    file: fileName,
    sha256,
  };
  if (signedAt !== false) {
    row.signed_at = signedAt ?? new Date().toISOString().slice(0, 10);
    row.signer = "occam-maintainers-ed25519";
    row.signature = null;
  }
  return row;
}

export function buildPullRequestMarkdown({
  playbookId,
  hosts,
  summary,
  exportPath,
  communityTarget,
  manifestRow,
}) {
  const hostList = Array.isArray(hosts) ? hosts.join(", ") : String(hosts ?? playbookId);
  const body = summary?.trim() || `Community playbook update for ${hostList}.`;
  const manifestSnippet = JSON.stringify(manifestRow, null, 2);

  return `# Contribute site playbook: \`${playbookId}\`

## Summary

${body}

## Files

| Export | Community target |
|--------|------------------|
| \`${exportPath}\` | \`${communityTarget}\` |

## Manifest update

Add or update this row in \`profiles/playbooks/community/manifest.json\`:

\`\`\`json
${manifestSnippet}
\`\`\`

Compute checksum: \`node scripts/lib/playbook-manifest-sha256.mjs --file <community-json>\`

Optional Ed25519 \`signature\` over \`{id, sha256, schema_version}\` — v1.1 hardening; \`signature: null\` stub in v1 export.

## Checklist

- [ ] \`node workers/shared/lib/playbook-publish-hygiene.selftest.mjs\` — green
- [ ] No \`Cookie\` / \`Authorization\` in \`request.headers\`
- [ ] Selectors scoped — not \`body\`, \`html\`, \`*\`, or \`[style*=\`
- [ ] Tested with \`occam_transcode(..., playbook_policy=auto)\` on ${hostList}
- [ ] \`dotnet run --project benchmarks/l0-gate\` — \`L4_GENOME_OK\` + K8
- [ ] Lessons contain no credentials or session tokens

## Notes

Occam MCP does **not** auto-upload playbooks. Copy the export into your fork and open a PR manually.
`;
}

export function publishPlaybook({
  inputPath,
  outputDir,
  ackCommunityReview,
  summary,
  occamHome,
}) {
  if (!ackCommunityReview) {
    return {
      ok: false,
      failureCode: "ack_required",
      message: "Set --ack-community-review to export a community PR package (no auto-upload).",
    };
  }

  let raw;
  try {
    raw = readInput(inputPath);
  } catch (err) {
    return {
      ok: false,
      failureCode: "invalid_input",
      message: err instanceof Error ? err.message : String(err),
    };
  }

  let doc;
  try {
    doc = JSON.parse(raw);
  } catch (err) {
    return {
      ok: false,
      failureCode: "invalid_local_playbook",
      message: err instanceof Error ? err.message : "Input is not valid JSON.",
    };
  }

  const playbookId = doc?.id;
  if (!playbookId || typeof playbookId !== "string") {
    return {
      ok: false,
      failureCode: "playbook_schema_invalid",
      message: "Playbook id is required.",
    };
  }

  const violations = findViolations(doc);
  if (violations.length > 0) {
    return {
      ok: false,
      failureCode: "secrets_detected",
      message: `Remove secrets before publish: ${violations.join(", ")}`,
      violations,
    };
  }

  const exportDoc = sanitizeForExport(doc);
  const exportRoot = outputDir ?? defaultOutputDir(occamHome, playbookId);
  const communityFileName = `${playbookId}.json`;
  const exportPath = join(exportRoot, communityFileName);
  const manifestRowPath = join(exportRoot, "manifest-row.json");
  const prPath = join(exportRoot, "PULL_REQUEST.md");
  const communityTarget = `profiles/playbooks/community/${communityFileName}`;

  const serialized = `${JSON.stringify(exportDoc, null, 2)}\n`;
  const manifestRow = buildManifestRow({
    id: playbookId,
    hosts: exportDoc.hosts ?? [playbookId],
    schemaVersion: exportDoc.schema_version ?? "1.0",
    fileName: communityFileName,
    sha256: sha256Hex(serialized),
  });

  mkdirRecursive(exportRoot);
  writeFile(exportPath, serialized);
  writeFile(manifestRowPath, `${JSON.stringify(manifestRow, null, 2)}\n`);
  writeFile(
    prPath,
    buildPullRequestMarkdown({
      playbookId,
      hosts: exportDoc.hosts,
      summary,
      exportPath,
      communityTarget,
      manifestRow,
    }),
  );

  return {
    ok: true,
    playbookId,
    exportPath,
    pullRequestTemplatePath: prPath,
    manifestRowPath,
    communityTargetPath: communityTarget,
    manifestRow,
    nextSteps: [
      `Review export: ${exportPath}`,
      `Copy to ${communityTarget} in your Occam MCP fork`,
      "Run: node workers/shared/lib/playbook-publish-hygiene.selftest.mjs",
      "Run: dotnet run --project benchmarks/l0-gate",
      "Open PR with PULL_REQUEST.md body (manual — no auto-upload)",
    ],
  };
}

function readInput(inputPath) {
  return readFileSync(inputPath, "utf8");
}

function writeFile(path, content) {
  writeFileSync(path, content, "utf8");
}

function mkdirRecursive(dir) {
  mkdirSync(dir, { recursive: true });
}

function defaultOutputDir(occamHome, playbookId) {
  const root = occamHome ?? process.env.OCCAM_HOME ?? process.cwd();
  return join(root, "artifacts", "playbook-publish", playbookId);
}
