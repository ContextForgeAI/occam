/**
 * Parse MCP tools/call results. Occam marks typed ok:false envelopes with
 * isError:true while keeping the JSON body — callers must still read it.
 */

/**
 * @param {unknown} result
 * @returns {{ raw: unknown, parsed: Record<string, unknown> | null, isError: boolean }}
 */
export function parseToolJson(result) {
  const rec = result && typeof result === "object" ? /** @type {Record<string, unknown>} */ (result) : {};
  const isError = rec.isError === true;
  const content = Array.isArray(rec.content) ? rec.content : [];
  const textBlock = content.find((c) => c && typeof c === "object" && c.type === "text");
  const text = textBlock && typeof textBlock.text === "string" ? textBlock.text : null;
  if (!text) return { raw: result, parsed: null, isError };
  try {
    const parsed = JSON.parse(text);
    return {
      raw: text,
      parsed: parsed && typeof parsed === "object" ? parsed : null,
      isError,
    };
  } catch {
    return { raw: text, parsed: null, isError };
  }
}
