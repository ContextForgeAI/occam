import assert from "node:assert/strict";
import {
  cookieMatchesHost,
  filterCookiesForUrl,
  harvestCookiesForRetry,
  toCookieHeader,
} from "./harvest-cookies.mjs";

assert.equal(cookieMatchesHost("stackoverflow.com", "stackoverflow.com"), true);
assert.equal(cookieMatchesHost(".stackoverflow.com", "stackoverflow.com"), true);
assert.equal(cookieMatchesHost(".stackoverflow.com", "meta.stackoverflow.com"), true);
assert.equal(cookieMatchesHost("cloudflare.com", "stackoverflow.com"), false);
assert.equal(cookieMatchesHost(".cloudflare.com", "stackoverflow.com"), false);

const pageUrl = "https://stackoverflow.com/questions/1";
const scoped = filterCookiesForUrl(
  [
    { name: "prov", value: "abc", domain: ".stackoverflow.com" },
    { name: "cf_clearance", value: "tok", domain: ".cloudflare.com" },
    { name: "prov", value: "dup", domain: "stackoverflow.com" },
    { name: "", value: "x", domain: "stackoverflow.com" },
  ],
  pageUrl,
);
assert.deepEqual(
  scoped.map((cookie) => cookie.name),
  ["prov"],
  "third-party and duplicate cookie names must be dropped",
);
assert.equal(toCookieHeader(scoped), "prov=abc");

const harvested = await harvestCookiesForRetry(
  {
    cookies: async () => [
      { name: "sid", value: "1", domain: "example.test" },
      { name: "tracker", value: "x", domain: "ads.example.net" },
    ],
  },
  "https://example.test/q/1",
  "https://example.test/q/1",
);
assert.equal(harvested.count, 1);
assert.equal(harvested.header, "sid=1");

const empty = await harvestCookiesForRetry({}, "https://example.test/");
assert.equal(empty.count, 0);
assert.equal(empty.header, "");

console.log("harvest-cookies.selftest: OK");
