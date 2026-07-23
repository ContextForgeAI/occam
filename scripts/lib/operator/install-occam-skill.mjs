/**
 * Install FF-Occam skill to harness-specific directories.
 * Source of truth: $OCCAM_HOME/skills/occam (or package @ff-occam/skill).
 */
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** @typedef {'cursor'|'claude'|'hermes'|'copilot'|'kiro'|'pi'|'devin'|'codex'|'generic'|'all'} Platform */

export const SKILL_PLATFORMS = [
  "cursor",
  "claude",
  "hermes",
  "copilot",
  "kiro",
  "pi",
  "devin",
  "codex",
  "generic",
  "all",
];

const AGENTS_SECTION_MARKER_START = "<!-- occam-skill:start -->";
const AGENTS_SECTION_MARKER_END = "<!-- occam-skill:end -->";

/**
 * @param {string} [occamHome]
 */
export function resolveSkillSource(occamHome) {
  const candidates = [];

  if (occamHome?.trim()) {
    candidates.push(path.join(occamHome.trim(), "skills", "occam"));
  }

  const here = path.dirname(fileURLToPath(import.meta.url));
  candidates.push(path.resolve(here, "../../..", "skills", "occam"));

  try {
    const pkgRoot = path.resolve(here, "../../../packages/occam-skill/skill");
    candidates.push(pkgRoot);
  } catch {
    // ignore
  }

  for (const dir of candidates) {
    if (fs.existsSync(path.join(dir, "SKILL.md"))) {
      return dir;
    }
  }

  return null;
}

/**
 * @param {string} src
 * @param {string} dest
 */
function copyTree(src, dest) {
  fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyTree(srcPath, destPath);
    } else if (entry.isFile()) {
      fs.copyFileSync(srcPath, destPath);
    }
  }
}

/**
 * @param {{ platform: Platform; scope: 'global'|'project'; projectRoot?: string; target?: string }} opts
 * @returns {{ dest: string; platform: string }[]}
 */
export function resolveInstallDestinations(opts) {
  const home = os.homedir();
  const projectRoot = opts.projectRoot ?? process.cwd();
  const platforms =
    opts.platform === "all"
      ? ["cursor", "claude", "hermes", "copilot", "kiro", "pi", "devin"]
      : [opts.platform];

  /** @type {{ dest: string; platform: string }[]} */
  const out = [];

  if (opts.target?.trim()) {
    out.push({ dest: path.resolve(opts.target.trim()), platform: "generic" });
    return out;
  }

  for (const platform of platforms) {
    switch (platform) {
      case "cursor":
        out.push({
          dest:
            opts.scope === "project"
              ? path.join(projectRoot, ".cursor", "skills", "occam")
              : path.join(home, ".cursor", "skills", "occam"),
          platform,
        });
        break;
      case "claude":
        out.push({
          dest:
            opts.scope === "project"
              ? path.join(projectRoot, ".claude", "skills", "occam")
              : path.join(home, ".claude", "skills", "occam"),
          platform,
        });
        break;
      case "hermes":
        out.push({
          dest: path.join(home, ".hermes", "skills", "occam"),
          platform,
        });
        break;
      case "copilot":
        out.push({
          dest: path.join(home, ".copilot", "skills", "occam"),
          platform,
        });
        break;
      case "kiro":
        out.push({
          dest: path.join(projectRoot, ".kiro", "skills", "occam"),
          platform,
        });
        break;
      case "pi":
        out.push({
          dest: path.join(home, ".pi", "agent", "skills", "occam"),
          platform,
        });
        break;
      case "devin":
        out.push({
          dest: path.join(home, ".config", "devin", "skills", "occam"),
          platform,
        });
        break;
      case "codex":
        out.push({
          dest: path.join(projectRoot, ".agents", "skills", "occam"),
          platform,
        });
        break;
      default:
        break;
    }
  }

  return out;
}

/**
 * @param {string} projectRoot
 * @param {{ dryRun?: boolean }} [opts]
 */
export function writeAgentsMdSection(projectRoot, opts = {}) {
  const agentsPath = path.join(projectRoot, "AGENTS.md");
  const block = [
    AGENTS_SECTION_MARKER_START,
    "",
    "## FF-Occam skill",
    "",
    "For live web extraction, activate the **occam** skill (`.agents/skills/occam/SKILL.md` or harness skills dir).",
    "Wire MCP first: `occam doctor`, `occam smoke`, then call `occam_*` tools. On `ok: false`, content is unknown.",
    "",
    AGENTS_SECTION_MARKER_END,
    "",
  ].join("\n");

  if (opts.dryRun) {
    return { agentsPath, action: "would-write-section" };
  }

  let text = "";
  if (fs.existsSync(agentsPath)) {
    text = fs.readFileSync(agentsPath, "utf8");
    if (text.includes(AGENTS_SECTION_MARKER_START)) {
      const start = text.indexOf(AGENTS_SECTION_MARKER_START);
      const end = text.indexOf(AGENTS_SECTION_MARKER_END);
      if (end !== -1) {
        text =
          text.slice(0, start) +
          block +
          text.slice(end + AGENTS_SECTION_MARKER_END.length).replace(/^\n*/, "\n");
      }
    } else {
      text = `${text.trimEnd()}\n\n${block}`;
    }
  } else {
    text = `# AGENTS\n\n${block}`;
  }

  fs.mkdirSync(path.dirname(agentsPath), { recursive: true });
  fs.writeFileSync(agentsPath, text, "utf8");
  return { agentsPath, action: "wrote-section" };
}

/**
 * @param {{
 *   occamHome?: string,
 *   platform?: Platform,
 *   scope?: 'global'|'project',
 *   projectRoot?: string,
 *   target?: string,
 *   dryRun?: boolean,
 *   includeCodexAgents?: boolean,
 * }} opts
 */
export function installOccamSkill(opts = {}) {
  const platform = opts.platform ?? "all";
  const scope = opts.scope ?? "global";
  const source = resolveSkillSource(opts.occamHome);

  if (!source) {
    return {
      ok: false,
      error: "skill source not found — run from FF-Occam install or install @ff-occam/skill",
    };
  }

  const version = fs.existsSync(path.join(source, ".occam_skill_version"))
    ? fs.readFileSync(path.join(source, ".occam_skill_version"), "utf8").trim()
    : "unknown";

  const destinations = resolveInstallDestinations({
    platform,
    scope,
    projectRoot: opts.projectRoot,
    target: opts.target,
  });

  if (destinations.length === 0) {
    return { ok: false, error: `no destinations for platform: ${platform}` };
  }

  /** @type {{ platform: string; dest: string; action: string }[]} */
  const installed = [];

  for (const { dest, platform: p } of destinations) {
    if (opts.dryRun) {
      installed.push({ platform: p, dest, action: "would-copy" });
      continue;
    }
    if (fs.existsSync(dest)) {
      fs.rmSync(dest, { recursive: true, force: true });
    }
    copyTree(source, dest);
    installed.push({ platform: p, dest, action: "copied" });
  }

  let agents = null;
  if (
    !opts.dryRun &&
    (platform === "codex" || platform === "all") &&
    scope === "project"
  ) {
    agents = writeAgentsMdSection(opts.projectRoot ?? process.cwd());
  }

  return {
    ok: true,
    version,
    source,
    installed,
    agents,
  };
}
