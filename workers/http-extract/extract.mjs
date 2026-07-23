import { runHttpExtract } from "./lib/http-extract-run.mjs";
import { installSilentExitGuard } from "../shared/lib/worker-exit-guard.mjs";

// A stalled extract must still tell the host something typed. Without this, a promise that never
// settles makes node exit 13 printing nothing, and the host reports "workers_unavailable / run doctor"
// — telling the user their install is broken when it isn't.
const guard = installSilentExitGuard("node_readability_turndown");

const url = process.argv[2];
if (!url) {
  guard.disarm(); // a usage error is reported on stderr + exit 1, not as a JSON verdict
  console.error("Usage: node extract.mjs <url> [--html-file=path] [--final-url=url] [--headers-file=path]");
  process.exit(1);
}

const extraArgs = process.argv.slice(3);
const htmlFileArg = extraArgs.find((arg) => arg.startsWith("--html-file="));
const finalUrlArg = extraArgs.find((arg) => arg.startsWith("--final-url="));
const headersFileArg = extraArgs.find((arg) => arg.startsWith("--headers-file="));

const result = await runHttpExtract({
  url,
  htmlFile: htmlFileArg?.slice("--html-file=".length).replace(/^"|"$/g, "") ?? null,
  finalUrl: finalUrlArg?.slice("--final-url=".length).replace(/^"|"$/g, "") ?? null,
  headersFile: headersFileArg?.slice("--headers-file=".length) ?? null,
});

guard.emit(result);
if (!result.ok) {
  process.exit(0);
}
