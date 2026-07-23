import { runBrowserExtract } from "./lib/browser-extract-run.mjs";
import { classifyBrowserLaunchError } from "./lib/browser-launch-options.mjs";
import { installSilentExitGuard } from "../shared/lib/worker-exit-guard.mjs";

// Same top-level-await shape as the http worker: a promise that never settles would exit 13 silently.
// The catch below only covers rejections — this covers "never settles at all".
const guard = installSilentExitGuard("browser_playwright");

const args = process.argv.slice(2);
const consentAggressive = args.includes("--consent-aggressive");
const leanAssets = args.includes("--lean-assets");
const planFileArg = args.find((a) => a.startsWith("--browser-plan-file="));
const headersFileArg = args.find((a) => a.startsWith("--headers-file="));
const storageStateFileArg = args.find((a) => a.startsWith("--storage-state-file="));
const headersFile = headersFileArg?.slice("--headers-file=".length).replace(/^"|"$/g, "") ?? null;
const storageStateFile = storageStateFileArg?.slice("--storage-state-file=".length).replace(/^"|"$/g, "") ?? null;
const variantArg = args.find((a) => a.startsWith("--extract-variant="));
const url = args.find((a) => !a.startsWith("-"));

if (!url) {
  guard.disarm(); // a usage error is reported on stderr + exit 1, not as a JSON verdict
  console.error(
    "Usage: node browser-extract.mjs <url> [--consent-aggressive] [--lean-assets] [--extract-variant=css-hide]",
  );
  process.exit(1);
}

if (args.includes("--cookie-inject")) {
  process.env.WT_COOKIE_INJECT = "1";
}

function failureFromError(error) {
  const message = error?.message ?? String(error);
  // A browser-availability failure carries an actionable fix (install / install-deps) + which layer.
  // Failure code stays `playwright_missing` (already wired into C# routing + the gate) — the `fix`
  // and `reason` are additive; the C# side can surface them into a clearer response next.
  const provision = classifyBrowserLaunchError(error);
  if (provision) {
    return {
      ok: false,
      backend: "browser_playwright",
      failure: "playwright_missing",
      reason: provision.reason,
      fix: provision.fix,
      message,
      latency_ms: 0,
    };
  }
  return {
    ok: false,
    backend: "browser_playwright",
    failure: error?.name ?? "error",
    message,
    latency_ms: 0,
  };
}

try {
  const result = await runBrowserExtract(url, {
    leanAssets,
    consentAggressive,
    headersFile,
    storageStateFile,
    browserPlanFile: planFileArg?.split("=")[1] ?? null,
    extractVariant: variantArg?.split("=")[1],
    cookieInject: args.includes("--cookie-inject"),
    features: process.env.OCCAM_FEATURES || null,
  });
  guard.emit(result);
} catch (error) {
  guard.emit(failureFromError(error));
  process.exitCode = 1;
}
