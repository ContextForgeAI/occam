/**
 * DOM-derived structured tables for agent analysis (opt-in `json_tables` feature).
 *
 * Walks the article content DOM and emits one entry per genuine data table:
 *   { caption, headers[], rows[][], source_selector, records? }
 *
 * `rows` stay physical (one array per <tr>) so markdown/GFM consumers are unchanged.
 * `records` are optional semantic reconstructions — e.g. Hacker News pairs a title row
 * (`tr.athing`) with the following subtext row into one knowledge object:
 *   { rank, title, url, site, author, points, comments, age, schema, provenance }
 *
 * Layout tables (those containing a nested <table>) are skipped — they carry no tabular data.
 * `source_selector` follows the same semantics as dom-blocks.
 */

import { buildElementSelector } from "./dom-blocks.mjs";

const DEFAULT_MAX_TABLES = 40;
const MAX_ROWS = 200;
const MAX_COLS = 40;
const MAX_CELL_LENGTH = 2000;
const MAX_RECORDS = 200;

/**
 * @param {Element | null} root content root (connected element or detached wrapper)
 * @param {{ doc?: Document, maxTables?: number }} [options]
 * @returns {Array<{ caption: string, headers: string[], rows: string[][], source_selector: string, records?: SemanticRecord[] }>}
 */
export function collectTables(root, options = {}) {
  if (!root) {
    return [];
  }
  const doc = options.doc ?? root.ownerDocument ?? null;
  if (!doc) {
    return [];
  }
  const maxTables = options.maxTables ?? DEFAULT_MAX_TABLES;

  const candidates = root.localName === "table"
    ? [root, ...root.querySelectorAll("table")]
    : [...root.querySelectorAll("table")];

  const out = [];
  for (const table of candidates) {
    if (out.length >= maxTables) {
      break;
    }
    // Skip nested/layout tables: a table that contains another table is structural, not data.
    if (table.querySelector("table")) {
      continue;
    }
    const parsed = parseTable(table, doc, root);
    if (parsed) {
      out.push(parsed);
    }
  }
  return out;
}

/**
 * Pure reconstruction entry used by selftests — works from a live table element.
 * @param {Element} table
 * @param {{ doc?: Document, root?: Element }} [options]
 * @returns {SemanticRecord[]}
 */
export function reconstructSemanticRows(table, options = {}) {
  if (!table) {
    return [];
  }
  const doc = options.doc ?? table.ownerDocument ?? null;
  const root = options.root ?? table;
  return reconstructFromDom(table, doc, root);
}

function parseTable(table, doc, root) {
  const caption = normalizeText(table.querySelector("caption")?.textContent) ?? "";
  const allRows = [...table.querySelectorAll("tr")];
  if (allRows.length === 0) {
    return null;
  }

  let headers = [];
  let bodyRows = allRows;

  const thead = table.querySelector("thead");
  if (thead) {
    const headerRow = thead.querySelector("tr");
    if (headerRow) {
      headers = cellsOf(headerRow);
    }
    bodyRows = allRows.filter((tr) => !thead.contains(tr));
  } else {
    const first = allRows[0];
    const firstCells = first ? [...first.children].filter((c) => c.localName === "td" || c.localName === "th") : [];
    if (firstCells.length > 0 && firstCells.every((c) => c.localName === "th")) {
      headers = cellsOf(first);
      bodyRows = allRows.slice(1);
    }
  }

  const rows = [];
  const bodyIndexByTr = new Map();
  for (const tr of bodyRows) {
    if (rows.length >= MAX_ROWS) {
      break;
    }
    const cells = cellsOf(tr);
    if (cells.length === 0) {
      continue;
    }
    bodyIndexByTr.set(tr, rows.length);
    rows.push(cells);
  }

  if (headers.length === 0 && rows.length === 0) {
    return null;
  }

  // Reject single-column tables with no header — almost always layout, not data.
  const maxCols = Math.max(headers.length, ...rows.map((r) => r.length), 0);
  if (headers.length === 0 && maxCols < 2) {
    return null;
  }

  const source_selector = buildElementSelector(table, { doc, root });
  const records = reconstructFromDom(table, doc, root, bodyIndexByTr);
  const result = {
    caption,
    headers,
    rows,
    source_selector,
  };
  if (records.length > 0) {
    result.records = records;
  }
  return result;
}

/**
 * @typedef {{
 *   rank?: string,
 *   title?: string,
 *   url?: string,
 *   site?: string,
 *   author?: string,
 *   points?: number,
 *   comments?: number,
 *   age?: string,
 *   schema: string,
 *   provenance: { source_selector: string, row_indexes: number[], table_selector?: string }
 * }} SemanticRecord
 */

/**
 * @param {Element} table
 * @param {Document | null} doc
 * @param {Element} root
 * @param {Map<Element, number>} [bodyIndexByTr]
 * @returns {SemanticRecord[]}
 */
function reconstructFromDom(table, doc, root, bodyIndexByTr = null) {
  const athings = [...table.querySelectorAll("tr.athing")];
  if (athings.length >= 1) {
    return reconstructHnItemlist(table, athings, doc, root, bodyIndexByTr);
  }

  // Textual paired-row fallback when classes are gone but HN-shaped text remains.
  return reconstructPairedTextRows(table, doc, root, bodyIndexByTr);
}

/**
 * Hacker News itemlist: each story is `tr.athing` + next sibling `tr` with `.subtext`.
 * @returns {SemanticRecord[]}
 */
function reconstructHnItemlist(table, athings, doc, root, bodyIndexByTr) {
  const tableSel = buildElementSelector(table, { doc, root });
  const records = [];

  for (const athing of athings) {
    if (records.length >= MAX_RECORDS) {
      break;
    }
    const sub = nextDataRow(athing);
    const rank = normalizeText(athing.querySelector(".rank")?.textContent)?.replace(/\.$/, "") ?? "";
    const titleAnchor = athing.querySelector(".titleline > a")
      ?? athing.querySelector(".titleline a")
      ?? athing.querySelector("a[href]");
    const title = normalizeText(titleAnchor?.textContent) ?? "";
    const url = absolutize(titleAnchor?.getAttribute?.("href") ?? "", doc);
    const site = normalizeText(athing.querySelector(".sitestr")?.textContent)
      ?? siteFromUrl(url)
      ?? "";

    let author = "";
    let points = null;
    let comments = null;
    let age = "";
    if (sub) {
      author = normalizeText(sub.querySelector(".hnuser")?.textContent) ?? "";
      const scoreText = normalizeText(sub.querySelector(".score")?.textContent) ?? "";
      points = parseLeadingInt(scoreText);
      const ageEl = sub.querySelector(".age") ?? sub.querySelector("a[href*='item?id=']");
      age = normalizeText(ageEl?.getAttribute?.("title") ? ageEl.textContent : ageEl?.textContent) ?? "";
      comments = parseComments(sub);
    }

    if (!title && !url) {
      continue;
    }

    const rowIndexes = [];
    const athingIdx = bodyIndexByTr?.get(athing);
    if (typeof athingIdx === "number") {
      rowIndexes.push(athingIdx);
    }
    if (sub) {
      const subIdx = bodyIndexByTr?.get(sub);
      if (typeof subIdx === "number") {
        rowIndexes.push(subIdx);
      }
    }

    records.push({
      rank: rank || undefined,
      title: title || undefined,
      url: url || undefined,
      site: site || undefined,
      author: author || undefined,
      points: points ?? undefined,
      comments: comments ?? undefined,
      age: age || undefined,
      schema: "hn_item",
      provenance: {
        source_selector: buildElementSelector(athing, { doc, root }),
        row_indexes: rowIndexes,
        table_selector: tableSel,
      },
    });
  }

  return records;
}

/**
 * When `.athing` is absent: pair a rank+title row with a following "N points by …" meta row.
 */
function reconstructPairedTextRows(table, doc, root, bodyIndexByTr) {
  const trs = [...table.querySelectorAll("tr")].filter((tr) => {
    const cells = cellsOf(tr);
    return cells.length > 0 && !tr.classList?.contains?.("spacer");
  });
  if (trs.length < 4) {
    return [];
  }

  const tableSel = buildElementSelector(table, { doc, root });
  const records = [];
  let i = 0;
  while (i < trs.length - 1 && records.length < MAX_RECORDS) {
    const titleTr = trs[i];
    const metaTr = trs[i + 1];
    const titleCells = cellsOf(titleTr);
    const metaJoined = cellsOf(metaTr).join(" ");
    const looksLikeMeta = /\b\d+\s+points?\b/i.test(metaJoined) || /\bby\s+\S+/i.test(metaJoined);
    const looksLikeTitle = titleCells.some((c) => /^\d+\.?$/.test(c))
      || titleTr.querySelector("a[href]");

    if (!looksLikeMeta || !looksLikeTitle) {
      i += 1;
      continue;
    }

    const rankCell = titleCells.find((c) => /^\d+\.?$/.test(c));
    const rank = rankCell ? rankCell.replace(/\.$/, "") : undefined;
    const titleAnchor = titleTr.querySelector(".titleline > a")
      ?? titleTr.querySelector("a[href]:not([href^='vote'])");
    const title = normalizeText(titleAnchor?.textContent)
      ?? titleCells.filter((c) => c !== rankCell).sort((a, b) => b.length - a.length)[0]
      ?? "";
    const url = absolutize(titleAnchor?.getAttribute?.("href") ?? "", doc);
    const siteMatch = metaJoined.match(/\(([^)]+)\)/)
      ?? titleCells.join(" ").match(/\(([^)]+)\)/);
    const site = normalizeText(titleTr.querySelector(".sitestr")?.textContent)
      ?? (siteMatch ? siteMatch[1] : null)
      ?? siteFromUrl(url)
      ?? undefined;

    const authorMatch = metaJoined.match(/\bby\s+(\S+)/i);
    const points = parseLeadingInt(metaJoined.match(/(\d+)\s+points?/i)?.[0] ?? "");
    const comments = parseLeadingInt(metaJoined.match(/(\d+)\s+comments?/i)?.[0] ?? "")
      ?? (/\bdiscuss\b/i.test(metaJoined) ? 0 : null);
    const ageMatch = metaJoined.match(/(\d+\s+(?:minute|hour|day|week|month|year)s?\s+ago)/i);

    const rowIndexes = [];
    const tIdx = bodyIndexByTr?.get(titleTr);
    const mIdx = bodyIndexByTr?.get(metaTr);
    if (typeof tIdx === "number") {
      rowIndexes.push(tIdx);
    }
    if (typeof mIdx === "number") {
      rowIndexes.push(mIdx);
    }

    if (title || url) {
      records.push({
        rank,
        title: title || undefined,
        url: url || undefined,
        site,
        author: authorMatch?.[1],
        points: points ?? undefined,
        comments: comments ?? undefined,
        age: ageMatch?.[1],
        schema: "hn_item",
        provenance: {
          source_selector: buildElementSelector(titleTr, { doc, root }),
          row_indexes: rowIndexes,
          table_selector: tableSel,
        },
      });
    }

    i += 2;
    // Skip spacer if present.
    if (i < trs.length && (trs[i].classList?.contains?.("spacer") || cellsOf(trs[i]).every((c) => !c))) {
      i += 1;
    }
  }

  // Only accept when we clearly reconstructed a list (avoids false pairs on ordinary tables).
  return records.length >= 2 ? records : [];
}

function nextDataRow(tr) {
  let sib = tr.nextElementSibling;
  while (sib) {
    if (sib.localName === "tr") {
      if (sib.classList?.contains?.("spacer")) {
        sib = sib.nextElementSibling;
        continue;
      }
      return sib;
    }
    sib = sib.nextElementSibling;
  }
  return null;
}

function parseComments(sub) {
  const links = [...(sub.querySelectorAll?.("a") ?? [])];
  for (const a of links) {
    const t = normalizeText(a.textContent) ?? "";
    if (/^\d+\s+comments?$/i.test(t)) {
      return parseLeadingInt(t);
    }
    if (/^discuss$/i.test(t)) {
      return 0;
    }
  }
  const blob = normalizeText(sub.textContent) ?? "";
  const m = blob.match(/(\d+)\s+comments?/i);
  return m ? parseLeadingInt(m[0]) : null;
}

function parseLeadingInt(value) {
  const m = String(value ?? "").match(/(\d+)/);
  return m ? Number.parseInt(m[1], 10) : null;
}

function siteFromUrl(url) {
  if (!url) {
    return null;
  }
  try {
    const host = new URL(url).hostname.replace(/^www\./, "");
    return host || null;
  } catch {
    return null;
  }
}

function absolutize(href, doc) {
  if (!href) {
    return "";
  }
  try {
    const base = doc?.baseURI || doc?.URL || undefined;
    return new URL(href, base).href;
  } catch {
    return href;
  }
}

function cellsOf(tr) {
  const out = [];
  for (const cell of tr.children) {
    if (cell.localName !== "td" && cell.localName !== "th") {
      continue;
    }
    if (out.length >= MAX_COLS) {
      break;
    }
    let text = normalizeText(cell.textContent) ?? "";
    if (text.length > MAX_CELL_LENGTH) {
      text = text.slice(0, MAX_CELL_LENGTH);
    }
    out.push(text);
  }
  return out;
}

function normalizeText(value) {
  const text = (value ?? "").replace(/\s+/g, " ").trim();
  return text.length > 0 ? text : null;
}
