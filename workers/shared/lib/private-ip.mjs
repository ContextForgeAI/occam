/**
 * Private IP address detection - mirrors C# PrivacyClassifier.IsPrivateIp() logic
 * Used for SSRF/DNS rebinding protection
 */

/**
 * Checks if an IP address is private (RFC 1918, loopback, link-local, etc.)
 * @param {string} ipString - IP address string (IPv4 or IPv6)
 * @returns {boolean} true if private, false otherwise
 */
export function isPrivateIp(ipString) {
  if (!ipString) return false;
  
  try {
    // Handle IPv6 addresses in brackets
    const ip = ipString.replace(/^\[|\]$/g, '');
    
    // Check IPv4
    if (/^\d+\.\d+\.\d+\.\d+$/.test(ip)) {
      const parts = ip.split('.').map(Number);
      if (parts.length !== 4) return false;
      
      const [a, b, c, d] = parts;

      // "This host on this network" (0.0.0.0/8) — routes to localhost on Linux; an SSRF vector.
      if (a === 0) return true;

      // Loopback (127.0.0.0/8)
      if (a === 127) return true;

      // RFC 1918 private ranges
      // 10.0.0.0/8
      if (a === 10) return true;
      // 172.16.0.0/12
      if (a === 172 && b >= 16 && b <= 31) return true;
      // 192.168.0.0/16
      if (a === 192 && b === 168) return true;
      // Link-local (169.254.0.0/16)
      if (a === 169 && b === 254) return true;
      
      return false;
    }
    
    // Check IPv6
    if (ip.includes(':')) {
      // IPv4-mapped IPv6 (::ffff:a.b.c.d or its hex ::ffff:AABB:CCDD form) — validate the
      // embedded IPv4 so ::ffff:127.0.0.1 / ::ffff:169.254.169.254 can't slip past the v6 checks.
      const mappedDotted = ip.match(/^::ffff:(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})$/i);
      if (mappedDotted) return isPrivateIp(mappedDotted[1]);
      const mappedHex = ip.match(/^::ffff:([0-9a-f]{1,4}):([0-9a-f]{1,4})$/i);
      if (mappedHex) {
        const hi = parseInt(mappedHex[1], 16);
        const lo = parseInt(mappedHex[2], 16);
        return isPrivateIp(`${(hi >> 8) & 255}.${hi & 255}.${(lo >> 8) & 255}.${lo & 255}`);
      }

      // IPv6 loopback (::1/128)
      if (ip === '::1' || ip === '0:0:0:0:0:0:0:1') return true;
      
      // IPv6 link-local (fe80::/10)
      if (ip.startsWith('fe80:') || ip.startsWith('fe80::') || ip.startsWith('fe80:0:') || ip.startsWith('fe80:0000:')) {
        return true;
      }
      
      // IPv6 site-local (fec0::/10) - deprecated but still used
      if (ip.startsWith('fec0:') || ip.startsWith('fec0::')) {
        return true;
      }
      
      // IPv6 unique local (fc00::/7) - RFC 4193
      if (ip.startsWith('fc') || ip.startsWith('fd')) {
        return true;
      }
      
      // IPv6 loopback in various forms
      if (ip === '::' || ip.startsWith('0:0:0:0:0:0:0:')) return true;
      
      return false;
    }
    
    return false;
  } catch {
    return false;
  }
}

/**
 * Checks if private URLs are allowed via environment variable
 * Mirrors C# PrivacyClassifier.IsPrivateUrlBlocked()
 * @returns {boolean} true if private URLs are blocked, false if allowed
 */
export function isPrivateUrlBlocked() {
  return process.env.OCCAM_ALLOW_PRIVATE_URLS !== "1";
}

/**
 * Checks if private IP checks should be skipped
 * @returns {boolean} true if private IP checks should be skipped
 */
export function shouldSkipPrivateIpCheck() {
  return process.env.OCCAM_ALLOW_PRIVATE_URLS === "1";
}

/**
 * SSRF block — thrown by resolveAndValidateHost when a host resolves to a private
 * address or cannot be resolved. `.failure` carries the worker failure code.
 */
export class SsrfBlockedError extends Error {
  /** @param {"private_ip_blocked" | "dns_resolution_failed"} failure */
  constructor(failure, message = failure) {
    super(message);
    this.name = "SsrfBlockedError";
    this.failure = failure;
  }
}

/**
 * Resolves a hostname across BOTH address families and validates every returned
 * address against the private-IP policy. This is the SSRF/DNS-rebinding enforcement
 * boundary for the workers — IPv4 *and* IPv6 (e.g. ::1, fc00::/7) are checked, fixing
 * the prior IPv4-only gap. Returns the validated records so the caller can pin the
 * connection to exactly these addresses (closing the TOCTOU re-resolution window).
 *
 * @param {string} hostname
 * @returns {Promise<Array<{ address: string, family: number }>>}
 * @throws {SsrfBlockedError} private_ip_blocked | dns_resolution_failed
 */
export async function resolveAndValidateHost(hostname, { allowPrivate = false } = {}) {
  const dns = await import("node:dns").then((m) => m.promises);
  let records;
  try {
    // family:0 (default) → getaddrinfo returns both A and AAAA records.
    records = await dns.lookup(hostname, { all: true, family: 0 });
  } catch {
    throw new SsrfBlockedError("dns_resolution_failed");
  }
  if (!records || records.length === 0) {
    throw new SsrfBlockedError("dns_resolution_failed");
  }
  // `allowPrivate` (OCCAM_ALLOW_PRIVATE_URLS=1, local testing) relaxes ONLY the private-IP rejection — it
  // does NOT skip resolution or pinning. So the connection is still pinned to the resolved addresses in every
  // configuration, which is what real users run and what the gate must therefore exercise.
  if (!allowPrivate) {
    for (const { address } of records) {
      if (isPrivateIp(address)) {
        throw new SsrfBlockedError("private_ip_blocked");
      }
    }
  }
  return records;
}

/**
 * Builds an undici Agent whose connection lookup is pinned to the already-validated
 * addresses, so the socket connects to exactly the IPs we checked even if authoritative
 * DNS would now return a different (private) answer. Kills the DNS-rebinding TOCTOU window
 * on the direct (non-proxied) fetch path. Pass via `egressFetch(url, { dispatcher })`.
 *
 * Host-aware: the pins apply ONLY to the host they were validated for. When the request
 * follows a redirect to a DIFFERENT host, undici calls this lookup with the new hostname —
 * we re-resolve and SSRF-validate that host on the fly instead of blindly returning the
 * original host's IPs. Returning the wrong host's IP caused TLS cert SAN mismatch
 * (ERR_TLS_CERT_ALTNAME_INVALID) on cross-host redirects, e.g. oracle.com → www.oracle.com
 * (Q-004); reusing pins across hosts would also have bypassed SSRF validation on the new host.
 *
 * @param {string} hostname  the host the records were validated for
 * @param {Array<{ address: string, family: number }>} records
 * @returns {Promise<import('undici').Agent>}
 */
export async function createPinnedDispatcher(hostname, records, { allowPrivate = false } = {}) {
  const { Agent } = await import("undici");
  return new Agent({ connect: { lookup: createPinnedLookup(hostname, records, { allowPrivate }) } });
}

/**
 * Builds the host-aware net.lookup-compatible function used by the pinned dispatcher.
 * Exported for unit testing. Same host → validated pins (no fresh DNS, kills TOCTOU);
 * different host (redirect target) → resolve + SSRF-validate it on the fly.
 *
 * @param {string} hostname
 * @param {Array<{ address: string, family: number }>} records
 */
export function createPinnedLookup(hostname, records, { allowPrivate = false } = {}) {
  const validatedHost = String(hostname || "").toLowerCase();
  const pinned = records.map((r) => ({ address: r.address, family: r.family }));
  const respond = (set, options, callback) =>
    options && options.all
      ? callback(null, set)
      : callback(null, set[0].address, set[0].family);
  return function lookup(host, options, callback) {
    if (String(host || "").toLowerCase() === validatedHost) {
      respond(pinned, options, callback);
      return;
    }
    // Redirect to a different host: re-resolve + apply the SAME private-IP policy as the original request
    // (so a redirect can't smuggle in a private target under default policy, and local testing still works).
    resolveAndValidateHost(host, { allowPrivate })
      .then((recs) => respond(recs.map((r) => ({ address: r.address, family: r.family })), options, callback))
      .catch((err) => callback(err));
  };
}

/**
 * SSRF guard for a fetch target given as a URL (or bare hostname): resolves + validates the
 * host across both address families and returns a pinned undici dispatcher for it, or
 * `undefined` when private URLs are explicitly allowed (local testing). Throws SsrfBlockedError
 * when the host resolves to a private address or cannot be resolved.
 *
 * Use this for EVERY fetch whose target comes from untrusted input — including application-level
 * redirects (HTML meta-refresh, JS location) that bypass undici's guarded HTTP-3xx path. Sharing
 * one helper keeps the redirect paths from silently drifting out of the SSRF policy.
 *
 * @param {string} urlOrHostname
 * @returns {Promise<import('undici').Agent | undefined>}
 * @throws {SsrfBlockedError}
 */
export async function pinnedDispatcherForUrl(urlOrHostname) {
  // Always resolve + pin. The flag only relaxes the private-IP rejection (allowPrivate), it does NOT turn
  // pinning off — otherwise a whole configuration (and the CI gate) would silently run the un-pinned path.
  const allowPrivate = shouldSkipPrivateIpCheck();
  const hostname = /:\/\//.test(urlOrHostname) ? new URL(urlOrHostname).hostname : urlOrHostname;
  const records = await resolveAndValidateHost(hostname, { allowPrivate });
  return createPinnedDispatcher(hostname, records, { allowPrivate });
}

/**
 * Validates a URL against private IP/host policy
 * @param {string} url - URL to validate
 * @returns {boolean} true if URL is allowed, false if blocked
 */
export function isUrlAllowed(url) {
  if (!url) return false;
  
  // If private URLs are allowed via env var, skip all checks
  if (!isPrivateUrlBlocked()) {
    return true;
  }
  
  try {
    const parsed = new URL(url);
    const hostname = parsed.hostname;
    
    // Check for localhost, .local, .internal
    if (hostname === "localhost" || 
        hostname.endsWith(".local") || 
        hostname.endsWith(".internal")) {
      return false;
    }
    
    // Check if hostname is an IP address
    if (/^\d+\.\d+\.\d+\.\d+$/.test(hostname) || /^\[?[0-9a-fA-F:]+\]?$/.test(hostname)) {
      if (isPrivateIp(hostname.replace(/[\[\]]/g, ''))) {
        return false;
      }
    }
    
    return true;
  } catch {
    return false;
  }
}