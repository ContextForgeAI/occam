# Proxy and egress

**Slug:** `proxy-egress` · **Product system:** PS-1 Acquisition · **CAPs:** 11 · **Public relevance:** HIGH

**Member CAPs:** CAP-102, CAP-157–CAP-164, CAP-166, CAP-193  
**Product capability:** CAP-157  
**Engineering findings:** EF-007, EF-057

## What it is

Configuration of **static** and **rotating** HTTP/HTTPS/SOCKS proxies for **Node worker egress** (and Playwright launch proxy), plus explicit documentation of paths that **ignore** OCCAM proxy env. CAP-102 is the tool-facing proxy story; CAP-157 is the product capability.

## Why it exists

Let operators route worker traffic through corporate or residential proxies without baking proxy into Core C# HttpClients (probe/map/managed/search).

## User-visible entrypoints

| Surface | Honors OCCAM proxy? | Evidence |
|---------|---------------------|----------|
| http-extract / browser-extract via `egressFetch` / Playwright | **Yes** (static) | CAP-157/160 |
| css-extract fetch | **Yes** static via egressFetch | CAP-193 |
| Proxy rotation | Primary extract one-shot only | CAP-162/164 |
| Persistent HTTP daemon / browser pool / css spawn / dom-skeleton | Rotation **no**; static env may still apply unevenly | CAP-165 (network-safety / ABSENT) |
| Core C# HttpClients (probe, map, managed, search, robots) | **No** | CAP-166; **EF-007** |
| MCP param | **None** — env only | |

## Core behavior

### Static proxy (CAP-157)

Env: `OCCAM_HTTP_PROXY`, `OCCAM_HTTPS_PROXY` (falls back to HTTP if unset), `OCCAM_NO_PROXY`. Implemented in `workers/shared/lib/egress-proxy.mjs`. `egressFetch()` uses undici ProxyAgent / SOCKS as appropriate.

### Validation / failures (CAP-158)

Invalid proxy URL → typed failure; connect failures reclassified.

### NO_PROXY (CAP-159)

Exact / suffix / wildcard / global bypass matching.

### Playwright (CAP-160)

`resolvePlaywrightProxy` maps same env to `LaunchOptions.proxy` including embedded credentials. Wired in `browser-session.mjs`. Resolve failure → **fail-open null proxy** (GAP-030).

### Credential redaction (CAP-161)

Proxy URLs redacted in both C# and JS logs.

### Rotation (CAP-162–164)

- `OCCAM_PROXY_LIST` and/or `OCCAM_PROXY_LIST_FILE` (URL-per-line or scraper CSV — CAP-163).
- Round-robin per **one-shot spawn**.
- Forces SkipDaemon / one-shot (CAP-164) so rotation is not applied inside a long-lived daemon incorrectly.

## Advanced behavior

| CAP | Behavior |
|-----|----------|
| CAP-102 | Tool/docs surface for proxy knobs |
| CAP-193 | css-extract confirms proxy plumbing not HTML-only |
| Empty `OCCAM_PROXY_LIST_FILE` | Suppresses inline `OCCAM_PROXY_LIST` — **EF-057** / GAP-019 |

## Automatic / silent behavior

| Behavior | Impact |
|----------|--------|
| Rotation configured | Disables warm daemon for primary extracts (AUTOMATION #21) |
| Playwright proxy resolve fail | Silent direct egress (GAP-030) |
| Core paths | Always direct — surprising if operator set only OCCAM_*PROXY* |

## Parameters

None on MCP tools. Operators set environment before host start (spawn inherits env).

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_HTTP_PROXY` | unset | Worker HTTP proxy |
| `OCCAM_HTTPS_PROXY` | unset (→ HTTP) | HTTPS proxy |
| `OCCAM_NO_PROXY` | unset | Bypass list |
| `OCCAM_PROXY_LIST` | unset | Inline rotation list |
| `OCCAM_PROXY_LIST_FILE` | unset | File list; **empty file suppresses inline** (EF-057) |

`Workers/EgressProxyConfig.cs` compiles env for worker spawns — does not configure Core HttpClient `.Proxy`.

## Backends

Affects Node worker backends and Playwright launch. Does not wrap managed/provider HttpClients.

## Sessions / state

Rotation index is process-local. No durable proxy session store.

## Network behavior

Worker traffic may egress via proxy; Core traffic does not. SOCKS5 supported on worker path (CAP-157). Fail-open Playwright proxy defeats intended egress policy.

## Artifacts produced

None dedicated. Failures may surface as `network_error` / proxy-typed codes from worker validation (CAP-158).

## Trust / provenance properties

Proxy credentials must not leak into receipts/logs (CAP-161). Using a proxy does not add provenance — it changes egress identity.

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Bad proxy URL | Typed worker failure |
| Playwright resolve fail | null proxy (direct) |
| Empty list file | Inline list ignored (EF-057) |
| Rotation + daemon desire | One-shot forced |

## Platform differences

None beyond env inheritance and path to `_FILE`. SOCKS/HTTP agent behavior is Node-side.

## Composition with other capabilities

- Couples tightly with `http-acquisition` / `browser-acquisition` spawn mode.
- `network-safety` SSRF still applies on worker side after proxy (pin to resolved addresses).
- `managed-acquisition` bypasses this family entirely.

## Known limitations

- Name “egress” overstates coverage — Core HttpClients excluded (EF-007).
- Rotation does not reach pool/daemon/css/dom-skeleton spawns (CAP-165).
- Empty proxy list file footgun (EF-057).
- No MCP-level per-call proxy override.

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-007** | Core C# HttpClients never honor OCCAM_*PROXY* |
| **EF-057** | Empty `OCCAM_PROXY_LIST_FILE` suppresses inline list; (also LibreTranslate sync block — adjacent) |
| GAP-030 | Playwright proxy fail-open |

## Code evidence

- `workers/shared/lib/egress-proxy.mjs:1-217`
- `Services/ProxyRotationSettings.cs`
- `Workers/EgressProxyConfig.cs`
- `docs-audit/subsystems/network-fetch-proxy.md` §2
- `docs-audit/ACQUISITION-ROUTING-MODEL.md` Rung 1b

## Public-doc relevance

**HIGH** for operators. Must list which paths honor proxy and which do not (probe/managed/search).

## Handbook relevance

Operator networking: static vs rotation, daemon interaction, Core bypass warning.
