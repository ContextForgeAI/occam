#!/usr/bin/env node
/**
 * Single source of truth for the auto-provision gate (B6).
 *
 * "Will the browser worker install chromium itself?" is decided HERE, by the same two predicates the real
 * gate in browser-session.mjs uses (`autoInstallEnabled() && !usesSystemBrowser()`). The C# host used to
 * re-implement this rule in C# and keep it in sync by hand; it now spawns this probe instead
 * (FeatureDiscoveryService.WillAutoProvisionBrowser), so the rule exists in exactly one place — the language
 * that executes it.
 *
 * Consumer: src/FFOccamMcp.Core/Services/FeatureDiscoveryService.cs. It only asks when no browser is installed
 * (the rare cold path) and caches the answer for the process lifetime — every input is process-level env, which
 * cannot change mid-run. If this probe's stdout contract changes, that C# reader must change with it.
 *
 * Deliberately pure logic: it imports no playwright and launches nothing, because it runs exactly in the case
 * where launching fails (no browser binary). That is why verify-browser-launch.mjs cannot serve this purpose.
 *
 * Contract: prints ONE JSON line to stdout — {"will_provision": boolean} — and exits 0.
 */
import path from "node:path";
import { fileURLToPath } from "node:url";
import { autoInstallEnabled } from "./browser-provision.mjs";
import { usesSystemBrowser } from "./browser-launch-options.mjs";

/** The worker's own answer, in the same terms as the gate in browser-session.mjs. */
export function willAutoProvision() {
  return autoInstallEnabled() && !usesSystemBrowser();
}

const isMain =
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isMain) {
  console.log(JSON.stringify({ will_provision: willAutoProvision() }));
}
