import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { findSubcommand } from "./occam-cli-subcommands.mjs";

/**
 * @param {string} occamHome
 */
export function isLevelBInstall(occamHome) {
  const hasVersion = existsSync(join(occamHome, "VERSION"));
  const hasGit = existsSync(join(occamHome, ".git"));
  return hasVersion && !hasGit;
}

/**
 * @param {string} occamHome
 * @param {string} relativePath under scripts/
 */
export function resolveScriptPath(occamHome, relativePath) {
  return join(occamHome, "scripts", relativePath);
}

/**
 * Stage scripts outside OCCAM_HOME so Windows can delete the install tree
 * while uninstall/disconnect modules are loaded.
 * @param {string} occamHome
 * @param {string} scriptRelative under scripts/
 * @returns {{ scriptPath: string, cleanup: () => void }}
 */
export function stageRemovalScript(occamHome, scriptRelative) {
  const stagingRoot = mkdtempSync(join(tmpdir(), "occam-removal-"));
  const scriptsSrc = join(occamHome, "scripts");
  const scriptsDst = join(stagingRoot, "scripts");
  cpSync(scriptsSrc, scriptsDst, { recursive: true });
  return {
    scriptPath: join(scriptsDst, scriptRelative),
    cleanup: () => {
      try {
        rmSync(stagingRoot, { recursive: true, force: true, maxRetries: 5, retryDelay: 50 });
      } catch {
        /* best-effort */
      }
    },
  };
}

/**
 * @param {import("./occam-cli-subcommands.mjs").CliSubcommand} sub
 * @param {string} occamHome
 * @param {string[]} passthroughArgs
 */
export function dispatchSubcommand(sub, occamHome, passthroughArgs = []) {
  const env = { ...process.env, OCCAM_HOME: occamHome };

  if (sub.delegate === "node") {
    let scriptPath = resolveScriptPath(occamHome, sub.script ?? "");
    if (!existsSync(scriptPath)) {
      console.error(`error: missing ${scriptPath}`);
      return 1;
    }

    let cleanup = () => {};
    if (sub.name === "uninstall" || sub.name === "disconnect") {
      const staged = stageRemovalScript(occamHome, sub.script ?? "");
      scriptPath = staged.scriptPath;
      cleanup = staged.cleanup;
      if (!existsSync(scriptPath)) {
        cleanup();
        console.error(`error: missing staged ${scriptPath}`);
        return 1;
      }
    }

    const args = [scriptPath];
    if (sub.name === "snippet" && passthroughArgs.length === 0) {
      args.push(occamHome);
    } else if (sub.passthrough) {
      args.push(...passthroughArgs);
    }

    // Chat is normally `exec`'d by the user launcher. If reached via
    // `node occam.mjs chat`, spawnSync is fine for non-interactive --once/--help.
    // Uninstall/disconnect must not use OCCAM_HOME as cwd: on Windows the
    // process cwd keeps a handle and recursive rm fails with EPERM/EBUSY.
    const cwd =
      sub.name === "uninstall" || sub.name === "disconnect" ? tmpdir() : occamHome;

    try {
      const result = spawnSync(process.execPath, args, {
        cwd,
        env,
        stdio: "inherit",
      });
      return result.status ?? 1;
    } finally {
      cleanup();
    }
  }

  if (sub.delegate === "shell") {
    if (process.platform === "win32") {
      const ps1 = resolveScriptPath(occamHome, `${sub.script}.ps1`);
      if (!existsSync(ps1)) {
        console.error(`error: missing ${ps1}`);
        return 1;
      }
      const doctorArgs = [...passthroughArgs];
      if (sub.name === "doctor" && isLevelBInstall(occamHome) && !doctorArgs.includes("--skip-build") && !doctorArgs.includes("-SkipBuild")) {
        doctorArgs.push("--skip-build");
      }
      const psArgs =
        sub.name === "doctor"
          ? doctorArgs.map((a) => {
              if (a === "--skip-build") return "-SkipBuild";
              if (a === "--quiet") return "-Quiet";
              if (a === "--verbose" || a === "-v") return "-VerboseDoctor";
              return a;
            })
          : doctorArgs;
      const result = spawnSync(
        "powershell",
        ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps1, ...psArgs],
        { cwd: occamHome, env, stdio: "inherit" },
      );
      return result.status ?? 1;
    }

    const sh = resolveScriptPath(occamHome, `${sub.script}.sh`);
    if (!existsSync(sh)) {
      console.error(`error: missing ${sh}`);
      return 1;
    }
    const doctorArgs = [...passthroughArgs];
    if (sub.name === "doctor" && isLevelBInstall(occamHome) && !doctorArgs.includes("--skip-build")) {
      doctorArgs.push("--skip-build");
    }
    const result = spawnSync("bash", [sh, ...doctorArgs], {
      cwd: occamHome,
      env,
      stdio: "inherit",
    });
    return result.status ?? 1;
  }

  console.error(`error: unsupported delegate ${sub.delegate}`);
  return 1;
}

/**
 * @param {string} name
 * @param {string} occamHome
 * @param {string[]} args
 */
export function runSubcommandByName(name, occamHome, args = []) {
  const sub = findSubcommand(name);
  if (!sub) {
    console.error(`error: unknown command '${name}'`);
    return 1;
  }

  if (sub.delegate === "internal") {
    return 0;
  }

  return dispatchSubcommand(sub, occamHome, args);
}
