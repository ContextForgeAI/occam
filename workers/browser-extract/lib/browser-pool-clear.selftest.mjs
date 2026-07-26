import assert from "node:assert/strict";
import { clearAnonymousContextState } from "./browser-pool.mjs";

/** Minimal fake context used when Playwright is unavailable in unit selftest. */
function makeFakeContext() {
  let cookies = [{ name: "a", value: "1", domain: "example.com", path: "/" }];
  let storageCalls = 0;
  return {
    cookies,
    get storageCalls() {
      return storageCalls;
    },
    async setStorageState(state) {
      storageCalls += 1;
      assert.deepEqual(state, { cookies: [], origins: [] });
      cookies = [];
    },
    async clearCookies() {
      cookies = [];
    },
    async clearPermissions() {},
    async cookiesList() {
      return cookies;
    },
  };
}

const ctx = makeFakeContext();
await clearAnonymousContextState(ctx);
assert.equal(ctx.storageCalls, 1);
assert.deepEqual(await ctx.cookiesList(), []);

// Fallback path when setStorageState missing: clearCookies still runs.
const fallback = {
  cleared: false,
  async clearCookies() {
    this.cleared = true;
  },
  async clearPermissions() {},
};
await clearAnonymousContextState(fallback);
assert.equal(fallback.cleared, true);

await clearAnonymousContextState(null);

console.log("browser-pool-clear.selftest: OK");
