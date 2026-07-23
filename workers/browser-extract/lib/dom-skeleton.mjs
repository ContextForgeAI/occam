/**
 * Lightweight DOM skeleton for self-healing playbooks (PB3).
 * No raw HTML — capped tree for host-agent reasoning.
 */

const SKIP_TAGS = new Set(["script", "style", "noscript", "svg", "path", "link", "meta"]);
const INTERACTIVE = new Set(["a", "button", "input", "select", "textarea", "summary"]);

/**
 * @param {import('playwright').Page} page
 * @param {{ maxNodes?: number, maxDepth?: number }} opts
 */
export async function buildDomSkeleton(page, opts = {}) {
  const maxNodes = Math.min(Math.max(opts.maxNodes ?? 400, 50), 600);
  const maxDepth = opts.maxDepth ?? 12;

  return page.evaluate(
    ({ maxNodes, maxDepth, skipTags, interactiveTags }) => {
      const stats = { nodeCount: 0, maxDepth: 0, interactiveCount: 0 };
      const landmarks = new Set();
      const testIds = [];
      const mainCandidates = [];

      function trim(s, n = 80) {
        if (!s) return null;
        const t = s.replace(/\s+/g, " ").trim();
        return t.length > n ? `${t.slice(0, n)}…` : t;
      }

      function classList(el) {
        const raw = el.className;
        if (typeof raw !== "string" || !raw.trim()) return null;
        return raw.split(/\s+/).filter(Boolean).slice(0, 3);
      }

      function isInteractive(el) {
        const tag = el.tagName.toLowerCase();
        if (interactiveTags.includes(tag)) return true;
        if (el.getAttribute("role") === "button") return true;
        if (el.hasAttribute("onclick") || el.hasAttribute("tabindex")) return true;
        return false;
      }

      function scoreMain(el) {
        const tag = el.tagName.toLowerCase();
        let score = 0;
        if (tag === "main" || el.getAttribute("role") === "main") score += 0.4;
        if (el.id && /content|main|readme|article/i.test(el.id)) score += 0.2;
        const text = trim(el.innerText ?? "", 200) ?? "";
        if (text.length > 120) score += 0.25;
        if (el.querySelector("article, h1, h2")) score += 0.15;
        return Math.min(score, 1);
      }

      function selectorHint(el) {
        if (el.id) return `#${CSS.escape(el.id)}`;
        const tid = el.getAttribute("data-testid");
        if (tid) return `[data-testid="${tid}"]`;
        const tag = el.tagName.toLowerCase();
        const cls = classList(el);
        if (cls?.length) return `${tag}.${cls.join(".")}`;
        return tag;
      }

      function walk(el, depth) {
        if (stats.nodeCount >= maxNodes || depth > maxDepth) return null;
        const tag = el.tagName?.toLowerCase();
        if (!tag || skipTags.includes(tag)) return null;

        stats.nodeCount += 1;
        stats.maxDepth = Math.max(stats.maxDepth, depth);

        const role = el.getAttribute("role");
        if (role === "main" || role === "navigation" || tag === "main" || tag === "nav") {
          landmarks.add(role || tag);
        }

        const testId = el.getAttribute("data-testid");
        if (testId && testIds.length < 40) testIds.push(testId);

        const interactive = isInteractive(el);
        if (interactive) stats.interactiveCount += 1;

        const node = {
          tag,
          id: el.id || null,
          class: classList(el),
          role: role || null,
          testId: testId || null,
          aria: trim(el.getAttribute("aria-label") ?? ""),
          text: trim(el.childNodes.length === 1 && el.childNodes[0]?.nodeType === 3 ? el.textContent : ""),
          interactive,
          children: [],
        };

        const mainScore = scoreMain(el);
        if (mainScore >= 0.45 && mainCandidates.length < 12) {
          mainCandidates.push({
            selector: selectorHint(el),
            textAnchor: trim(el.innerText ?? "", 80),
            score: Math.round(mainScore * 100) / 100,
          });
        }

        for (const child of el.children) {
          if (stats.nodeCount >= maxNodes) break;
          const c = walk(child, depth + 1);
          if (c) node.children.push(c);
        }

        const shadow = el.shadowRoot;
        if (shadow && stats.nodeCount < maxNodes) {
          for (const child of shadow.children) {
            if (stats.nodeCount >= maxNodes) break;
            const c = walk(child, depth + 1);
            if (c) {
              if (!node.children) node.children = [];
              node.children.push(c);
            }
          }
        }

        if (node.children?.length === 0) delete node.children;
        return node;
      }

      const body = document.body;
      const root = body ? walk(body, 0) : { tag: "body", children: [] };

      mainCandidates.sort((a, b) => b.score - a.score);

      return {
        root,
        stats,
        anchors: {
          landmarks: [...landmarks],
          dataTestIds: testIds,
          mainCandidates: mainCandidates.slice(0, 8),
        },
      };
    },
    {
      maxNodes,
      maxDepth,
      skipTags: [...SKIP_TAGS],
      interactiveTags: [...INTERACTIVE],
    },
  );
}
