import { JSDOM, VirtualConsole } from "jsdom";
import TurndownService from "turndown";
import { addTableRule } from "./turndown-table-rule.mjs";
import { getDefaultFetchHeaders } from "./default-fetch-headers.mjs";
import { EgressProxyError, egressFetch } from "./egress-proxy.mjs";
import { loadSeedForUrl } from "./playbook-seed.mjs";
import { pinnedDispatcherForUrl, SsrfBlockedError } from "./private-ip.mjs";
import { readResponseBodyForExtract } from "./response-body-cap.mjs";

export const CRATES_README_SELECTOR = "#crate-readme";
const CRATES_API_USER_AGENT = "FF-Occam/1.0 (+https://github.com/ContextForgeAI/occam)";

const adapters = new Map([
  ["npm_registry_package", {
    resolve(requestedUrl) {
      const parsed = new URL(requestedUrl);
      let decodedPathname;
      try {
        decodedPathname = decodeURIComponent(parsed.pathname);
      } catch {
        return null;
      }
      const match = decodedPathname.match(/^\/package\/((?:@[^/]+\/)?[^/]+)\/?$/);
      if (!match || !/^(?:@[a-z0-9][a-z0-9._~-]*\/)?[a-z0-9][a-z0-9._~-]*$/i.test(match[1])) {
        return null;
      }
      return `https://registry.npmjs.org/${encodeURIComponent(match[1])}/latest`;
    },
    render(payload, sourceUrl) {
      if (!payload || typeof payload !== "object" || typeof payload.name !== "string") {
        return null;
      }

      const lines = [`# ${payload.name}`];
      if (typeof payload.description === "string" && payload.description.trim()) {
        lines.push("", payload.description.trim());
      }
      lines.push("", "## Package metadata");
      appendField(lines, "Latest version", payload.version);
      appendField(lines, "License", normalizeLicense(payload.license));
      appendLinkField(lines, "Homepage", payload.homepage);
      appendLinkField(lines, "Repository", normalizeRepository(payload.repository));

      const dependencies = payload.dependencies && typeof payload.dependencies === "object"
        ? Object.entries(payload.dependencies)
        : [];
      if (dependencies.length > 0) {
        lines.push("", "## Dependencies");
        for (const [name, version] of dependencies.slice(0, 100)) {
          lines.push(`- \`${name}\`: \`${String(version)}\``);
        }
      }

      lines.push("", `Source: [npm registry package metadata](${sourceUrl})`);
      return lines.join("\n").trim();
    },
  }],
  ["crates_io_readme", {
    eager: true,
    resolve: resolveCratesReadmeRequest,
    fetch: fetchCratesReadme,
  }],
]);

export function sparseIndexUrlForCrate(crateName) {
  const name = crateName.toLowerCase();
  if (name.length === 1) {
    return `https://index.crates.io/1/${name}`;
  }
  if (name.length === 2) {
    return `https://index.crates.io/2/${name}`;
  }
  if (name.length === 3) {
    return `https://index.crates.io/3/${name[0]}/${name}`;
  }
  return `https://index.crates.io/${name.slice(0, 2)}/${name.slice(2, 4)}/${name}`;
}

export function staticCratesReadmeUrl(crateName, version) {
  const encodedName = encodeURIComponent(crateName);
  return `https://static.crates.io/readmes/${encodedName}/${encodedName}-${encodeURIComponent(version)}.html`;
}

export function resolveCratesReadmeRequest(requestedUrl) {
  let parsed;
  try {
    parsed = new URL(requestedUrl);
  } catch {
    return null;
  }

  let pathname;
  try {
    pathname = decodeURIComponent(parsed.pathname);
  } catch {
    return null;
  }

  const match = pathname.match(/^\/crates\/([a-z0-9][a-z0-9_-]{0,63})\/?$/i);
  if (!match || parsed.search || parsed.hash) {
    return null;
  }

  const crateName = match[1].toLowerCase();
  return {
    crateName,
    indexUrl: sparseIndexUrlForCrate(crateName),
  };
}

function parseSemver(value) {
  const match = String(value).match(
    /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/,
  );
  if (!match) {
    return null;
  }
  return {
    raw: String(value),
    major: BigInt(match[1]),
    minor: BigInt(match[2]),
    patch: BigInt(match[3]),
    prerelease: match[4]?.split(".") ?? [],
  };
}

function comparePrerelease(left, right) {
  if (left.length === 0 || right.length === 0) {
    return left.length === right.length ? 0 : (left.length === 0 ? 1 : -1);
  }
  for (let index = 0; index < Math.max(left.length, right.length); index += 1) {
    if (left[index] === undefined || right[index] === undefined) {
      return left[index] === right[index] ? 0 : (left[index] === undefined ? -1 : 1);
    }
    const leftNumeric = /^\d+$/.test(left[index]);
    const rightNumeric = /^\d+$/.test(right[index]);
    if (leftNumeric && rightNumeric) {
      const difference = BigInt(left[index]) - BigInt(right[index]);
      if (difference !== 0n) {
        return difference > 0n ? 1 : -1;
      }
    } else if (leftNumeric !== rightNumeric) {
      return leftNumeric ? -1 : 1;
    } else if (left[index] !== right[index]) {
      return left[index] > right[index] ? 1 : -1;
    }
  }
  return 0;
}

function compareSemver(left, right) {
  for (const field of ["major", "minor", "patch"]) {
    if (left[field] !== right[field]) {
      return left[field] > right[field] ? 1 : -1;
    }
  }
  return comparePrerelease(left.prerelease, right.prerelease);
}

export function selectLatestNonYankedVersion(indexBody) {
  let latest = null;
  for (const line of String(indexBody).split(/\r?\n/)) {
    if (!line.trim()) {
      continue;
    }
    try {
      const entry = JSON.parse(line);
      if (entry?.yanked === true || typeof entry?.vers !== "string") {
        continue;
      }
      const version = parseSemver(entry.vers);
      if (version && (!latest || compareSemver(version, latest) > 0)) {
        latest = version;
      }
    } catch {
      return null;
    }
  }
  return latest?.raw ?? null;
}

export function renderCratesReadmeHtml(html, crateName, version) {
  if (typeof html !== "string" || !html.trim()) {
    return null;
  }
  const virtualConsole = new VirtualConsole();
  virtualConsole.on("jsdomError", () => {});
  const dom = new JSDOM(
    `<!doctype html><main id="crate-readme">${html}</main>`,
    { url: `https://crates.io/crates/${encodeURIComponent(crateName)}`, virtualConsole },
  );
  const root = dom.window.document.querySelector(CRATES_README_SELECTOR);
  if (!root || !(root.textContent ?? "").trim()) {
    return null;
  }

  const turndown = new TurndownService({ headingStyle: "atx", codeBlockStyle: "fenced" });
  addTableRule(turndown);
  const readme = turndown.turndown(root).trim();
  return readme ? `# ${crateName} ${version}\n\n${readme}` : null;
}

export function createSerialRateLimiter({
  intervalMs,
  now = () => Date.now(),
  wait = (delayMs) => new Promise((resolve) => setTimeout(resolve, delayMs)),
}) {
  let tail = Promise.resolve();
  let nextAllowedAt = 0;
  return {
    run(task) {
      const previous = tail;
      let release;
      tail = new Promise((resolve) => {
        release = resolve;
      });
      return previous.then(async () => {
        try {
          const delayMs = Math.max(0, nextAllowedAt - now());
          if (delayMs > 0) {
            await wait(delayMs);
          }
          nextAllowedAt = now() + intervalMs;
          return await task();
        } finally {
          release();
        }
      });
    },
  };
}

const cratesApiRateLimiter = createSerialRateLimiter({ intervalMs: 1_000 });

async function fetchPinned(url, init, consume) {
  let dispatcher;
  try {
    dispatcher = await pinnedDispatcherForUrl(url);
    const response = await egressFetch(url, {
      ...init,
      ...(dispatcher ? { dispatcher } : {}),
    });
    return await consume(response);
  } finally {
    await dispatcher?.destroy().catch(() => {});
  }
}

async function fetchCratesReadme({
  requestedUrl,
  resolution,
  maxResponseBytes,
  started,
}) {
  const indexBody = await fetchPinned(
    resolution.indexUrl,
    {
      headers: buildSourceAdapterFetchHeaders({}, "crates_io_readme", "application/json"),
      redirect: "error",
      signal: AbortSignal.timeout(30_000),
    },
    async (response) => {
      if (!response.ok) {
        await response.body?.cancel().catch(() => {});
        return null;
      }
      const body = await readResponseBodyForExtract(response, maxResponseBytes);
      return body.truncated ? null : body.html;
    },
  );
  if (!indexBody) {
    return null;
  }

  const version = selectLatestNonYankedVersion(indexBody);
  if (!version) {
    return null;
  }

  const fetchRendered = (sourceUrl) => fetchPinned(
    sourceUrl,
    {
      headers: buildSourceAdapterFetchHeaders({}, "crates_io_readme", "text/html"),
      redirect: "error",
      signal: AbortSignal.timeout(30_000),
    },
    async (response) => {
      if (!response.ok) {
        await response.body?.cancel().catch(() => {});
        return null;
      }
      const body = await readResponseBodyForExtract(response, maxResponseBytes);
      if (body.truncated) {
        return null;
      }
      const markdown = renderCratesReadmeHtml(body.html, resolution.crateName, version);
      return markdown ? {
        markdown,
        finalUrl: response.url,
        htmlLength: body.html.length,
        contentType: response.headers.get("content-type") ?? "text/html",
      } : null;
    },
  );

  const expectedStaticUrl = staticCratesReadmeUrl(resolution.crateName, version);
  let rendered = await fetchRendered(expectedStaticUrl);
  if (!rendered) {
    const apiUrl = `https://crates.io/api/v1/crates/${encodeURIComponent(resolution.crateName)}/${encodeURIComponent(version)}/readme`;
    const redirectedStaticUrl = await cratesApiRateLimiter.run(() => fetchPinned(
      apiUrl,
      {
        headers: buildSourceAdapterFetchHeaders({}, "crates_io_readme", "text/html"),
        redirect: "manual",
        signal: AbortSignal.timeout(30_000),
      },
      async (response) => {
        const location = response.headers.get("location");
        await response.body?.cancel().catch(() => {});
        if (![301, 302, 303, 307, 308].includes(response.status) || !location) {
          return null;
        }
        const target = new URL(location, apiUrl);
        return target.href === expectedStaticUrl ? target.href : null;
      },
    ));
    rendered = redirectedStaticUrl ? await fetchRendered(redirectedStaticUrl) : null;
  }
  if (!rendered) {
    return null;
  }

  return {
    ok: true,
    backend: "crates_io_readme",
    url: { requested: requestedUrl, final: rendered.finalUrl },
    title: `${resolution.crateName} ${version}`,
    markdown: rendered.markdown,
    text_length: rendered.markdown.length,
    html_length: rendered.htmlLength,
    content_type: rendered.contentType,
    latency_ms: Math.round(performance.now() - started),
  };
}

function appendField(lines, label, value) {
  if (typeof value === "string" && value.trim()) {
    lines.push(`- ${label}: ${value.trim()}`);
  }
}

function appendLinkField(lines, label, value) {
  if (typeof value === "string" && /^https?:\/\//i.test(value)) {
    lines.push(`- ${label}: [${value}](${value})`);
  }
}

function normalizeLicense(value) {
  if (typeof value === "string") {
    return value;
  }
  return typeof value?.type === "string" ? value.type : "";
}

function normalizeRepository(value) {
  const raw = typeof value === "string" ? value : value?.url;
  if (typeof raw !== "string") {
    return "";
  }
  return raw
    .replace(/^git\+/, "")
    .replace(/^git:\/\/github\.com\//i, "https://github.com/")
    .replace(/\.git$/i, "");
}

export function resolveSourceAdapter(requestedUrl) {
  const adapterName = loadSeedForUrl(requestedUrl)?.extract?.sourceAdapter;
  const adapter = adapters.get(adapterName);
  if (!adapter) {
    return null;
  }
  const resolution = adapter.resolve(requestedUrl);
  if (!resolution) {
    return null;
  }
  return {
    adapterName,
    eager: adapter.eager === true,
    resolution,
    sourceUrl: typeof resolution === "string" ? resolution : resolution.indexUrl,
    fetch: adapter.fetch,
    render: adapter.render,
  };
}

export function renderSourceAdapterPayload(adapterName, payload, sourceUrl) {
  return adapters.get(adapterName)?.render(payload, sourceUrl) ?? null;
}

export function buildSourceAdapterFetchHeaders(
  _requestHeaders = {},
  adapterName = "",
  accept = "application/json",
) {
  // Source adapters target public cross-origin APIs; caller/session headers must never follow.
  const defaults = getDefaultFetchHeaders();
  return {
    "User-Agent": adapterName === "crates_io_readme"
      ? CRATES_API_USER_AGENT
      : defaults.userAgent,
    Accept: accept,
  };
}

export function shouldTrySourceAdapterFirst(requestedUrl) {
  return resolveSourceAdapter(requestedUrl)?.eager === true;
}

export async function trySourceAdapter({
  requestedUrl,
  requestHeaders,
  maxResponseBytes,
  started,
}) {
  const resolved = resolveSourceAdapter(requestedUrl);
  if (!resolved) {
    return null;
  }

  if (typeof resolved.fetch === "function") {
    try {
      return await resolved.fetch({
        requestedUrl,
        resolution: resolved.resolution,
        maxResponseBytes,
        started,
      });
    } catch (error) {
      if (error instanceof SsrfBlockedError || error instanceof EgressProxyError) {
        return null;
      }
      return null;
    }
  }

  let dispatcher;
  try {
    dispatcher = await pinnedDispatcherForUrl(resolved.sourceUrl);
    const response = await egressFetch(resolved.sourceUrl, {
      headers: buildSourceAdapterFetchHeaders(requestHeaders),
      signal: AbortSignal.timeout(30_000),
      ...(dispatcher ? { dispatcher } : {}),
    });
    if (!response.ok) {
      await response.body?.cancel().catch(() => {});
      return null;
    }

    const body = await readResponseBodyForExtract(response, maxResponseBytes);
    if (body.truncated) {
      return null;
    }
    const payload = JSON.parse(body.html);
    const markdown = resolved.render(payload, response.url);
    if (!markdown) {
      return null;
    }

    return {
      ok: true,
      backend: resolved.adapterName,
      url: { requested: requestedUrl, final: response.url },
      title: payload.name ?? "",
      markdown,
      text_length: markdown.length,
      html_length: body.html.length,
      content_type: response.headers.get("content-type") ?? "application/json",
      latency_ms: Math.round(performance.now() - started),
    };
  } catch (error) {
    if (error instanceof SsrfBlockedError || error instanceof EgressProxyError) {
      return null;
    }
    return null;
  } finally {
    await dispatcher?.destroy().catch(() => {});
  }
}
