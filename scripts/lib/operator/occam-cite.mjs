/**
 * Citation Inspector assembler — reuses occam_claim_check.
 * Does not classify support vs refute. Not a new MCP tool.
 */
export const CITE_SCHEMA = "occam.citation-inspect.v1";

/**
 * @param {string[]} argv
 */
export function parseCiteArgs(argv) {
  const flags = {
    help: false,
    json: false,
    claim: null,
    url: null,
    out: null,
    fromJson: null,
    maxMatches: 3,
    error: null,
  };
  const args = [...argv];
  while (args.length) {
    const token = args.shift();
    if (token === "-h" || token === "--help") {
      flags.help = true;
      continue;
    }
    if (token === "--json") {
      flags.json = true;
      continue;
    }
    const take = (name) => (token.includes("=") ? token.slice(name.length + 1) : args.shift());
    if (token === "--claim" || token.startsWith("--claim=")) {
      flags.claim = take("--claim");
      if (!flags.claim) flags.error = "missing --claim value";
      continue;
    }
    if (token === "--url" || token.startsWith("--url=")) {
      flags.url = take("--url");
      if (!flags.url) flags.error = "missing --url value";
      continue;
    }
    if (token === "--out" || token.startsWith("--out=")) {
      flags.out = take("--out");
      if (!flags.out) flags.error = "missing --out value";
      continue;
    }
    if (token === "--from-json" || token.startsWith("--from-json=")) {
      flags.fromJson = take("--from-json");
      if (!flags.fromJson) flags.error = "missing --from-json value";
      continue;
    }
    if (token === "--max-matches" || token.startsWith("--max-matches=")) {
      const n = Number(take("--max-matches"));
      if (!Number.isInteger(n) || n < 1 || n > 10) flags.error = "--max-matches must be 1–10";
      else flags.maxMatches = n;
      continue;
    }
    if (token.startsWith("-")) {
      flags.error = `unknown flag ${token}`;
      continue;
    }
    if (!flags.claim) flags.claim = token;
    else if (!flags.url) flags.url = token;
    else flags.error = `unexpected argument ${token}`;
  }
  if (!flags.help && !flags.error) {
    if (!flags.fromJson && (!flags.claim || !flags.url)) flags.error = "need --claim and --url, or --from-json";
    else if (!flags.fromJson && !flags.out) flags.error = "need --out";
  }
  return flags;
}

/**
 * @param {object} input
 */
export function assembleCite(input) {
  const payload = input.payload ?? {};
  const ok = payload.ok === true;
  const found = payload.found === true;
  const matches = Array.isArray(payload.matches)
    ? payload.matches.map((row) => ({
        text: row.text ?? "",
        score: Number(row.score) || 0,
        sourceSelector: row.sourceSelector ?? null,
      }))
    : [];
  return {
    schema: CITE_SCHEMA,
    createdAt: input.createdAt ?? new Date().toISOString(),
    toolchain: input.toolchain ?? "ff-occam",
    claim: input.claim ?? payload.claim ?? null,
    url: input.url ?? payload.url ?? null,
    ok,
    found: ok ? found : null,
    retrieved: ok ? payload.retrieved === true : null,
    verdict: ok ? payload.verdict ?? "not_evaluated" : null,
    proven: payload.proven ?? null,
    failureCode: ok ? null : String(payload.failure?.code ?? payload.failureCode ?? "extraction_failed"),
    matchCount: matches.length,
    matches,
    reviewer: {
      action: "confirm or reject whether the retrieved passage supports the claim",
      not: "Occam does not decide support vs refute; verdict is not_evaluated",
    },
  };
}

export function citeFiles(report) {
  const lines = [
    "# Citation inspect\n",
    `Claim: ${report.claim}\n`,
    `URL: ${report.url}\n`,
    report.ok
      ? `found:${report.found}  verdict:${report.verdict}  matches:${report.matchCount}`
      : `ok:false ${report.failureCode}`,
    "",
    report.reviewer.action,
    report.reviewer.not,
    "",
  ];
  for (const match of report.matches) {
    lines.push(`## score ${match.score}`);
    if (match.sourceSelector) lines.push(`selector: ${match.sourceSelector}`);
    lines.push(match.text, "");
  }
  return {
    "inspect.json": `${JSON.stringify(report, null, 2)}\n`,
    "evidence.txt": `${lines.join("\n")}\n`,
  };
}
