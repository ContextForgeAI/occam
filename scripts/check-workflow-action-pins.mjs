#!/usr/bin/env node
/**
 * Fail closed when a GitHub Actions workflow executes a third-party action by
 * a mutable tag or branch. Local actions are intentionally outside this
 * policy. Container actions must use an immutable sha256 image digest.
 */
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");
const workflowsDir = join(repoRoot, ".github", "workflows");
const shaPattern = /^[0-9a-f]{40}$/i;
const dockerDigestPattern = /^docker:\/\/[^@\s]+@sha256:[0-9a-f]{64}$/i;
const versionCommentPattern = /#\s*v\d+(?:\.\d+){0,2}(?:[-+][0-9A-Za-z.-]+)?\s*$/;

function stripYamlQuotes(value) {
  if (
    value.length >= 2 &&
    ((value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'")))
  ) {
    return value.slice(1, -1);
  }
  return value;
}

export function validateUsesLine(line, file = "workflow.yml", lineNumber = 1) {
  const match = line.match(/^\s*(?:-\s*)?(?:"uses"|'uses'|uses)\s*:\s*(.*?)\s*$/);
  if (!match) {
    if (/^\s*-\s*\{[^#]*(?:"uses"|'uses'|\buses)\s*:/.test(line)) {
      return {
        failures: [
          `${file}:${lineNumber}: inline uses syntax is not auditable; use a block-style uses entry`,
        ],
        externalActions: 1,
      };
    }
    return { failures: [], externalActions: 0 };
  }

  const raw = match[1].trim();
  const commentAt = raw.search(/\s+#/);
  const value = stripYamlQuotes((commentAt >= 0 ? raw.slice(0, commentAt) : raw).trim());
  const comment = commentAt >= 0 ? raw.slice(commentAt).trim() : "";

  if (value.startsWith("./")) return { failures: [], externalActions: 0 };

  if (value.startsWith("docker://")) {
    return dockerDigestPattern.test(value)
      ? { failures: [], externalActions: 1 }
      : {
          failures: [
            `${file}:${lineNumber}: container action must use an immutable sha256 digest: ${value}`,
          ],
          externalActions: 1,
        };
  }

  const failures = [];
  const at = value.lastIndexOf("@");
  const action = at > 0 ? value.slice(0, at) : value;
  const ref = at > 0 ? value.slice(at + 1) : "";
  if (!action.includes("/") || !shaPattern.test(ref)) {
    failures.push(
      `${file}:${lineNumber}: external action must use a 40-hex commit SHA: ${value}`,
    );
  } else if (!versionCommentPattern.test(comment)) {
    failures.push(
      `${file}:${lineNumber}: pinned action must keep an auditable version comment (for example # v4.4.0): ${value}`,
    );
  }
  return { failures, externalActions: 1 };
}

export function validateWorkflow(file, directory = workflowsDir) {
  const failures = [];
  let externalActions = 0;
  const lines = readFileSync(join(directory, file), "utf8").split(/\r?\n/);

  for (let index = 0; index < lines.length; index += 1) {
    const result = validateUsesLine(lines[index], file, index + 1);
    failures.push(...result.failures);
    externalActions += result.externalActions;
  }

  return { failures, externalActions };
}

export function auditWorkflows(directory = workflowsDir) {
  const workflowFiles = readdirSync(directory)
    .filter((file) => /\.ya?ml$/i.test(file))
    .sort();
  const failures = [];
  let externalActions = 0;

  if (workflowFiles.length === 0) {
    failures.push("no workflow YAML files found");
  }
  for (const file of workflowFiles) {
    const result = validateWorkflow(file, directory);
    failures.push(...result.failures);
    externalActions += result.externalActions;
  }
  if (externalActions === 0) {
    failures.push("no external actions found; policy assertion did not inspect any actionable uses entry");
  }
  return { failures, externalActions, workflowFiles };
}

const isDirect = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isDirect) {
  const result = auditWorkflows();
  if (result.failures.length > 0) {
    console.error("WORKFLOW_ACTION_PINS_FAILED");
    for (const failure of result.failures) console.error(`- ${failure}`);
    process.exit(1);
  }
  console.log(
    `WORKFLOW_ACTION_PINS_OK workflows=${result.workflowFiles.length} external_actions=${result.externalActions}`,
  );
}
