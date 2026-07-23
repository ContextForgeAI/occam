/**
 * @ff-occam/skill — portable installer (published npm copy).
 */
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

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

export function resolveSkillSource(occamHome) {
  const bundled = path.join(__dirname, "..", "skill");
  if (fs.existsSync(path.join(bundled, "SKILL.md"))) {
    return bundled;
  }

  if (occamHome?.trim()) {
    const homeSkill = path.join(occamHome.trim(), "skills", "occam");
    if (fs.existsSync(path.join(homeSkill, "SKILL.md"))) {
      return homeSkill;
    }
  }

  return null;
}

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

export function resolveInstallDestinations(opts) {
  const home = os.homedir();
  const projectRoot = opts.projectRoot ?? process.cwd();
  const platforms =
    opts.platform === "all"
      ? ["cursor", "claude", "hermes", "copilot", "kiro", "pi", "devin"]
      : [opts.platform];

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
        out.push({ dest: path.join(home, ".hermes", "skills", "occam"), platform });
        break;
      case "copilot":
        out.push({ dest: path.join(home, ".copilot", "skills", "occam"), platform });
        break;
      case "kiro":
        out.push({ dest: path.join(projectRoot, ".kiro", "skills", "occam"), platform });
        break;
      case "pi":
        out.push({ dest: path.join(home, ".pi", "agent", "skills", "occam"), platform });
        break;
      case "devin":
        out.push({ dest: path.join(home, ".config", "devin", "skills", "occam"), platform });
        break;
      case "codex":
        out.push({ dest: path.join(projectRoot, ".agents", "skills", "occam"), platform });
        break;
      default:
        break;
    }
  }

  return out;
}

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

export function installOccamSkill(opts = {}) {
  const platform = opts.platform ?? "all";
  const scope = opts.scope ?? "global";
  const source = resolveSkillSource(opts.occamHome);

  if (!source) {
    return {
      ok: false,
      error: "skill source not found — reinstall @ff-occam/skill",
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

  return { ok: true, version, source, installed, agents };
}
