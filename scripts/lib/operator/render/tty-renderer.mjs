/** @param {ReturnType<import("../help-catalog.mjs").buildHelpViewModel>} vm */
export function renderHelpTty(vm) {
  const lines = [
    vm.title,
    "─".repeat(52),
    "",
    "Operator commands:",
  ];

  for (const row of vm.commands.filter((c) => c.tier === "operator")) {
    lines.push(`  ${row.id}`);
    lines.push(`    ${row.summary}`);
    lines.push(`    ${row.usage}`);
  }

  lines.push("", "CI / Hermes:");
  for (const row of vm.commands.filter((c) => c.tier === "ci")) {
    lines.push(`  ${row.id} — ${row.summary}`);
  }

  lines.push("", "Maintainer:");
  for (const row of vm.commands.filter((c) => c.tier === "maintainer")) {
    lines.push(`  ${row.id} — ${row.summary}`);
  }

  lines.push("", "Next steps (install → connect → verify):");
  for (const step of vm.nextSteps ?? []) {
    lines.push(`  ${step.id}: ${step.summary}`);
    if (step.command) {
      lines.push(`    ${step.command}`);
    }
    if (step.alt) {
      lines.push(`    alt: ${step.alt}`);
    }
  }

  lines.push("", "Detail: node scripts/occam-help.mjs <command-id>");
  lines.push("Steps:  node scripts/occam-help.mjs next-steps");
  lines.push("JSON:    node scripts/occam-help.mjs --json");
  return lines.join("\n");
}

/** @param {ReturnType<import("../help-catalog.mjs").buildCommandDetail>} detail */
export function renderCommandDetailTty(detail) {
  if (!detail) {
    return "Command not found.";
  }

  const lines = [
    detail.id,
    detail.summary,
    "",
    `Usage: ${detail.usage}`,
    `Path:  ${detail.path}`,
    `Tier:  ${detail.tier}`,
  ];

  if (detail.relatedEnv?.length) {
    lines.push(`Env:   ${detail.relatedEnv.join(", ")}`);
  }

  if (detail.seeAlso) {
    lines.push(`Doc:   ${detail.seeAlso}`);
  }

  return lines.join("\n");
}
