/**
 * GFM table rendering for turndown.
 *
 * Stock turndown has no table rule, so a <table> collapses into a flat run of cell
 * text and the first cell of each row is swallowed (Q-023). This rule renders a
 * <table> as a pipe-delimited GFM table so agents keep the row/column structure.
 * Layout tables are left to Readability to strip; the structured json_tables path
 * (dom-tables.collectTables) is unaffected — this only improves the *markdown*.
 *
 * @param {import('turndown')} turndown
 */
export function addTableRule(turndown) {
  turndown.addRule("gfmTable", {
    filter: "table",
    replacement: (_content, node) => {
      // Scope rows/cells to THIS table only. querySelectorAll uses the descendant
      // combinator, so a nested <table> inside a cell would otherwise merge its rows
      // into the parent matrix and explode the column count — filter by closest("table").
      // (The nested table's text still survives, flattened into its parent cell.)
      const rows = Array.from(node.querySelectorAll("tr")).filter(
        (tr) => tr.closest("table") === node);
      const matrix = rows
        .map((tr) =>
          Array.from(tr.querySelectorAll("th, td"))
            .filter((cell) => cell.closest("table") === node)
            .map((cell) =>
              (cell.textContent || "")
                .replace(/\s+/g, " ")
                .replace(/\|/g, "\\|")
                .trim()))
        .filter((cells) => cells.length > 0);

      if (matrix.length === 0) {
        return "";
      }

      const cols = Math.max(...matrix.map((r) => r.length));
      const pad = (r) => {
        const copy = r.slice();
        while (copy.length < cols) {
          copy.push("");
        }
        return copy;
      };

      const header = pad(matrix[0]);
      const lines = [
        `| ${header.join(" | ")} |`,
        `| ${header.map(() => "---").join(" | ")} |`,
        ...matrix.slice(1).map((r) => `| ${pad(r).join(" | ")} |`),
      ];
      return `\n\n${lines.join("\n")}\n\n`;
    },
  });
}
