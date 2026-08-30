import { mergeFetchHeaders, stripCrossOriginSensitiveHeaders } from "./request-headers.mjs";
import { getDefaultFetchHeaders } from "./default-fetch-headers.mjs";
import { EgressProxyError, egressFetch } from "./egress-proxy.mjs";
import { loadSeedForUrl } from "./playbook-seed.mjs";
import { pinnedDispatcherForUrl, SsrfBlockedError } from "./private-ip.mjs";
import { readResponseBodyForExtract } from "./response-body-cap.mjs";

const adapters = new Map([
  ["npm_registry_package", {
    resolve(requestedUrl) {
      const parsed = new URL(requestedUrl);
      const match = decodeURIComponent(parsed.pathname).match(/^\/package\/((?:@[^/]+\/)?[^/]+)\/?$/);
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
]);

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
  const sourceUrl = adapter.resolve(requestedUrl);
  return sourceUrl ? { adapterName, sourceUrl, render: adapter.render } : null;
}

export function renderSourceAdapterPayload(adapterName, payload, sourceUrl) {
  return adapters.get(adapterName)?.render(payload, sourceUrl) ?? null;
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

  let dispatcher;
  try {
    dispatcher = await pinnedDispatcherForUrl(resolved.sourceUrl);
    const defaults = getDefaultFetchHeaders();
    const safeHeaders = stripCrossOriginSensitiveHeaders(
      requestHeaders,
      requestedUrl,
      resolved.sourceUrl);
    const response = await egressFetch(resolved.sourceUrl, {
      headers: mergeFetchHeaders(safeHeaders, {
        "User-Agent": defaults.userAgent,
        Accept: "application/json",
      }),
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
