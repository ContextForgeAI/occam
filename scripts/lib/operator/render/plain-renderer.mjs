/** @param {ReturnType<import("../help-catalog.mjs").buildHelpViewModel>} vm */
export function renderHelpPlain(vm) {
  const lines = [vm.title, ""];
  for (const row of vm.commands) {
    lines.push(`${row.tier}\t${row.id}\t${row.summary}`);
    lines.push(`  ${row.usage}`);
  }

  return lines.join("\n");
}

/** @param {ReturnType<import("../help-catalog.mjs").buildCommandDetail>} detail */
export function renderCommandDetailPlain(detail) {
  if (!detail) {
    return "not_found";
  }

  return [
    detail.id,
    detail.summary,
    detail.usage,
    detail.path,
    detail.tier,
    detail.relatedEnv?.join(", ") ?? "",
    detail.seeAlso ?? "",
  ].join("\n");
}
