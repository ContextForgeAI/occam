/**
 * Docs Change Brief assembler — reuses occam_transcode if_none_match / diff.
 * Not a new MCP tool. Not occam_watch.
 */
export const BRIEF_SCHEMA = "occam.docs-change-brief.v1";

const SIGNIFICANT =
  /\b(deprecat\w*|breaking change|removed|renamed|no longer|now defaults?|must not|warning:|changed in)\b/i;

/**
 * @param {string[]} argv
 */
export function parseBriefArgs(argv) {
  const flags = {
    help: false,
    json: false,
    urls: [],
    out: null,
    against: null,
    focus: null,
    fromJson: null,
    resume: false,
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
    if (token === "--resume") {
      flags.resume = true;
      continue;
    }
    const take = (name) => (token.includes("=") ? token.slice(name.length + 1) : args.shift());
    if (token === "--url" || token.startsWith("--url=")) {
      const url = take("--url");
      if (!url) flags.error = "missing --url value";
      else flags.urls.push(url);
      continue;
    }
    if (token === "--out" || token.startsWith("--out=")) {
      flags.out = take("--out");
      if (!flags.out) flags.error = "missing --out value";
      continue;
    }
    if (token === "--against" || token.startsWith("--against=")) {
      flags.against = take("--against");
      if (!flags.against) flags.error = "missing --against value";
      continue;
    }
    if (token === "--focus" || token.startsWith("--focus=")) {
      flags.focus = take("--focus");
      if (!flags.focus) flags.error = "missing --focus value";
      continue;
    }
    if (token === "--from-json" || token.startsWith("--from-json=")) {
      flags.fromJson = take("--from-json");
      if (!flags.fromJson) flags.error = "missing --from-json value";
      continue;
    }
    if (token.startsWith("-")) {
      flags.error = `unknown flag ${token}`;
      continue;
    }
    flags.urls.push(token);
  }
  if (!flags.help && !flags.error) {
    if (!flags.fromJson && flags.urls.length === 0) flags.error = "need --url or --from-json";
    else if (!flags.fromJson && !flags.out) flags.error = "need --out";
  }
  return flags;
}

/**
 * @param {string | null | undefined} text
 */
export function significanceOf(text) {
  if (!text) return "none";
  return SIGNIFICANT.test(text) ? "significant" : "noise";
}

/**
 * @param {object} input
 */
export function assembleBrief(input) {
  const pages = (input.pages ?? []).map((row) => {
    const ok = row.ok !== false;
    const unchanged = row.unchanged === true;
    const markdown = typeof row.markdown === "string" ? row.markdown : "";
    const significance = !ok ? "unknown" : unchanged ? "none" : significanceOf(markdown);
    return {
      url: row.url,
      ok,
      unchanged,
      contentHash: row.contentHash ?? null,
      failureCode: row.failureCode ?? null,
      significance,
      bytes: Buffer.byteLength(markdown, "utf8"),
    };
  });
  const failed = pages.filter((p) => !p.ok);
  const unchanged = pages.filter((p) => p.ok && p.unchanged);
  const changed = pages.filter((p) => p.ok && !p.unchanged);
  const significant = changed.filter((p) => p.significance === "significant");
  return {
    schema: BRIEF_SCHEMA,
    createdAt: input.createdAt ?? new Date().toISOString(),
    toolchain: input.toolchain ?? "ff-occam",
    focus: input.focus ?? null,
    pages,
    summary: {
      requested: pages.length,
      changed: changed.length,
      unchanged: unchanged.length,
      failed: failed.length,
      significant: significant.length,
    },
    stop: { reason: failed.length ? "extract_failed" : "complete" },
    ok: failed.length === 0 && pages.length > 0,
  };
}

export function briefFiles(report, notes = "") {
  const lines = ["# Docs change brief\n"];
  if (report.focus) lines.push(`Focus: ${report.focus}\n`);
  lines.push(
    `Changed ${report.summary.changed}, unchanged ${report.summary.unchanged}, failed ${report.summary.failed}, significant ${report.summary.significant}.\n`,
  );
  for (const page of report.pages) {
    lines.push(`## ${page.url}`);
    if (!page.ok) lines.push(`ok:false ${page.failureCode ?? ""}`.trim());
    else if (page.unchanged) lines.push("unchanged:true — no brief items.");
    else lines.push(`changed (${page.significance}). Review the new extract; do not invent deprecations.`);
    lines.push("");
  }
  if (notes) lines.push(notes.endsWith("\n") ? notes : `${notes}\n`);
  return {
    "brief-state.json": `${JSON.stringify(report, null, 2)}\n`,
    "brief.md": lines.join("\n"),
  };
}
