import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
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
 * @param {import("./occam-cli-subcommands.mjs").CliSubcommand} sub
 * @param {string} occamHome
 * @param {string[]} passthroughArgs
 */
export function dispatchSubcommand(sub, occamHome, passthroughArgs = []) {
  const env = { ...process.env, OCCAM_HOME: occamHome };

  if (sub.delegate === "node") {
    const scriptPath = resolveScriptPath(occamHome, sub.script ?? "");
    if (!existsSync(scriptPath)) {
      console.error(`error: missing ${scriptPath}`);
      return 1;
    }

    const args = [scriptPath];
    if (sub.name === "snippet" && passthroughArgs.length === 0) {
      args.push(occamHome);
    } else if (sub.passthrough) {
      args.push(...passthroughArgs);
    }

    const result = spawnSync(process.execPath, args, {
      cwd: occamHome,
      env,
      stdio: "inherit",
    });
    return result.status ?? 1;
  }

  if (sub.delegate === "shell") {
    if (process.platform === "win32") {
      const ps1 = resolveScriptPath(occamHome, `${sub.script}.ps1`);
      if (!existsSync(ps1)) {
        console.error(`error: missing ${ps1}`);
        return 1;
      }
      const doctorArgs = [...passthroughArgs];
      if (sub.name === "doctor" && isLevelBInstall(occamHome) && !doctorArgs.includes("--skip-build")) {
        doctorArgs.push("--skip-build");
      }
      const result = spawnSync(
        "powershell",
        ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps1, ...doctorArgs],
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
