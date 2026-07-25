import { ONBOARD_COMPLETE, ONBOARD_WELCOME, STEP_COPY } from "../onboard-copy.mjs";
import { getConnectionNextSteps, buildSnippetFromOnboardResult } from "../mcp-snippet.mjs";
import { formatChoices, horizontalRule, indent, sectionBox, stepHeader } from "./tty-layout.mjs";

export function renderOnboardWelcome() {
  const lines = [
    sectionBox(ONBOARD_WELCOME.title, [
      ONBOARD_WELCOME.subtitle,
      "",
      ...ONBOARD_WELCOME.bullets,
      "",
      ONBOARD_WELCOME.hint,
    ]),
  ];
  return lines.join("\n");
}

/**
 * @param {string} stepId
 * @param {number} index 1-based
 * @param {number} total
 * @param {string} promptLabel
 * @param {string} defaultValue
 */
export function renderStepPrompt(stepId, index, total, promptLabel, defaultValue) {
  const copy = STEP_COPY[stepId];
  if (!copy) {
    const hint = defaultValue ? ` [${defaultValue}]` : "";
    return `${promptLabel}${hint}: `;
  }

  const blocks = [
    stepHeader(copy.title, index, total),
    "",
    indent(copy.description),
  ];

  if (copy.choices?.length) {
    blocks.push("", indent(formatChoices(copy.choices)));
  }

  const hint = defaultValue ? ` [${defaultValue}]` : "";
  blocks.push("");
  return `${blocks.join("\n")}› ${promptLabel}${hint}: `;
}

/**
 * @param {ReturnType<import("../onboard-flow.mjs").buildOnboardResult> & { verify?: { ok: boolean, skipped?: boolean, step?: string } }} result
 * @param {ReturnType<import("../onboard-flow.mjs").buildMcpSnippetConfig>} mcpConfig
 */
export function renderOnboardComplete(result, mcpConfig) {
  const snippet = buildSnippetFromOnboardResult(result);
  const envLines = Object.entries(result.env)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([k, v]) => `  ${k}=${v}`);

  const verifyLine =
    result.verify?.skipped
      ? "  verify     skipped (--skip-doctor or CI)"
      : result.verify?.ok
        ? "  verify     doctor OK"
        : `  verify     FAILED (${result.verify?.step ?? "unknown"})`;

  const nextSteps = getConnectionNextSteps(snippet.connectionKind);
  const launch =
    snippet.launchCommand ||
    `node ${(result.occamHome || "").replace(/\\/g, "/")}/scripts/launch-mcp-host.mjs`;

  const lines = [
    "",
    horizontalRule("═"),
    `  ${ONBOARD_COMPLETE.title}`,
    horizontalRule("═"),
    "",
    indent(`profile      ${result.profile}`),
    indent(`host         ${result.hostTarget}`),
    indent(`transport    stdio`),
    indent(`saved        ${result.configPath}`),
    verifyLine,
    "",
    indent("Occam MCP command:"),
    indent(`  ${launch}`),
    "",
    indent("Environment preview (merged on MCP spawn):"),
    ...envLines,
    "",
    indent("Suggested next action:"),
    ...nextSteps.map((s, i) => indent(`${i + 1}. ${s}`, "    ")),
  ];

  if (snippet.format === "yaml" && snippet.hermesYaml && result.hostTarget === "hermes") {
    lines.push(
      "",
      horizontalRule("─"),
      indent(ONBOARD_COMPLETE.hermesTip),
      "",
      snippet.hermesYaml.trimEnd(),
      "",
    );
  } else if (snippet.format === "json" && mcpConfig) {
    lines.push(
      "",
      horizontalRule("─"),
      indent("Paste into MCP host settings (host-neutral JSON):"),
      "",
      JSON.stringify(mcpConfig, null, 2),
      "",
    );
  } else if (snippet.message) {
    lines.push("", indent(snippet.message), "");
  }

  return lines.join("\n");
}
