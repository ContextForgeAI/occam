#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildMcpSnippetConfig, runFlow } from "./lib/operator/onboard-flow.mjs";
import { writeOnboardConfig } from "./lib/operator/onboard-config.mjs";
import { collectInteractiveAnswers, PROFILE_IDS } from "./lib/operator/onboard-steps.mjs";
import { selectRenderer } from "./lib/operator/render/select-renderer.mjs";
import { writeHostConfig } from "./lib/operator/write-mcp-config.mjs";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const defaultHome = process.env.OCCAM_HOME?.trim() || join(scriptDir, "..");

const HOST_TARGETS = ["cursor", "hermes", "openclaw", "claude-desktop", "generic-stdio", "cli-only"];

function parseArgs(argv) {
  const args = [...argv];
  let format = "tty";
  let skip = false;
  let nonInteractive = false;
  let profile = "default";
  let skipDoctor = false;
  let skipWelcome = false;
  let writeConfig = false;
  let forceWrite = false;
  let hostTarget = "";

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === "--json") {
      format = "json";
    } else if (arg === "--plain") {
      format = "plain";
    } else if (arg === "--skip") {
      skip = true;
    } else if (arg === "--skip-welcome") {
      skipWelcome = true;
    } else if (arg === "--non-interactive") {
      nonInteractive = true;
    } else if (arg === "--skip-doctor") {
      skipDoctor = true;
    } else if (arg === "--write-config") {
      writeConfig = true;
    } else if (arg === "--force") {
      forceWrite = true;
    } else if (arg === "--profile") {
      profile = args[++i]?.trim() || "default";
    } else if (arg === "--host-target") {
      hostTarget = args[++i]?.trim() || "";
    } else if (arg === "-h" || arg === "--help") {
      printUsage();
      process.exit(0);
    }
  }

  return { format, skip, nonInteractive, profile, skipDoctor, skipWelcome, writeConfig, forceWrite, hostTarget };
}

function printUsage() {
  console.log(`usage: node scripts/occam-onboard.mjs [options]

Options:
  --skip                 Exit 0 without writing config
  --non-interactive      Use flags only (no TTY prompts)
  --profile NAME         default | hermes-headless | mass-scrape
  --host-target NAME     cursor | hermes | openclaw | claude-desktop | generic-stdio | cli-only
  --json | --plain       Output format
  --skip-doctor          Skip doctor/smoke verify step
  --skip-welcome         Skip wizard welcome (e.g. after get-ff-occam product banner)
  --write-config         Merge ff-occam into ~/.cursor/mcp.json (TTY confirm; --force to skip)
  --force                With --write-config, skip YES prompt
  -h, --help             Show this help
`);
}

function resolveDefaultHostTarget(profile, explicit) {
  if (explicit && HOST_TARGETS.includes(explicit)) {
    return explicit;
  }
  if (process.env.OCCAM_HOST === "hermes") {
    return "hermes";
  }
  if (process.env.OCCAM_HOST === "cursor") {
    return "cursor";
  }
  if (profile === "hermes-headless") {
    return "hermes";
  }
  return "cursor";
}

function runVerify(home, skipDoctor) {
  if (skipDoctor || process.env.CI === "1" || process.env.CI === "true") {
    return { ok: true, skipped: true };
  }

  const doctorSh = join(home, "scripts", "occam-doctor.sh");
  if (existsSync(doctorSh)) {
    const doctor = spawnSync("bash", [doctorSh, "--skip-build"], {
      cwd: home,
      env: { ...process.env, OCCAM_HOME: home },
      stdio: "inherit",
    });
    if (doctor.status !== 0) {
      return { ok: false, step: "doctor" };
    }
  }

  const smoke = spawnSync("node", [join(home, "scripts", "hermes-smoke.mjs")], {
    cwd: home,
    env: { ...process.env, OCCAM_HOME: home, WT_OCCAM_BANNER: "0", OCCAM_BANNER: "0" },
    stdio: "inherit",
  });
  if (smoke.status !== 0) {
    return { ok: false, step: "hermes-smoke" };
  }

  return { ok: true, skipped: false };
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));

  if (opts.skip) {
    if (opts.format === "json") {
      console.log(JSON.stringify({ skipped: true }, null, 2));
    } else {
      console.log("Onboard skipped — no config written.");
    }
    process.exit(0);
  }

  /** @type {Record<string, string>} */
  let answers;

  if (opts.nonInteractive) {
    if (!PROFILE_IDS.includes(opts.profile)) {
      console.error(`invalid profile: ${opts.profile}`);
      process.exit(2);
    }

    const ht = resolveDefaultHostTarget(opts.profile, opts.hostTarget);
    answers = {
      occamHome: defaultHome,
      hostTarget: ht,
      browser: "bundled",
      proxy: "no",
      profile: opts.profile,
    };
  } else if (!process.stdin.isTTY) {
    console.error("No TTY — use --non-interactive or --skip");
    process.exit(2);
  } else {
    const ht = resolveDefaultHostTarget(
      process.env.OCCAM_HOST === "hermes" ? "hermes-headless" : "default",
      opts.hostTarget || undefined,
    );
    answers = await collectInteractiveAnswers(
      {
        occamHome: defaultHome,
        hostTarget: ht,
        browser: "bundled",
        proxy: "no",
        profile: process.env.OCCAM_HOST === "hermes" ? "hermes-headless" : "default",
      },
      { skipWelcome: opts.skipWelcome },
    );
  }

  const result = runFlow(answers);
  const mcpConfig = buildMcpSnippetConfig(result);
  writeOnboardConfig(result);

  const verify = runVerify(result.occamHome, opts.skipDoctor);
  const { renderOnboard } = selectRenderer(opts.format);

  if (!opts.nonInteractive && opts.format === "tty" && process.stdin.isTTY) {
    console.log("\nRunning verify (doctor + hermes-smoke)…\n");
  }

  console.log(renderOnboard({ ...result, verify }, mcpConfig));

  if (opts.writeConfig) {
    await writeHostConfig(result, { force: opts.forceWrite });
  }

  if (!verify.ok) {
    console.error(`verify failed: ${verify.step}`);
    process.exit(1);
  }
}

main().catch((err) => {
  console.error(err.message || String(err));
  process.exit(1);
});
