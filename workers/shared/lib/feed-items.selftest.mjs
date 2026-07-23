import assert from "node:assert/strict";
import { JSDOM } from "jsdom";
import {
  collectFeedItems,
  collectJsonFeed,
  formatSummaryFields,
  looksLikeFeed,
  looksLikeJsonFeed,
} from "./feed-items.mjs";

function parseXml(xml) {
  const dom = new JSDOM(xml, { contentType: "text/xml" });
  return dom.window.document;
}

// --- RSS 2.0 with HTML-in-description (CDATA) ---
const rssXml = `<?xml version="1.0"?>
<rss version="2.0">
  <channel>
    <title>RSS Blog</title>
    <item>
      <title>Hello RSS</title>
      <link>https://example.com/rss/1</link>
      <pubDate>Mon, 20 Jul 2026 12:00:00 GMT</pubDate>
      <description><![CDATA[<p>Hello <a href="https://example.com/x">world</a> &amp; friends.</p>]]></description>
    </item>
  </channel>
</rss>`;

const rss = collectFeedItems(parseXml(rssXml), { baseUrl: "https://example.com/" });
assert.equal(rss?.title, "RSS Blog");
assert.equal(rss?.items.length, 1);
const rssItem = rss.items[0];
assert.ok(rssItem.summaryHtml.includes("<p>"), "RSS summaryHtml keeps markup");
assert.ok(rssItem.summaryHtml.includes("<a href="), "RSS summaryHtml keeps anchor");
assert.equal(rssItem.summaryText, "Hello world & friends.");
assert.equal(rssItem.summary, rssItem.summaryText, "summary aliases summaryText");
assert.ok(!rssItem.summaryMarkdown.includes("<p>"), "RSS markdown has no <p>");
assert.ok(!rssItem.summaryMarkdown.includes("<a"), "RSS markdown has no raw <a>");
assert.ok(rssItem.summaryMarkdown.includes("[world](https://example.com/x)"), "RSS markdown has link");
assert.ok(rssItem.summaryMarkdown.includes("&") || rssItem.summaryMarkdown.includes("friends"), "RSS markdown keeps text");

// --- Atom with HTML summary ---
const atomXml = `<?xml version="1.0"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title>Atom Feed</title>
  <entry>
    <title>Hello Atom</title>
    <link href="https://example.com/atom/1" rel="alternate"/>
    <published>2026-07-20T12:00:00Z</published>
    <summary type="html">&lt;p&gt;Atom &lt;em&gt;summary&lt;/em&gt; with &amp;quot;quotes&amp;quot;.&lt;/p&gt;</summary>
  </entry>
</feed>`;

const atom = collectFeedItems(parseXml(atomXml), { baseUrl: "https://example.com/" });
assert.equal(atom?.title, "Atom Feed");
assert.equal(atom?.items.length, 1);
const atomItem = atom.items[0];
assert.equal(atomItem.link, "https://example.com/atom/1");
assert.ok(atomItem.summaryHtml.includes("<p>") || atomItem.summaryHtml.includes("&lt;p&gt;"), "Atom keeps html-ish source");
assert.ok(!/<[a-z]/i.test(atomItem.summaryText.replace(/&/g, "")), "Atom summaryText has no tags");
assert.ok(atomItem.summaryText.includes("Atom"), "Atom summaryText has prose");
assert.ok(atomItem.summaryText.includes("summary"), "Atom summaryText has emphasis text");
assert.ok(!atomItem.summaryMarkdown.includes("<p>"), "Atom markdown clean of <p>");
assert.ok(atomItem.summaryMarkdown.includes("_summary_") || atomItem.summaryMarkdown.includes("summary"), "Atom markdown keeps emphasis");

// --- JSON Feed ---
assert.equal(looksLikeJsonFeed("application/feed+json", "{}"), true);
assert.equal(
  looksLikeFeed("application/json", '{"version":"https://jsonfeed.org/version/1.1","title":"x"}'),
  true,
);

const jsonFeed = collectJsonFeed({
  version: "https://jsonfeed.org/version/1.1",
  title: "JSON Feed Blog",
  items: [
    {
      id: "1",
      url: "https://example.com/jf/1",
      title: "Hello JSON Feed",
      date_published: "2026-07-20T12:00:00Z",
      content_html: '<p>JSON <a href="https://example.com/y">Feed</a> body.</p>',
      content_text: "JSON Feed body.",
    },
  ],
}, { baseUrl: "https://example.com/" });

assert.equal(jsonFeed?.title, "JSON Feed Blog");
assert.equal(jsonFeed?.items.length, 1);
const jf = jsonFeed.items[0];
assert.equal(jf.link, "https://example.com/jf/1");
assert.ok(jf.summaryHtml.includes("<p>"));
assert.equal(jf.summaryText, "JSON Feed body.");
assert.equal(jf.summary, "JSON Feed body.");
assert.ok(!jf.summaryMarkdown.includes("<a"));
assert.ok(jf.summaryMarkdown.includes("[Feed](https://example.com/y)"));

// --- Plain text (no HTML) stays plain ---
const plain = formatSummaryFields("Just a plain sentence.");
assert.equal(plain.summaryHtml, "");
assert.equal(plain.summaryText, "Just a plain sentence.");
assert.equal(plain.summaryMarkdown, "Just a plain sentence.");
assert.equal(plain.summary, plain.summaryText);

console.log("feed-items.selftest: OK");
