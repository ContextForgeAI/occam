// Self-test for the SSRF / private-IP guard. Run: node workers/shared/lib/private-ip.selftest.mjs
// Wired (non-fatal) into occam-doctor. Focus: the both-families + pinning hardening.
import { isPrivateIp, resolveAndValidateHost, createPinnedDispatcher, createPinnedLookup, pinnedDispatcherForUrl, SsrfBlockedError } from "./private-ip.mjs";

let failures = 0;
function check(name, cond) {
  if (cond) {
    console.log(`  ok  ${name}`);
  } else {
    failures += 1;
    console.error(`FAIL  ${name}`);
  }
}

// IPv4 private ranges
check("127.0.0.1 is private", isPrivateIp("127.0.0.1"));
check("10.0.0.5 is private", isPrivateIp("10.0.0.5"));
check("172.16.0.1 is private", isPrivateIp("172.16.0.1"));
check("192.168.1.1 is private", isPrivateIp("192.168.1.1"));
check("169.254.1.1 is private", isPrivateIp("169.254.1.1"));
check("0.0.0.0 is private", isPrivateIp("0.0.0.0"));
check("0.1.2.3 (0.0.0.0/8) is private", isPrivateIp("0.1.2.3"));
check("8.8.8.8 is public", !isPrivateIp("8.8.8.8"));

// IPv4-mapped IPv6 must fold to the embedded IPv4 (SSRF bypass guard)
check("::ffff:127.0.0.1 is private", isPrivateIp("::ffff:127.0.0.1"));
check("::ffff:169.254.169.254 is private", isPrivateIp("::ffff:169.254.169.254"));
check("::ffff:7f00:1 (hex-mapped loopback) is private", isPrivateIp("::ffff:7f00:1"));
check("::ffff:8.8.8.8 is public", !isPrivateIp("::ffff:8.8.8.8"));

// IPv6 — the previously-unchecked family (regression guard for the IPv4-only bug)
check("::1 is private", isPrivateIp("::1"));
check("fc00:: is private", isPrivateIp("fc00::1"));
check("fd12:3456::1 is private", isPrivateIp("fd12:3456::1"));
check("fe80:: link-local is private", isPrivateIp("fe80::1"));
check("2606:4700:4700::1111 is public", !isPrivateIp("2606:4700:4700::1111"));

// resolveAndValidateHost: a literal private IP host must be rejected with the typed code
let blocked = null;
try {
  await resolveAndValidateHost("127.0.0.1");
} catch (e) {
  blocked = e;
}
check("resolveAndValidateHost(127.0.0.1) throws SsrfBlockedError", blocked instanceof SsrfBlockedError);
check("blocked code is private_ip_blocked", blocked?.failure === "private_ip_blocked");

// createPinnedDispatcher: returns a usable undici Agent (dispatcher) over the validated records
const dispatcher = await createPinnedDispatcher("example.com", [{ address: "93.184.216.34", family: 4 }]);
check("createPinnedDispatcher returns a dispatcher", typeof dispatcher?.dispatch === "function");
check("pinned dispatcher is closeable", typeof dispatcher?.close === "function");
await dispatcher.close().catch(() => {});

// Q-004: host-aware lookup. Same host → validated pin; different host → NOT the pinned IP
// (returning it caused ERR_TLS_CERT_ALTNAME_INVALID on cross-host redirects like oracle.com→www.oracle.com).
const lookup = createPinnedLookup("example.com", [{ address: "93.184.216.34", family: 4 }]);
const sameAll = await new Promise((res) => lookup("example.com", { all: true }, (e, v) => res(e ? "err" : v)));
check("pinned lookup same-host returns the pin", Array.isArray(sameAll) && sameAll[0].address === "93.184.216.34");
const sameOne = await new Promise((res) => lookup("EXAMPLE.COM", {}, (e, addr) => res(e ? "err" : addr)));
check("pinned lookup is case-insensitive on host", sameOne === "93.184.216.34");
const diff = await new Promise((res) =>
  lookup("no-such-host.invalid", { all: true }, (e, v) => res(e ? "err" : (Array.isArray(v) && v[0].address))));
check("pinned lookup does NOT reuse pin for a different host", diff !== "93.184.216.34");

// pinnedDispatcherForUrl: the shared SSRF guard for redirect targets (meta-refresh etc.).
// A target URL whose host is a private literal IP must be rejected before any fetch.
let refreshBlocked = null;
try {
  await pinnedDispatcherForUrl("http://169.254.169.254/latest/meta-data/");
} catch (e) {
  refreshBlocked = e;
}
check("pinnedDispatcherForUrl blocks private redirect target", refreshBlocked instanceof SsrfBlockedError);
check("blocked redirect code is private_ip_blocked", refreshBlocked?.failure === "private_ip_blocked");

let mappedBlocked = null;
try {
  await pinnedDispatcherForUrl("http://[::ffff:169.254.169.254]/");
} catch (e) {
  mappedBlocked = e;
}
check("pinnedDispatcherForUrl blocks IPv4-mapped redirect target", mappedBlocked instanceof SsrfBlockedError);

// OCCAM_ALLOW_PRIVATE_URLS=1 relaxes ONLY the private-IP rejection — it must STILL pin (not return undefined),
// so the pinned path is exercised in every configuration incl. the CI gate. A private target now resolves +
// pins instead of being skipped.
const priorAllow = process.env.OCCAM_ALLOW_PRIVATE_URLS;
process.env.OCCAM_ALLOW_PRIVATE_URLS = "1";
const allowedDispatcher = await pinnedDispatcherForUrl("http://127.0.0.1/");
check("OCCAM_ALLOW_PRIVATE_URLS=1 still pins a private target (not undefined)", allowedDispatcher !== undefined && typeof allowedDispatcher.close === "function");
await allowedDispatcher?.close?.().catch(() => {});
if (priorAllow === undefined) delete process.env.OCCAM_ALLOW_PRIVATE_URLS;
else process.env.OCCAM_ALLOW_PRIVATE_URLS = priorAllow;

if (failures > 0) {
  console.error(`private-ip selftest: ${failures} failure(s)`);
  process.exit(1);
}
console.log("private-ip selftest: all checks passed");
