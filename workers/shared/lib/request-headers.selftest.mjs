// Self-test for cross-origin credential stripping on redirect targets (meta-refresh etc.),
// browser extraHTTPHeaders credential blocking (F1), and applySessionCookies arg order (F3).
// Run: node workers/shared/lib/request-headers.selftest.mjs
import { stripCrossOriginSensitiveHeaders, pickExtraHttpHeaders } from "./request-headers.mjs";
import { applySessionCookies } from "../../browser-extract/lib/session-headers.mjs";

let failures = 0;
function check(name, cond) {
  if (cond) {
    console.log(`  ok  ${name}`);
  } else {
    failures += 1;
    console.error(`FAIL  ${name}`);
  }
}

const creds = {
  Cookie: "sid=secret",
  Authorization: "Bearer token",
  "X-Custom": "keep-me",
  "User-Agent": "occam",
};

// Same origin → headers untouched (credentials survive to the same site).
const same = stripCrossOriginSensitiveHeaders(creds, "https://a.com/page", "https://a.com/other");
check("same-origin keeps Cookie", same.Cookie === "sid=secret");
check("same-origin keeps Authorization", same.Authorization === "Bearer token");

// Different host → strip credentials, keep the rest.
const crossHost = stripCrossOriginSensitiveHeaders(creds, "https://a.com/page", "https://b.com/");
check("cross-host strips Cookie", crossHost.Cookie === undefined);
check("cross-host strips Authorization", crossHost.Authorization === undefined);
check("cross-host keeps X-Custom", crossHost["X-Custom"] === "keep-me");
check("cross-host keeps User-Agent", crossHost["User-Agent"] === "occam");

// Scheme downgrade on the same host is cross-origin → strip (credentials over http).
const downgrade = stripCrossOriginSensitiveHeaders(creds, "https://a.com/", "http://a.com/");
check("scheme downgrade strips Cookie", downgrade.Cookie === undefined);

// Different port is cross-origin → strip.
const port = stripCrossOriginSensitiveHeaders(creds, "https://a.com/", "https://a.com:8443/");
check("different port strips Authorization", port.Authorization === undefined);

// Case-insensitive header names are stripped too.
const lower = stripCrossOriginSensitiveHeaders(
  { cookie: "x=1", authorization: "Bearer y" },
  "https://a.com/",
  "https://b.com/",
);
check("cross-host strips lowercase cookie/authorization", lower.cookie === undefined && lower.authorization === undefined);

// Unparseable target → fail safe (strip).
const bad = stripCrossOriginSensitiveHeaders(creds, "https://a.com/", "not a url");
check("unparseable target fails safe (strips Cookie)", bad.Cookie === undefined);

// F1 — pickExtraHttpHeaders must not leak credential headers into Playwright extraHTTPHeaders
// (static per-context, attach cross-origin with no origin filter).
const extra = pickExtraHttpHeaders({
  Authorization: "Bearer token",
  "Proxy-Authorization": "Basic zzz",
  Cookie: "sid=secret",
  "X-Custom": "keep-me",
});
check("extra blocks Authorization", extra.Authorization === undefined);
check("extra blocks Proxy-Authorization", extra["Proxy-Authorization"] === undefined);
check("extra blocks Cookie", extra.Cookie === undefined);
check("extra keeps X-Custom", extra["X-Custom"] === "keep-me");
const extraLower = pickExtraHttpHeaders({ authorization: "Bearer y" });
check("extra blocks lowercase authorization", extraLower.authorization === undefined);

// F3 — applySessionCookies signature is (context, url, headers); the Cookie must be read off the
// 3rd arg. A fake context captures addCookies() calls.
let captured = null;
const fakeContext = { addCookies: async (cookies) => { captured = cookies; } };
const res = await applySessionCookies(fakeContext, "https://a.com/page", { Cookie: "sid=abc" });
check("applySessionCookies reads Cookie from headers arg", res.cookiesAdded === 1 && captured?.[0]?.name === "sid");
// Guard against the swapped-arg regression: URL passed as the headers arg → no cookie found.
const swapped = await applySessionCookies(fakeContext, "https://a.com/page", "https://a.com/page");
check("applySessionCookies with non-object headers adds nothing", swapped.cookiesAdded === 0);

if (failures > 0) {
  console.error(`request-headers selftest: ${failures} failure(s)`);
  process.exit(1);
}
console.log("request-headers cross-origin strip selftest: OK");
