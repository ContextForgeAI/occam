# Occam demo gateway prototype

This directory is a separate, non-shipping deployment prototype for a bounded
public Occam demonstration. It is not part of Occam Core, is not an MCP tool,
and is not linked from the public documentation until a deployment passes the
security, cost, and operational gates below.

## Goal

Let a visitor submit one public HTTP(S) URL and see a compact, source-linked
Occam result without giving an anonymous service access to browser sessions,
cookies, custom headers, proxies, search providers, or private networks.

## Architecture

```text
browser
  -> demo HTTP boundary
     -> origin + rate + concurrency policy
        -> long-lived local MCP stdio session
           -> occam_transcode (HTTP only, bounded tokens)
              -> existing pinned SSRF-safe worker path
```

The gateway performs an early hostname/DNS policy check. The existing Occam
worker remains the authoritative outbound boundary: it validates and pins the
actual connection and revalidates redirect targets. The gateway never sets
`OCCAM_ALLOW_PRIVATE_URLS=1`.

## API

```http
POST /v1/transcode
Content-Type: application/json

{"url":"https://example.com/"}
```

Success returns a deliberately small projection:

```json
{
  "ok": true,
  "url": {
    "url": "https://example.com/",
    "finalUrl": "https://example.com/"
  },
  "markdown": "# Example Domain\n...",
  "backend": "http",
  "contentHash": "...",
  "truncated": false
}
```

Failures preserve the public typed failure code but do not expose internal
process errors.

## Prototype limits

- `http` and `https` only; ports 80 and 443 only.
- No URL credentials.
- Request body capped at 2 KiB.
- Default three requests per minute and 30 per day per client address.
- Default two concurrent transcodes per process; excess work fails fast.
- HTTP backend only, playbooks off, response budget capped.
- Markdown projection capped independently of the MCP response budget.
- No cookies, sessions, custom headers, browser fallback, search, or storage.
- No URL or page-content logging.
- In-memory limits assume one beta instance. A multi-instance deployment needs
  a shared limiter before public activation.

## Decisions and trade-offs

| Decision | Benefit | Cost / revisit trigger |
|---|---|---|
| Separate Node gateway over MCP stdio | No HTTP surface or demo policy enters L0 Core | The gateway must supervise one child host and recover from process failure |
| HTTP backend only | Bounded latency, memory, and anonymous-service abuse surface | Lower coverage on JavaScript-heavy pages; browser access needs a separate allowlisted design |
| Fail fast at the concurrency cap | Predictable resource ceiling; no hidden queue retention | Visitors may receive `demo_busy` during bursts |
| In-memory rate limits | No external state for a one-instance beta | Not safe for horizontal scaling; replace with a shared atomic limiter first |
| No request/content storage | Smaller privacy and breach surface | Product analytics remain aggregate and limited |
| Early DNS check plus the Core pinned guard | Cheap rejection at the edge and authoritative protection at the socket | Two DNS checks; the Core result remains authoritative if DNS changes between them |

## Local run

Prerequisites are the normal source-checkout prerequisites. From this directory:

```powershell
npm test
$env:OCCAM_HOME = (Resolve-Path ..\..).Path
npm start
```

Open `http://127.0.0.1:8787/`.

Configuration:

| Variable | Default | Purpose |
|---|---:|---|
| `PORT` | `8787` | Listen port |
| `DEMO_HOST` | `127.0.0.1` | Listen address |
| `DEMO_ENABLED` | `1` | Immediate request kill switch (`0` disables transcode) |
| `DEMO_ALLOWED_ORIGIN` | same-origin only | Optional exact browser origin |
| `DEMO_TRUST_PROXY` | `0` | Trust first forwarded client IP only behind a trusted stripping proxy |
| `DEMO_RATE_PER_MINUTE` | `3` | Per-client minute limit |
| `DEMO_RATE_PER_DAY` | `30` | Per-client daily limit |
| `DEMO_MAX_CONCURRENCY` | `2` | In-process active transcodes |
| `DEMO_MAX_MARKDOWN_CHARS` | `12000` | Response projection cap |
| `DEMO_MCP_TIMEOUT_MS` | `40000` | MCP request timeout |

## Publication gates

Before deployment:

1. Run the gateway self-test and the repository L0 fast gate.
2. Test private/loopback/link-local IPv4 and IPv6, DNS rebinding, and redirect
   targets against the real worker path.
3. Test request-body, output, timeout, rate, and concurrency limits.
4. Put the service behind a trusted proxy that strips incoming forwarding
   headers, or leave `DEMO_TRUST_PROXY=0`.
5. Add a shared rate limiter before scaling beyond one instance.
6. Set a hard platform egress/concurrency budget and an immediate kill switch.
7. Approve the exact public origin, privacy notice, hosting cost, and rollback.
8. Only then add a public documentation page or CTA.

## Revisit when the service grows

- Replace the in-memory limiter with a shared atomic store.
- Separate the MCP process supervisor from the HTTP process if restarts become
  correlated.
- Add allowlisted browser-backed examples only after a separate cost and abuse
  review; anonymous browser fallback remains out of the base contract.
- Add aggregate metrics without recording submitted URLs or extracted content.
