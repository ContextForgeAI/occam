import { readFile } from "node:fs/promises";
import { extractStructuredData } from "./lib/css-schema-extract.mjs";
import { EgressProxyError, egressFetch } from "../shared/lib/egress-proxy.mjs";
import { getDefaultFetchHeaders } from "../shared/lib/default-fetch-headers.mjs";

const url = process.argv[2];
const fieldsPath = process.argv[3];
const extraArgs = process.argv.slice(4);
const htmlFileArg = extraArgs.find((arg) => arg.startsWith("--html-file="));
const finalUrlArg = extraArgs.find((arg) => arg.startsWith("--final-url="));
const headersFileArg = extraArgs.find((arg) => arg.startsWith("--headers-file="));
const browserFallback = extraArgs.includes("--browser-fallback");

if (!url || !fieldsPath) {
  console.error("Usage: node css-extract.mjs <url> <fields.json> [--html-file=path] [--final-url=url] [--headers-file=path] [--browser-fallback]");
  process.exit(1);
}

const started = performance.now();

try {
  const spec = JSON.parse(await readFile(fieldsPath, "utf8"));
  let html;
  let finalUrl = finalUrlArg?.slice("--final-url=".length).replace(/^"|"$/g, "") ?? url;
  let backend = "css_extract_http";

  if (htmlFileArg) {
    const filePath = htmlFileArg.slice("--html-file=".length).replace(/^"|"$/g, "");
    html = await readFile(filePath, "utf8");
  } else {
    const headersFile = headersFileArg?.slice("--headers-file=".length);
    let requestHeaders = {};
    if (headersFile) {
      const raw = await readFile(headersFile.replace(/^"|"$/g, ""), "utf8");
      requestHeaders = JSON.parse(raw);
    }

    const defaults = getDefaultFetchHeaders();
    const response = await egressFetch(url, {
      headers: {
        ...requestHeaders,
        "User-Agent": defaults.userAgent,
        Accept: defaults.accept,
      },
      signal: AbortSignal.timeout(45_000),
    });

    if (!response.ok) {
      const blocked = response.status === 401 || response.status === 403 || response.status === 429;
      if (browserFallback && blocked) {
        const browserHtml = await fetchHtmlViaBrowser(url);
        if (!browserHtml?.html) {
          console.log(
            JSON.stringify({
              ok: false,
              backend: "css_extract_browser",
              failure: browserHtml?.failure ?? "browser_unavailable",
              latency_ms: Math.round(performance.now() - started),
            }),
          );
          process.exit(0);
        }
        html = browserHtml.html;
        finalUrl = browserHtml.finalUrl;
        backend = "css_extract_browser";
      } else {
        console.log(
          JSON.stringify({
            ok: false,
            backend: "css_extract_http",
            failure: `http_${response.status}`,
            latency_ms: Math.round(performance.now() - started),
          }),
        );
        process.exit(0);
      }
    } else {
      html = await response.text();
      finalUrl = response.url;
    }
  }

  const data = extractStructuredData(html, finalUrl, spec);
  console.log(
    JSON.stringify({
      ok: true,
      backend,
      url: { requested: url, final: finalUrl },
      data,
      html_length: html.length,
      latency_ms: Math.round(performance.now() - started),
    }),
  );
} catch (error) {
  const failure =
    error instanceof EgressProxyError
      ? error.code
      : error?.name === "TimeoutError"
        ? "timeout"
        : "extraction_failed";
  console.log(
    JSON.stringify({
      ok: false,
      backend: browserFallback ? "css_extract_browser" : "css_extract_http",
      failure,
      latency_ms: Math.round(performance.now() - started),
    }),
  );
}

async function fetchHtmlViaBrowser(targetUrl) {
  try {
    const { createBrowserSession } = await import("../browser-extract/lib/browser-session.mjs");
    const session = await createBrowserSession();
    try {
      const page = await session.context.newPage();
      await page.goto(targetUrl, { waitUntil: "domcontentloaded", timeout: 45_000 });
      await page.waitForTimeout(1500);
      return { html: await page.content(), finalUrl: page.url() };
    } finally {
      await session.close();
    }
  } catch (error) {
    return {
      failure: error?.message?.includes("playwright") ? "browser_unavailable" : "browser_failed",
    };
  }
}
