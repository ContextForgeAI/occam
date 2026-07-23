import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import { collectTables, reconstructSemanticRows } from "./dom-tables.mjs";

const hnHtml = `<!DOCTYPE html><html><body>
<table class="itemlist">
  <tr class="athing submission" id="48979269">
    <td align="right" valign="top" class="title"><span class="rank">1.</span></td>
    <td valign="top" class="votelinks"><a href="vote?id=48979269&amp;how=up"></a></td>
    <td class="title">
      <span class="titleline">
        <a href="https://werd.io/american-ai">China's open-weights AI strategy is winning</a>
        <span class="sitebit comhead"> (<a href="from?site=werd.io"><span class="sitestr">werd.io</span></a>)</span>
      </span>
    </td>
  </tr>
  <tr>
    <td colspan="2"></td>
    <td class="subtext">
      <span class="score" id="score_48979269">403 points</span> by
      <a href="user?id=benwerd" class="hnuser">benwerd</a>
      <span class="age" title="2026-07-20T12:00:00"><a href="item?id=48979269">4 hours ago</a></span>
      | <a href="hide?id=48979269">hide</a>
      | <a href="item?id=48979269">345&nbsp;comments</a>
    </td>
  </tr>
  <tr class="spacer" style="height:5px"></tr>
  <tr class="athing submission" id="48982374">
    <td align="right" valign="top" class="title"><span class="rank">2.</span></td>
    <td valign="top" class="votelinks"><a href="vote?id=48982374&amp;how=up"></a></td>
    <td class="title">
      <span class="titleline">
        <a href="https://rachelbythebay.com/w/2026/05/05/404/">That post never existed</a>
        <span class="sitebit comhead"> (<a href="from?site=rachelbythebay.com"><span class="sitestr">rachelbythebay.com</span></a>)</span>
      </span>
    </td>
  </tr>
  <tr>
    <td colspan="2"></td>
    <td class="subtext">
      <span class="score">29 points</span> by
      <a href="user?id=wglb" class="hnuser">wglb</a>
      <span class="age"><a href="item?id=48982374">32 minutes ago</a></span>
      | <a href="item?id=48982374">4&nbsp;comments</a>
    </td>
  </tr>
</table>
</body></html>`;

const dom = new JSDOM(hnHtml, { url: "https://news.ycombinator.com/" });
const doc = dom.window.document;
const root = doc.body;

const tables = collectTables(root, { doc });
assert.equal(tables.length, 1, "one itemlist table");
const t = tables[0];

// Physical rows still present (markdown path unchanged): 2 stories × 2 data rows (+ spacer skipped when empty).
assert.ok(t.rows.length >= 4, `physical rows kept (got ${t.rows.length})`);
assert.ok(!("markdown" in t), "tables codec does not invent markdown");

assert.ok(Array.isArray(t.records), "semantic records emitted");
assert.equal(t.records.length, 2, "two stories → two semantic records (not four physical rows)");

const r0 = t.records[0];
assert.equal(r0.schema, "hn_item");
assert.equal(r0.rank, "1");
assert.equal(r0.title, "China's open-weights AI strategy is winning");
assert.equal(r0.url, "https://werd.io/american-ai");
assert.equal(r0.site, "werd.io");
assert.equal(r0.author, "benwerd");
assert.equal(r0.points, 403);
assert.equal(r0.comments, 345);
assert.ok(r0.age && r0.age.includes("hour"), `age present: ${r0.age}`);
assert.ok(r0.provenance?.source_selector, "provenance.source_selector");
assert.ok(Array.isArray(r0.provenance.row_indexes), "provenance.row_indexes");
assert.equal(r0.provenance.row_indexes.length, 2, "one record spans two physical rows");
assert.ok(r0.provenance.table_selector, "provenance.table_selector");

const r1 = t.records[1];
assert.equal(r1.rank, "2");
assert.equal(r1.author, "wglb");
assert.equal(r1.points, 29);
assert.equal(r1.comments, 4);

// Direct API
const direct = reconstructSemanticRows(doc.querySelector("table.itemlist"), { doc, root });
assert.equal(direct.length, 2);

// Ordinary data table must NOT invent hn_item records.
const plainHtml = `<table>
  <thead><tr><th>A</th><th>B</th></tr></thead>
  <tbody>
    <tr><td>1</td><td>2</td></tr>
    <tr><td>3</td><td>4</td></tr>
  </tbody>
</table>`;
const plainDom = new JSDOM(plainHtml);
const plainTables = collectTables(plainDom.window.document.body, { doc: plainDom.window.document });
assert.equal(plainTables.length, 1);
assert.equal(plainTables[0].records, undefined, "plain tables have no semantic records");
assert.deepEqual(plainTables[0].rows, [["1", "2"], ["3", "4"]]);

console.log("dom-tables.selftest: OK");
