/**
 * Host-agnostic markdown boilerplate prune (footer, ads, SPA chrome noise).
 * @param {string} markdown
 * @returns {string}
 */
export function genericMarkdownPrune(markdown) {
  if (!markdown) {
    return markdown;
  }

  // Empty-text anchor links are never content: MkDocs line-number anchors
  // ("[](#__codelineno-0-1)" before each code line), HN/Reddit upvote arrows
  // ("[](https://news.ycombinator.com/vote?...)"), icon-only links, etc. Strip any "[]( … )"
  // (incl. whitespace-only text) inline. The negative lookbehind preserves empty-alt images
  // "![](url)" — those are real media references.
  markdown = markdown.replace(/(?<!!)\[\s*\]\([^)\s]*\)/g, "");

  const dropPatterns = [
    /help improve mdn/i,
    /view this page on github/i,
    /learn how to contribute/i,
    /this page was last modified/i,
    /mdn contributors/i,
    /report a problem with this content/i,
    /ads via carbon/i,
    /carbonads/i,
    /was this helpful/i,
    /edit this page on github/i,
    /^on this page$/i,
    /^community$/i,
    /become a sponsor/i,
    /^sponsor$/i,
    /\bdiscord\b/i,
    /\bbluesky\b/i,
    /^menu\s*on this page/i,
    /menuon this page/i,
    /^[-*]\s+\[(Discord|Bluesky|GitHub|X)\]/i,
    /^[\p{Emoji_Presentation}\p{Extended_Pictographic}\p{Emoji}]{1,4}(\s*[\p{Emoji_Presentation}\p{Extended_Pictographic}\p{Emoji}]{1,4})*\s*$/u,
    /[\p{Emoji_Presentation}\p{Extended_Pictographic}]{2,}/u,
  ];

  const lines = markdown.split("\n");
  const kept = [];

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      kept.push(line);
      continue;
    }

    if (dropPatterns.some((re) => re.test(trimmed))) {
      continue;
    }

    if (/^community\b/i.test(trimmed) && trimmed.length < 40) {
      continue;
    }

    kept.push(line);
  }

  return kept.join("\n").replace(/\n{3,}/g, "\n\n").trim();
}
