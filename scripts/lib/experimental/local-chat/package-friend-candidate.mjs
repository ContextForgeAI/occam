#!/usr/bin/env node
/**
 * Package + install an experimental friend-test candidate that exposes `occam chat`.
 *
 * Package (developer machine on this branch):
 *   node scripts/lib/experimental/local-chat/package-friend-candidate.mjs
 *
 * Install (friend machine; Occam Level B home already present):
 *   node install-friend-candidate.mjs --home "$OCCAM_HOME" --write-launcher
 *   occam chat
 *
 * Not a stable 1.0 API. Does not publish public main / release tags.
 */
import { createHash } from "node:crypto";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
  chmodSync,
} from "node:fs";
import { tmpdir, homedir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { execFileSync } from "node:child_process";
import {
  renderUnixLauncher,
  renderWindowsCmdLauncher,
  renderWindowsPs1Launcher,
  resolveUserBinDir,
} from "../../operator/install-user-cli.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const repoRootFromHere = resolve(here, "../../../../");

/** Relative to OCCAM_HOME / overlay root. */
export const FRIEND_CHAT_FILES = Object.freeze([
  "scripts/occam-chat.mjs",
  "scripts/lib/mcp-stdio-client.mjs",
  "scripts/lib/experimental/local-chat/ollama-api.mjs",
  "scripts/lib/experimental/local-chat/tool-surface.mjs",
  "scripts/lib/experimental/local-chat/model-select.mjs",
  "scripts/lib/experimental/local-chat/session.mjs",
  "scripts/lib/experimental/local-chat/chat-loop.mjs",
  "scripts/lib/experimental/local-chat/ollama-endpoint.mjs",
  "scripts/lib/experimental/local-chat/package-friend-candidate.mjs",
  "scripts/lib/operator/occam-cli-subcommands.mjs",
  "scripts/lib/operator/occam-cli-dispatch.mjs",
  "scripts/lib/operator/occam-command-registry.mjs",
  "scripts/lib/operator/install-user-cli.mjs",
]);

/**
 * @param {string} [root]
 */
export function resolveGitSha(root = repoRootFromHere) {
  try {
    return execFileSync("git", ["rev-parse", "--short=12", "HEAD"], {
      cwd: root,
      encoding: "utf8",
    }).trim();
  } catch {
    return "unknown";
  }
}

/**
 * @param {string} startDir
 */
export function findOverlayRoot(startDir) {
  let dir = resolve(startDir);
  for (let i = 0; i < 8; i++) {
    if (existsSync(join(dir, "scripts", "occam-chat.mjs"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  if (existsSync(join(repoRootFromHere, "scripts", "occam-chat.mjs"))) return repoRootFromHere;
  throw new Error("cannot locate overlay root (scripts/occam-chat.mjs)");
}

/**
 * @param {string} sourceRoot
 * @param {string} destHome
 */
export function applyFriendChatOverlay(sourceRoot, destHome) {
  const src = resolve(sourceRoot);
  const dest = resolve(destHome);
  if (!existsSync(join(dest, "scripts", "occam.mjs"))) {
    throw new Error(`destination is not an Occam home (missing scripts/occam.mjs): ${dest}`);
  }
  for (const rel of FRIEND_CHAT_FILES) {
    const from = join(src, rel);
    if (!existsSync(from)) throw new Error(`missing overlay file: ${rel}`);
    const to = join(dest, rel);
    mkdirSync(dirname(to), { recursive: true });
    copyFileSync(from, to);
  }
  writeFileSync(
    join(dest, "FRIEND_CHAT_CANDIDATE.json"),
    `${JSON.stringify(
      {
        kind: "occam-chat-friend-candidate",
        experimental: true,
        note: "Experimental local chat (occam chat). Not a stable 1.0 API.",
        appliedAt: new Date().toISOString(),
      },
      null,
      2,
    )}\n`,
  );
}

/**
 * @param {string} occamHome
 * @param {{ binDir?: string, nodeBin?: string, platform?: NodeJS.Platform }} [opts]
 */
export function writeFriendOccamLauncher(occamHome, opts = {}) {
  const binDir = opts.binDir || resolveUserBinDir();
  const nodeBin = opts.nodeBin || process.execPath;
  const platform = opts.platform || process.platform;
  mkdirSync(binDir, { recursive: true });
  if (platform === "win32") {
    const cmdPath = join(binDir, "occam.cmd");
    writeFileSync(cmdPath, renderWindowsCmdLauncher(occamHome, nodeBin));
    writeFileSync(join(binDir, "occam.ps1"), renderWindowsPs1Launcher(occamHome, nodeBin));
    return { launcherPath: cmdPath, binDir };
  }
  const shPath = join(binDir, "occam");
  writeFileSync(shPath, renderUnixLauncher(occamHome, nodeBin));
  try {
    chmodSync(shPath, 0o755);
  } catch {
    /* ignore */
  }
  return { launcherPath: shPath, binDir };
}

/**
 * @param {{ repoRoot?: string, outputDir?: string }} [opts]
 */
export function packageFriendCandidateOverlay(opts = {}) {
  const repoRoot = resolve(opts.repoRoot || repoRootFromHere);
  const sha = resolveGitSha(repoRoot);
  const outDir = resolve(opts.outputDir || join(repoRoot, "artifacts", "friend-candidates"));
  mkdirSync(outDir, { recursive: true });
  const stage = mkdtempSync(join(tmpdir(), "occam-friend-overlay-"));
  try {
    for (const rel of FRIEND_CHAT_FILES) {
      const from = join(repoRoot, rel);
      if (!existsSync(from)) throw new Error(`cannot package missing file: ${rel}`);
      const to = join(stage, rel);
      mkdirSync(dirname(to), { recursive: true });
      copyFileSync(from, to);
    }
    writeFileSync(
      join(stage, "install-friend-candidate.mjs"),
      `#!/usr/bin/env node
import { runInstallCli } from "./scripts/lib/experimental/local-chat/package-friend-candidate.mjs";
process.exit(await runInstallCli(process.argv.slice(2)));
`,
    );
    writeFileSync(
      join(stage, "FRIEND_CHAT_OVERLAY.json"),
      `${JSON.stringify(
        {
          kind: "occam-chat-friend-overlay",
          experimental: true,
          gitSha: sha,
          createdAt: new Date().toISOString(),
          files: FRIEND_CHAT_FILES,
        },
        null,
        2,
      )}\n`,
    );
    writeFileSync(
      join(stage, "FRIEND_INSTALL.txt"),
      `Occam experimental local chat — friend candidate overlay
Git: ${sha}

NOT a stable 1.0 release.

1. Keep Ollama running with a tool-capable model.
2. Extract this archive, then:
     node install-friend-candidate.mjs --home "$OCCAM_HOME" --write-launcher
   Typical OCCAM_HOME: ~/.local/share/ff-occam
3. Run:
     occam chat

No Git / MCP config / Python / SSH. Local Ollama API + Occam tools only.
`,
    );

    const name = `occam-chat-friend-${sha}-overlay.tar.gz`;
    const tarball = join(outDir, name);
    if (existsSync(tarball)) rmSync(tarball);
    execFileSync("tar", ["-czf", tarball, "-C", stage, "."], { stdio: "inherit" });
    const sha256 = createHash("sha256").update(readFileSync(tarball)).digest("hex");
    writeFileSync(join(outDir, `${name}.sha256`), `${sha256}  ${name}\n`);
    copyFileSync(join(stage, "FRIEND_INSTALL.txt"), join(outDir, "FRIEND_INSTALL.txt"));
    return { tarball, sha256, sha, outDir, name };
  } finally {
    rmSync(stage, { recursive: true, force: true });
  }
}

/**
 * @param {string[]} argv
 */
export async function runInstallCli(argv) {
  /** @type {{ home: string|null, writeLauncher: boolean, help: boolean }} */
  const opts = { home: null, writeLauncher: false, help: false };
  const args = [...argv];
  while (args.length) {
    const a = args.shift();
    if (a === "--home") opts.home = args.shift() ?? null;
    else if (a?.startsWith("--home=")) opts.home = a.slice("--home=".length);
    else if (a === "--write-launcher") opts.writeLauncher = true;
    else if (a === "-h" || a === "--help") opts.help = true;
    else throw new Error(`Unknown argument: ${a}`);
  }
  if (opts.help) {
    console.log("Usage: node install-friend-candidate.mjs --home <OCCAM_HOME> [--write-launcher]");
    return 0;
  }
  const home = resolve(
    opts.home || process.env.OCCAM_HOME || join(homedir(), ".local", "share", "ff-occam"),
  );
  const overlayRoot = findOverlayRoot(process.cwd());
  applyFriendChatOverlay(overlayRoot, home);
  console.error(`Applied experimental occam chat overlay → ${home}`);
  if (opts.writeLauncher) {
    const written = writeFriendOccamLauncher(home);
    console.error(`Wrote launcher: ${written.launcherPath}`);
    console.error(`Ensure PATH includes: ${written.binDir}`);
  }
  console.error("Experimental local chat ready. Run: occam chat");
  return 0;
}

const isMain =
  Boolean(process.argv[1]) && import.meta.url === pathToFileURL(resolve(process.argv[1])).href;

if (isMain) {
  packageFriendCandidateOverlay();
}
