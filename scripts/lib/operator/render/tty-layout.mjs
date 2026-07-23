const WIDTH = 62;

/** @param {string} [char] */
export function horizontalRule(char = "─") {
  return char.repeat(WIDTH);
}

/**
 * @param {string} text
 * @param {string} [prefix]
 */
export function indent(text, prefix = "  ") {
  return text
    .split("\n")
    .map((line) => (line.length ? `${prefix}${line}` : ""))
    .join("\n");
}

/**
 * @param {string} title
 * @param {number} index 1-based
 * @param {number} total
 */
export function stepHeader(title, index, total) {
  const label = `Step ${index} of ${total} · ${title}`;
  const pad = Math.max(1, WIDTH - label.length - 2);
  return `── ${label} ${"─".repeat(pad)}`;
}

/**
 * @param {string} title
 * @param {string[]} lines
 */
export function sectionBox(title, lines) {
  const out = [
    "",
    horizontalRule("═"),
    `  ${title}`,
    horizontalRule("═"),
    "",
    ...lines.map((l) => (l ? indent(l) : "")),
    "",
  ];
  return out.join("\n");
}

/**
 * @param {{ id: string, summary: string }[]} choices
 */
export function formatChoices(choices) {
  const lines = ["Choices:"];
  for (const c of choices) {
    const pad = Math.max(1, 14 - c.id.length);
    lines.push(`  ${c.id}${" ".repeat(pad)}${c.summary}`);
  }
  return lines.join("\n");
}
