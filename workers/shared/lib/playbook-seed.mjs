import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { AsyncLocalStorage } from "node:async_hooks";

const scriptDir = dirname(fileURLToPath(import.meta.url));

/** @type {object[] | null} */
let seedCache = null;

/** @type {object | null} */
let overlaySeed = null;
// Strict overlay (--playbook-overlay=) is a save-verify draft: it forces selector-only extraction
// (isStrictPlaybookOverlay). Soft overlay (--playbook-overlay-soft=) just ships the auto-resolved
// genome to the worker — it provides selectors/postMarkdown but keeps the Readability fallback, so a
// resolved genome without usable selectors doesn't turn into extraction_failed.
let overlayIsStrict = false;

const strictOverlayArg = process.argv.find((arg) => arg.startsWith("--playbook-overlay="));
const softOverlayArg = process.argv.find((arg) => arg.startsWith("--playbook-overlay-soft="));
const chosenOverlayArg = strictOverlayArg ?? softOverlayArg;
if (chosenOverlayArg) {
  const prefix = strictOverlayArg ? "--playbook-overlay=" : "--playbook-overlay-soft=";
  const overlayPath = chosenOverlayArg.slice(prefix.length).replace(/^"|"$/g, "");
  if (overlayPath && existsSync(overlayPath)) {
    try {
      overlaySeed = JSON.parse(readFileSync(overlayPath, "utf8"));
      overlayIsStrict = Boolean(strictOverlayArg);
    } catch (err) {
      const code = err && typeof err === "object" && "code" in err ? String(err.code) : "parse_failed";
      console.error(`[occam.worker] playbook_overlay_invalid code=${code}`);
      overlaySeed = null;
    }
  }
}

// A3: per-request overlay context for the long-lived browser daemon. Its process.argv is fixed at
// startup (--port only), so the argv globals above can only serve the one-shot worker (fresh argv per
// spawn). AsyncLocalStorage is the Node analogue of the C# AsyncLocal that PlaybookVerifyScope uses:
// the daemon wraps each /extract in runWithOverlay({seed, strict}) so concurrent/serial requests for
// different hosts keep isolated overlays. When no store is set (one-shot), we fall back to the argv
// globals, so the CLI path is byte-for-byte unchanged.
const overlayContext = new AsyncLocalStorage();

/** Run `fn` with a per-request overlay in scope (browser daemon path). */
export function runWithOverlay(seed, strict, fn) {
  return overlayContext.run({ seed: seed ?? null, strict: Boolean(strict) }, fn);
}

/** The active overlay: the per-request store (daemon) wins, else the argv-loaded global (one-shot). */
function activeOverlay() {
  const store = overlayContext.getStore();
  if (store) {
    return { seed: store.seed ?? null, strict: Boolean(store.strict) };
  }
  return { seed: overlaySeed, strict: overlayIsStrict };
}

export function resolveOccamHome() {
  const fromEnv = process.env.OCCAM_HOME?.trim();
  if (fromEnv) {
    return fromEnv;
  }
  return join(scriptDir, "..", "..", "..");
}

function seedsDirectory() {
  return join(resolveOccamHome(), "profiles", "playbooks", "seeds");
}

function localPlaybooksDirectory() {
  const fromEnv = process.env.OCCAM_PLAYBOOKS_LOCAL_ROOT?.trim();
  if (fromEnv) {
    return fromEnv;
  }
  return null;
}

function userPlaybooksDirectory() {
  const fromEnv = process.env.WT_PLAYBOOKS_PATH?.trim();
  if (fromEnv) {
    return fromEnv;
  }
  return null;
}

function communityPlaybooksDirectory() {
  return join(resolveOccamHome(), "profiles", "playbooks", "community");
}

/** @type {object[] | null} */
let communityCache = null;

/** @type {object[] | null} */
let userCache = null;

function loadDirectorySeeds(dir, cacheRef, setter) {
  if (cacheRef) {
    return cacheRef;
  }
  if (!dir || !existsSync(dir)) {
    setter([]);
    return [];
  }

  const loaded = readdirSync(dir)
    .filter((name) => name.endsWith(".json") && name !== "manifest.json")
    .map((name) => {
      try {
        return JSON.parse(readFileSync(join(dir, name), "utf8"));
      } catch (err) {
        const code = err && typeof err === "object" && "code" in err ? String(err.code) : "parse_failed";
        console.error(`[occam.worker] playbook_seed_invalid file=${name} code=${code}`);
        return null;
      }
    })
    .filter(Boolean);

  setter(loaded);
  return loaded;
}

function loadCommunitySeeds() {
  return loadDirectorySeeds(communityPlaybooksDirectory(), communityCache, (v) => {
    communityCache = v;
  });
}

function loadUserSeeds() {
  return loadDirectorySeeds(userPlaybooksDirectory(), userCache, (v) => {
    userCache = v;
  });
}

function findSeedForHost(seeds, host) {
  return seeds.find((seed) => hostMatchesSeed(seed, host)) ?? null;
}

function loadLocalSeedForUrl(url) {
  const dir = localPlaybooksDirectory();
  if (!dir || !existsSync(dir)) {
    return null;
  }

  const host = hostFromUrl(url);
  if (!host) {
    return null;
  }

  for (const name of readdirSync(dir)) {
    if (!name.endsWith(".playbook.json") && !name.endsWith(".json")) {
      continue;
    }

    if (name === "manifest.json") {
      continue;
    }

    try {
      const seed = JSON.parse(readFileSync(join(dir, name), "utf8"));
      if (hostMatchesSeed(seed, host)) {
        return seed;
      }
    } catch (err) {
      const code = err && typeof err === "object" && "code" in err ? String(err.code) : "parse_failed";
      console.error(`[occam.worker] local_playbook_invalid file=${name} code=${code}`);
    }
  }

  return null;
}

function loadSeeds() {
  if (seedCache) {
    return seedCache;
  }

  const dir = seedsDirectory();
  if (!existsSync(dir)) {
    seedCache = [];
    return seedCache;
  }

  seedCache = readdirSync(dir)
    .filter((name) => name.endsWith(".seed.json"))
    .map((name) => {
      const raw = readFileSync(join(dir, name), "utf8");
      return JSON.parse(raw);
    });

  return seedCache;
}

export function hostFromUrl(url) {
  try {
    return new URL(url).hostname.toLowerCase().replace(/^www\./, "");
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] host_from_url_failed code=${code}`);
    return "";
  }
}

export function isStrictPlaybookOverlay() {
  const store = overlayContext.getStore();
  if (store) {
    return Boolean(store.strict) && store.seed != null;
  }
  return overlayIsStrict && (overlaySeed !== null || Boolean(strictOverlayArg));
}

/**
 * True when the ACTIVE overlay (per-request store or argv global) matches this URL's host — i.e.
 * loadSeedForUrl returns the overlay seed and its selectors/postMarkdown actually shape the extract.
 * A3: the honest provenance signal the receipt stamps on — the overlay was APPLIED, not merely pushed.
 */
export function wasOverlayApplied(url) {
  const host = hostFromUrl(url);
  if (!host) {
    return false;
  }

  const { seed } = activeOverlay();
  return Boolean(seed && hostMatchesSeed(seed, host));
}

export function loadSeedForUrl(url) {
  const host = hostFromUrl(url);
  if (!host) {
    return null;
  }

  const { seed: activeSeed } = activeOverlay();
  if (activeSeed && hostMatchesSeed(activeSeed, host)) {
    return activeSeed;
  }

  const localSeed = loadLocalSeedForUrl(url);
  if (localSeed) {
    return localSeed;
  }

  const userSeed = findSeedForHost(loadUserSeeds(), host);
  if (userSeed) {
    return userSeed;
  }

  const communitySeed = findSeedForHost(loadCommunitySeeds(), host);
  if (communitySeed) {
    return communitySeed;
  }

  return findSeedForHost(loadSeeds(), host);
}

function hostMatchesSeed(seed, host) {
  return (seed.hosts ?? []).some((entry) => host === entry || host.endsWith(`.${entry}`));
}

export function getContentSelectorsForUrl(url) {
  const seed = loadSeedForUrl(url);
  // Local playbooks saved by early heal/save builds used snake_case. Keep current playbooks
  // canonical, but normalize that persisted legacy shape at the worker boundary so a higher-tier
  // local file cannot erase a valid lower-tier selector and collapse a live extract to thin output.
  const selectors = seed?.extract?.contentSelectors ?? seed?.extract?.content_selectors;
  return Array.isArray(selectors) ? selectors : [];
}

export function getDomStripSelectorsForUrl(url) {
  const seed = loadSeedForUrl(url);
  const selectors = seed?.extract?.domStripSelectors;
  return Array.isArray(selectors) ? selectors : [];
}

export function stripDomSelectors(document, selectors) {
  if (!document || !Array.isArray(selectors) || selectors.length === 0) {
    return;
  }

  for (const selector of selectors) {
    document.querySelectorAll(selector).forEach((el) => el.remove());
  }
}

function pruneNginxIndexModuleSpam(markdown, url) {
  if (!markdown || !url) {
    return markdown;
  }

  try {
    const host = new URL(url).hostname.toLowerCase().replace(/^www\./, "");
    const pathname = new URL(url).pathname;
    if (host !== "nginx.org" || !/^\/en\/docs\/?$/i.test(pathname)) {
      return markdown;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] nginx_prune_url_parse_failed code=${code}`);
    return markdown;
  }

  const lines = markdown.split("\n");
  const out = [];
  let inModules = false;

  for (const line of lines) {
    if (/^#{1,4}\s+Modules reference\b/i.test(line)) {
      inModules = true;
      out.push(line);
      continue;
    }

    if (inModules && /^#{1,4}\s+/.test(line) && !/^#{1,4}\s+Modules reference\b/i.test(line)) {
      inModules = false;
    }

    if (inModules && /^\*\s+.*\bngx_/i.test(line.trim())) {
      continue;
    }

    out.push(line);
  }

  return out.join("\n");
}

function restoreOpenAiDocHeadings(markdown, url) {
  if (!markdown || !url) {
    return markdown;
  }

  let host;
  try {
    host = new URL(url).hostname.toLowerCase().replace(/^www\./, "");
    if (host !== "developers.openai.com" && host !== "platform.openai.com") {
      return markdown;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] openai_headings_url_parse_failed code=${code}`);
    return markdown;
  }

  let result = markdown;
  const proseAnchors = [
    {
      title: "Embeddings",
      pattern: /(^|\n\n)(?!##\s+Embeddings\b)((?:An embedding is|Embeddings are)\b[^\n]*)/i,
    },
    {
      title: "Tokens",
      pattern:
        /(^|\n\n)(?!##\s+Tokens\b)((?:Text generation and embeddings models process text in chunks called tokens|Tokens represent commonly)[^\n]*)/i,
    },
  ];

  for (const { title, pattern } of proseAnchors) {
    result = result.replace(pattern, `$1## ${title}\n\n$2`);
  }

  const sections = [
    "Text generation models",
    "Text generation",
    "Embeddings",
    "Tokens",
    "Images and vision",
    "Audio",
  ];

  for (const title of sections) {
    const escaped = title.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const inlineRe = new RegExp(`(^|\\n\\n)(?!##\\s+${escaped}\\b)(${escaped})(\\s*\\n)`, "i");
    result = result.replace(inlineRe, `$1## ${title}$3`);
  }

  return result;
}

function pruneNuxtSidebarNav(markdown, url) {
  if (!markdown || !url) {
    return markdown;
  }

  try {
    const host = new URL(url).hostname.toLowerCase().replace(/^www\./, "");
    if (host !== "nuxt.com") {
      return markdown;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] nuxt_sidebar_url_parse_failed code=${code}`);
    return markdown;
  }

  const lines = markdown.split("\n");
  const kept = [];
  let consecutiveNav = 0;

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      consecutiveNav = 0;
      kept.push(line);
      continue;
    }

    if (/^[-*]\s+\[[^\]]{1,60}\]\([^)]+\)\s*$/.test(trimmed)) {
      consecutiveNav += 1;
      if (consecutiveNav > 3) {
        continue;
      }
    } else {
      consecutiveNav = 0;
    }

    kept.push(line);
  }

  return kept.join("\n");
}

function pruneNuxtFooterPromos(markdown, url) {
  if (!markdown || !url) {
    return markdown;
  }

  try {
    const host = new URL(url).hostname.toLowerCase().replace(/^www\./, "");
    if (host !== "nuxt.com") {
      return markdown;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] nuxt_footer_url_parse_failed code=${code}`);
    return markdown;
  }

  const dropPatterns = [
    /\bexplain with agent\b/i,
    /\bmaster nuxt\b/i,
    /\bnuxt certification\b/i,
    /certification\.nuxt\.com/i,
    /masteringnuxt\.com/i,
    /^certification$/i,
  ];

  return markdown
    .split("\n")
    .filter((line) => !dropPatterns.some((re) => re.test(line.trim())))
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function pruneDockerMarlinToolbar(markdown, url) {
  if (!markdown || !url) {
    return markdown;
  }

  try {
    const host = new URL(url).hostname.toLowerCase().replace(/^www\./, "");
    if (host !== "docs.docker.com") {
      return markdown;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] docker_toolbar_url_parse_failed code=${code}`);
    return markdown;
  }

  const dropPatterns = [
    /^Ask Gordon Copy Markdown View Markdown$/i,
    /^Ask Gordon$/i,
    /^Copy Markdown$/i,
    /^View Markdown$/i,
    /^function getCurrentPlaintextUrl\b/,
    /^function copyMarkdown\b/,
    /^function viewPlainText\b/,
    /^function askGordon\b/,
    /^> \*\*Embedded widget:\*\*/i,
    /_hjSafeContext/i,
  ];

  return markdown
    .split("\n")
    .filter((line) => !dropPatterns.some((re) => re.test(line.trim())))
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function pruneNuxtShikiCss(markdown, url) {
  if (!markdown || !url) {
    return markdown;
  }

  try {
    const host = new URL(url).hostname.toLowerCase().replace(/^www\./, "");
    if (host !== "nuxt.com") {
      return markdown;
    }
  } catch (err) {
    const code = err && typeof err === "object" && "code" in err ? String(err.code) : "invalid_url";
    console.error(`[occam.worker] nuxt_shiki_url_parse_failed code=${code}`);
    return markdown;
  }

  const dropPatterns = [
    /^\.shiki\b/i,
    /^--shiki-/i,
    /^\s*\{[^}]*--shiki-/,
    /--shiki-[a-z-]+:/i,
    /\.shiki[^;\n]*\{[^}]*--shiki-/i,
    // Package-manager tab bar (concatenated labels from code-block chrome).
    /^npm(?:yarn|pnpm|bun|deno)+$/i,
    /^npmyarnpnpmbun(?:deno)?$/i,
    /^Copy page$/i,
    /^Open on StackBlitz$/i,
    /^Show additional notes for an optimal setup$/i,
    /^Terminal$/i,
  ];

  return markdown
    .split("\n")
    .filter((line) => !dropPatterns.some((re) => re.test(line.trim())))
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

/**
 * Apply playbook seed post-markdown transforms (domain rules live in JSON flags, not host if-chains in extract).
 * @param {string} markdown
 * @param {string} url
 * @returns {string}
 */
export function applySeedPostMarkdown(markdown, url) {
  const seed = loadSeedForUrl(url);
  const flags = seed?.postMarkdown;
  if (!markdown || !flags) {
    return markdown;
  }

  let result = markdown;
  if (flags.nginxIndexModuleSpam) {
    result = pruneNginxIndexModuleSpam(result, url);
  }
  if (flags.openAiDocHeadings) {
    result = restoreOpenAiDocHeadings(result, url);
  }
  if (flags.nuxtSidebarNavCollapse) {
    result = pruneNuxtSidebarNav(result, url);
  }
  if (flags.nuxtFooterPromos) {
    result = pruneNuxtFooterPromos(result, url);
  }
  if (flags.nuxtShikiCssStrip) {
    result = pruneNuxtShikiCss(result, url);
  }
  if (flags.dockerMarlinToolbarStrip) {
    result = pruneDockerMarlinToolbar(result, url);
  }

  return result;
}

/** Reset cached seeds (tests). */
export function resetSeedCache() {
  seedCache = null;
  communityCache = null;
  userCache = null;
  overlaySeed = null;
  overlayIsStrict = false;
}
