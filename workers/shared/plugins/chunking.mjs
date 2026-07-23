/**
 * Semantic Chunking Plugin
 * Splits a markdown document into logical chunks based on headers.
 * Preserves the active headers hierarchy (breadcrumbs) for each chunk.
 * 
 * @param {string} markdown
 * @param {{ maxChunkLength?: number }} options
 * @returns {Array<{ text: string, headers: string[] }>}
 */
export function chunkMarkdown(markdown, options = {}) {
  const maxChunkLength = options.maxChunkLength ?? parseInt(process.env.OCCAM_CHUNK_SIZE ?? "2000", 10);
  const lines = markdown.split(/\r?\n/);
  const chunks = [];
  
  let currentChunkLines = [];
  let currentChunkLength = 0;
  let headerStack = []; // Tracks H1, H2, H3, H4, H5, H6

  for (const line of lines) {
    const headerMatch = line.match(/^(#{1,6})\s+(.+)$/);
    
    if (headerMatch) {
      const level = headerMatch[1].length;
      const title = headerMatch[2].trim();
      
      // If we have accumulated some content, commit it as a chunk before moving to a new section
      if (currentChunkLines.length > 0) {
        chunks.push({
          text: currentChunkLines.join("\n"),
          headers: headerStack.filter(Boolean),
        });
        currentChunkLines = [];
        currentChunkLength = 0;
      }

      // Update the header stack based on the heading level. Skipped levels (e.g. H2
      // straight to H4) leave sparse holes that filter(Boolean) drops from breadcrumbs.
      headerStack = headerStack.slice(0, level - 1);
      headerStack[level - 1] = title;
    }
    
    // Add line to the current chunk
    currentChunkLines.push(line);
    currentChunkLength += line.length + 1; // +1 for newline character
    
    // Check if we exceeded the size limit
    if (currentChunkLength >= maxChunkLength) {
      chunks.push({
        text: currentChunkLines.join("\n"),
        headers: [...headerStack].filter(Boolean),
      });
      currentChunkLines = [];
      currentChunkLength = 0;
    }
  }
  
  // Commit any remaining lines
  if (currentChunkLines.length > 0) {
    chunks.push({
      text: currentChunkLines.join("\n"),
      headers: [...headerStack].filter(Boolean),
    });
  }
  
  return chunks;
}

export default {
  name: "semantic_chunking",
  run(result) {
    if (result.ok && typeof result.markdown === "string") {
      result.chunks = chunkMarkdown(result.markdown);
    }
    return result;
  }
};
