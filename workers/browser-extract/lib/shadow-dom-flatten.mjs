/**
 * P10-C4: Flatten open shadow roots into light DOM clones for extract/skeleton.
 * Closed shadow roots stay invisible — P10-C4a heal guard still applies.
 */

export const FLAT_ATTR = "data-occam-shadow-flat";

/**
 * @param {Document} document
 * @param {{ maxHosts?: number }} [options]
 */
export function flattenOpenShadowRootsInDocument(document, options = {}) {
  const maxHosts = options.maxHosts ?? 128;
  let hostsFlattened = 0;
  const queue = [document.body].filter(Boolean);

  while (queue.length > 0 && hostsFlattened < maxHosts) {
    const root = queue.shift();
    const elements = root.querySelectorAll ? root.querySelectorAll("*") : [];
    for (const el of elements) {
      if (hostsFlattened >= maxHosts) break;
      const sr = el.shadowRoot;
      if (!sr) continue;
      if (el.querySelector(`[${FLAT_ATTR}]`)) continue;

      hostsFlattened += 1;
      const flat = document.createElement("div");
      flat.setAttribute(FLAT_ATTR, "true");
      flat.setAttribute("data-occam-shadow-host-tag", el.tagName.toLowerCase());

      for (const child of [...sr.childNodes]) {
        flat.appendChild(child.cloneNode(true));
      }

      el.appendChild(flat);
      queue.push(flat);
    }
  }

  return { hostsFlattened };
}

/**
 * @param {import('playwright').Page} page
 * @param {{ maxHosts?: number }} [options]
 */
export async function flattenOpenShadowRoots(page, options = {}) {
  return page.evaluate(
    ({ maxHosts, flatAttr }) => {
      let hostsFlattened = 0;
      const queue = [document.body].filter(Boolean);

      while (queue.length > 0 && hostsFlattened < maxHosts) {
        const root = queue.shift();
        const elements = root.querySelectorAll ? root.querySelectorAll("*") : [];
        for (const el of elements) {
          if (hostsFlattened >= maxHosts) break;
          const sr = el.shadowRoot;
          if (!sr) continue;
          if (el.querySelector(`[${flatAttr}]`)) continue;

          hostsFlattened += 1;
          const flat = document.createElement("div");
          flat.setAttribute(flatAttr, "true");
          flat.setAttribute("data-occam-shadow-host-tag", el.tagName.toLowerCase());

          for (const child of [...sr.childNodes]) {
            flat.appendChild(child.cloneNode(true));
          }

          el.appendChild(flat);
          queue.push(flat);
        }
      }

      return { hostsFlattened };
    },
    { maxHosts: options.maxHosts ?? 128, flatAttr: FLAT_ATTR },
  );
}
