#!/usr/bin/env node
/**
 * Guard: FFOccamMcp.Core must stay on net10.0 (Native AOT). Agents must not downgrade to net8.0.
 */
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const root = resolve(process.argv[2] ?? process.env.OCCAM_HOME ?? join(import.meta.dirname, "../.."));
const csproj = join(root, "src", "FFOccamMcp.Core", "FFOccamMcp.Core.csproj");

let text;
try {
  text = readFileSync(csproj, "utf8");
} catch {
  // Level B / prebuilt-tarball install has no src/ — there is nothing to guard
  // against a net8 downgrade, so this is not an error. (The guard only matters
  // in a git-clone build context where the csproj actually exists.)
  if (process.argv.includes("--verbose") || process.env.OCCAM_LOG === "1") {
    console.error(`note: no csproj (prebuilt install) — net10 guard skipped: ${csproj}`);
  }
  process.exit(0);
}

if (!/<TargetFramework>\s*net10\.0\s*<\/TargetFramework>/.test(text)) {
  console.error("error: FFOccamMcp.Core.csproj must target net10.0 (Native AOT).");
  console.error("Do not install .NET 8 or change TargetFramework to net8.0.");
  console.error("Revert local edits: git checkout -- src/FFOccamMcp.Core/FFOccamMcp.Core.csproj");
  console.error("See INSTALL.md");
  process.exit(1);
}

if (process.argv.includes("--verbose") || process.env.OCCAM_LOG === "1") {
  console.error(`ok: ${csproj} targets net10.0`);
}
