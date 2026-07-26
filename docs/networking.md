# Networking and proxies

Make network behavior **discoverable**. This page is intentionally honest about where proxy and SSRF controls apply — and where they do not.

**Status:** LIMITED (path-scoped)

## Proxy support

| Area | Proxy? | Notes |
|------|--------|-------|
| HTTP extract worker | Yes (env / egress helpers) | Respects configured HTTP(S) proxy settings used by the worker egress path |
| Browser extract | Partial | Browser traffic follows Playwright/browser proxy configuration when set; not identical to every HTTP helper |
| CSS / schema extract | Check worker path | Shares network-safety helpers on the hardened path; do not assume every Core `HttpClient` uses the same knobs |
| Managed providers | Provider-dependent | Third-party egress; Occam’s local proxy rotation is not “fingerprint rotation” |
| Search / translation / TSA | Separate clients | May not share the extract-worker proxy policy |

**Proxy rotation ≠ browser fingerprint rotation.** Rotating egress IPs does not change TLS/JA3 fingerprints or solve anti-bot systems.

### Proxy rotation (operator)

Some installs configure a **list** of proxies with rotation for HTTP egress. Rotation is incomplete for every daemon/browser path — do not assume the browser worker, CSS path, or managed provider share the same rotator. Prefer verifying the path you use against [Configuration](configuration.md). Empty proxy-file configurations can suppress an inline list (operator footgun).

## Automatic consent dismiss

On browser extracts, Occam may **silently dismiss common cookie-consent overlays**. This is not anti-bot bypass and not configurable as a first-class MCP feature. Treat it as automatic behavior that can change which DOM is visible — see handbook automatic-behaviors chapter.

## SSRF / private IP

- HTTP and browser acquisition preflight and worker DNS-pinning reject private / link-local targets unless explicitly allowed by operator policy.  
- CSS extract was brought to parity for private-IP rejection and response body caps.  
- Some auxiliary Core HTTP clients and managed/search paths do **not** share one universal guard — do not claim “every outbound call is SSRF-safe identically.”

## What to configure

See [Configuration](configuration.md) for `OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY` and related egress variables. Prefer documenting the variable you set against the path you use (HTTP read vs browser vs managed).

## Related

- [Acquisition](acquisition.md)
- [Trust / local-first](trust/local-first.md)
- [Configuration](configuration.md)
