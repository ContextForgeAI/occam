#!/usr/bin/env node
import {
  buildCommandDetail,
  buildHelpViewModel,
  OPERATOR_NEXT_STEPS,
} from "./lib/operator/help-catalog.mjs";
import { selectRenderer } from "./lib/operator/render/select-renderer.mjs";
import {
  renderCommandDetailJson,
} from "./lib/operator/render/json-renderer.mjs";
import {
  renderCommandDetailPlain,
} from "./lib/operator/render/plain-renderer.mjs";
import {
  renderCommandDetailTty,
} from "./lib/operator/render/tty-renderer.mjs";

function parseArgs(argv) {
  const args = [...argv];
  let format = "tty";
  if (args.includes("--json")) {
    format = "json";
    args.splice(args.indexOf("--json"), 1);
  } else if (args.includes("--plain")) {
    format = "plain";
    args.splice(args.indexOf("--plain"), 1);
  }

  if (args.includes("-h") || args.includes("--help")) {
    return { format, commandId: "occam-help", showSelfHelp: true };
  }

  const commandId = args[0]?.trim() || null;
  return { format, commandId, showSelfHelp: false };
}

const { format, commandId, showSelfHelp } = parseArgs(process.argv.slice(2));

if (showSelfHelp) {
  console.log("usage: node scripts/occam-help.mjs [--json|--plain] [command-id]");
  process.exit(0);
}

if (commandId) {
  if (commandId === "next-steps") {
    const payload =
      format === "json"
        ? JSON.stringify({ nextSteps: OPERATOR_NEXT_STEPS }, null, 2)
        : format === "plain"
          ? OPERATOR_NEXT_STEPS.map((s) => `${s.id}\t${s.summary}\t${s.command ?? ""}`).join("\n")
          : [
              "Operator next steps",
              "─".repeat(40),
              ...OPERATOR_NEXT_STEPS.flatMap((s, i) => [
                `${i + 1}. ${s.summary}`,
                s.command ? `   ${s.command}` : "",
                s.alt ? `   alt: ${s.alt}` : "",
                s.doc ? `   doc: ${s.doc}` : "",
              ].filter(Boolean)),
            ].join("\n");
    console.log(payload);
    process.exit(0);
  }

  const detail = buildCommandDetail(commandId);
  const text =
    format === "json"
      ? renderCommandDetailJson(detail)
      : format === "plain"
        ? renderCommandDetailPlain(detail)
        : renderCommandDetailTty(detail);
  console.log(text);
  process.exit(detail ? 0 : 1);
}

const vm = buildHelpViewModel();
const { renderHelp } = selectRenderer(format);
console.log(renderHelp(vm));
