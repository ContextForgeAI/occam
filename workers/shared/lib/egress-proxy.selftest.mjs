import assert from "node:assert/strict";
import {
  EgressProxyError,
  readEgressConfig,
  redactProxyUrl,
  resolvePlaywrightProxy,
  resolveProxyForUrl,
  shouldBypassProxy,
  validateProxyUrl,
} from "./egress-proxy.mjs";

assert.deepEqual(validateProxyUrl("http://127.0.0.1:8080"), { ok: true, server: "http://127.0.0.1:8080" });
assert.deepEqual(validateProxyUrl("ftp://proxy.corp"), { ok: false, failure: "invalid_proxy_url" });
assert.deepEqual(validateProxyUrl("not-a-url"), { ok: false, failure: "invalid_proxy_url" });

assert.equal(shouldBypassProxy("localhost", ["localhost", "127.0.0.1"]), true);
assert.equal(shouldBypassProxy("api.internal.corp", [".corp"]), true);
assert.equal(shouldBypassProxy("developer.mozilla.org", ["localhost"]), false);

const config = {
  httpProxy: "http://127.0.0.1:8080",
  httpsProxy: "http://127.0.0.1:8080",
  noProxy: ["developer.mozilla.org"],
};
assert.equal(resolveProxyForUrl("https://developer.mozilla.org/en-US/", config), null);
assert.equal(resolveProxyForUrl("https://nginx.org/en/docs/", config), "http://127.0.0.1:8080");

assert.match(redactProxyUrl("http://user:secret@proxy.corp:8080"), /\*\*\*/);
assert.doesNotMatch(redactProxyUrl("http://proxy.corp:8080"), /\*\*\*/);

const prevHttp = process.env.OCCAM_HTTP_PROXY;
const prevHttps = process.env.OCCAM_HTTPS_PROXY;
const prevNoProxy = process.env.OCCAM_NO_PROXY;
process.env.OCCAM_HTTP_PROXY = "http://127.0.0.1:3128";
process.env.OCCAM_HTTPS_PROXY = "";
process.env.OCCAM_NO_PROXY = "localhost,127.0.0.1";
const envConfig = readEgressConfig();
assert.equal(envConfig.httpProxy, "http://127.0.0.1:3128");
assert.equal(envConfig.httpsProxy, "http://127.0.0.1:3128");
assert.deepEqual(envConfig.noProxy, ["localhost", "127.0.0.1"]);
const playwrightProxy = resolvePlaywrightProxy();
assert.ok(playwrightProxy);
assert.equal(playwrightProxy?.server, "http://127.0.0.1:3128");
assert.equal(playwrightProxy?.bypass, "localhost,127.0.0.1");
if (prevHttp === undefined) {
  delete process.env.OCCAM_HTTP_PROXY;
} else {
  process.env.OCCAM_HTTP_PROXY = prevHttp;
}
if (prevHttps === undefined) {
  delete process.env.OCCAM_HTTPS_PROXY;
} else {
  process.env.OCCAM_HTTPS_PROXY = prevHttps;
}
if (prevNoProxy === undefined) {
  delete process.env.OCCAM_NO_PROXY;
} else {
  process.env.OCCAM_NO_PROXY = prevNoProxy;
}

const err = new EgressProxyError("invalid_proxy_url");
assert.equal(err.code, "invalid_proxy_url");

console.log("egress-proxy.selftest: OK");
