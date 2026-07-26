# Network safety

**Slug:** `network-safety` · **Product system:** PS-1 Acquisition · **CAPs:** 23 · **Public relevance:** HIGH

**Member CAPs:** CAP-073, CAP-100, CAP-101, CAP-103, CAP-110, CAP-150–CAP-156, CAP-165, CAP-184–CAP-190, CAP-194, CAP-225, CAP-226  
**Product capability:** CAP-151  
**Engineering findings:** EF-042, EF-043

## What it is

URL/DNS/redirect/response-size/robots/timeout **policy and guards** that constrain what Occam will fetch and how failures are typed. Spans Core preflight, worker SSRF pins, probe redirects, and politeness gates. **Not** a single class — a cross-cutting safety family.

## Why it exists

Prevent SSRF against private networks, bound memory/time, optionally respect robots, and give typed network failures — without claiming universal crawl politeness (robots default **off**).

## User-visible entrypoints

| Path | Guards applied | Evidence |
|------|----------------|----------|
| TranscodePipeline preflight | Literal private host block | CAP-150; `FetchPreflight` / `PrivacyClassifier` |
| HTTP / browser workers | DNS-pin SSRF + redirects | CAP-151–153, CAP-226 |
| Core `OutboundHttpGuard` clients | Probe/robots/genome/TSA | CAP-154 |
| Robots/throttle | Before router | CAP-103/190; `RobotsThrottleService` |
| Genome well-known fetch | Env-gated live call | CAP-073 |
| css-extract | **Parity gap** — incomplete | EF-043 |
| Managed (CAP-194) | Third-party; **no** OutboundHttpGuard | EF-003 (managed family) |

## Core behavior

### Private URL / SSRF

| CAP | Mechanism | Notes |
|-----|-----------|-------|
| CAP-150 | Literal localhost/`*.local`/`*.internal`/private IP pre-check | Before dispatch |
| CAP-151 | HTTP worker DNS-rebinding-safe pin | Product capability |
| CAP-152 | Per-hop SSRF on `<meta refresh>` | HTTP worker |
| CAP-153/226 | Browser every navigation | |
| CAP-154 | Core HttpClient ConnectCallback | Probe etc. |
| CAP-155 | Dual-stack private ranges match C#↔JS | |
| CAP-156 | `OCCAM_ALLOW_PRIVATE_URLS=1` relaxes **rejection only** — still resolves/pins | |

Default: private blocked. Allow flag does not disable pinning logic.

### Size / oversize (CAP-101, CAP-225)

- HTTP/browser: `OCCAM_MAX_RESPONSE_BYTES` default **8 MiB** (max raise 16 MiB in acquisition model).
- Browser HTML snapshot cap **900_000** chars (CAP-225).
- css-extract: **unbounded** `response.text()` — **EF-043** / GAP-004.

### Robots / throttle (CAP-103, CAP-190)

- `OCCAM_RESPECT_ROBOTS` → Disallow → `robots_disallowed`.
- `OCCAM_HOST_THROTTLE_MS` → per-host delay.
- Robots fetch errors **fail-open allow** (GAP-018).
- Default: both off — Occam is user-directed, not a crawler by default.

### Redirects (CAP-110, CAP-184–186)

- Probe: two HttpClient modes, both SSRF-guarded.
- HTTP worker: fetch redirects + meta-refresh ≤3 + credential strip (CAP-172 in session-fetch).
- Browser: all navigation types re-validated.

### Timeouts / retry (CAP-187, CAP-188)

- Fixed per-backend timeouts (HTTP 35s, browser env, managed 60s) — **not** per-call MCP params.
- **No** automatic retry/backoff on transient network failures (CAP-188). Escalation is the cascade, not retries.

### Shared identity (CAP-189)

Single default UA/Accept from `profiles/occam-fetch-defaults.json` across paths. Override only via session / headers file — **no** UA randomization.

## Advanced behavior

| CAP | Notes |
|-----|-------|
| CAP-073 | Well-known genome fetch — gated live network; genome body hazards in FAILURE map GAP-009 |
| CAP-165 | Rotation absent on daemon/pool/css/dom-skeleton (proxy family; safety-relevant reach) |
| CAP-194 | Managed third-party egress (safety boundary, not a guard) |

## Automatic / silent behavior

| Behavior | Risk |
|----------|------|
| Robots fail-open | Polite policy bypass when opted in but robots.txt unreachable |
| Probe SSRF → `network_error` | **Misleading** — EF-042 / GAP-003 |
| Playwright proxy fail-open | Egress policy bypass (GAP-030) — proxy family |
| Public-ref / 404 short-circuit | Avoids unsafe/pointless browser (routing) |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| (none direct) | — | Safety is env + URL shape |
| `session_profile` | unset | Does not disable SSRF |
| Tool URL | required | Scheme/host validated |

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_ALLOW_PRIVATE_URLS` | off | Allow private/localhost targets |
| `OCCAM_MAX_RESPONSE_BYTES` | 8 MiB | Body cap http/browser |
| `OCCAM_RESPECT_ROBOTS` | off | Enforce Disallow |
| `OCCAM_HOST_THROTTLE_MS` | 0 | Delay between hosts |
| `OCCAM_BROWSER_TIMEOUT_MS` | 60000 | Browser budget |
| Genome-related env | off | CAP-073 live fetch |

## Backends

Applies **to** http/browser/probe/robots clients unevenly. css-extract and managed lack full parity.

## Sessions / state

Robots throttle may keep per-host timing state in-process. SSRF decisions are per-request. No durable “allowlist” beyond env.

## Network behavior

This family **defines** the safety network behavior summarized above. Cross-ref `FAILURE-BEHAVIOR-MAP.md` rows for SSRF probe mask, css unbounded body, robots fail-open.

## Artifacts produced

Typed failures: `private_url_blocked`, `robots_disallowed`, `response_too_large`, `timeout`, `dns_error`, `tls_error`, `network_error`, `http_*`. Probe may dishonestly emit `network_error` for SSRF (EF-042).

## Trust / provenance properties

Guards reduce accidental internal SSRF; they do **not** prove external content safety. Fail-open politeness and probe code masking reduce **operator trust in failure codes**.

## Failure / fallback behavior

See `FAILURE-BEHAVIOR-MAP.md`. Critical honesty gaps:

| Trigger | Observed | Dangerous? |
|---------|----------|------------|
| Probe SSRF block | Often `network_error` | yes — hides policy |
| css-extract private IP | May fetch | yes |
| Robots fetch fail | allow | polite bypass |
| Oversize http/browser | typed | no |
| Oversize css | unbounded | DoS/memory |

## Platform differences

Private-range definitions intended to match across OS. Path separators for tier files OS-dependent. Vectorized HTML scanner (probe) differs by SIMD — throughput only (`PLATFORM-DIFFERENCES.md`).

## Composition with other capabilities

- Gates `acquisition-routing` before backends.
- Interacts with `proxy-egress` (proxy fail-open; Core clients ungarded for proxy).
- Genome (CAP-073) is playbook-adjacent network — still safety-relevant.
- CAP-194 overlaps `managed-acquisition`.

## Known limitations

- css-extract SSRF/body parity gap (EF-043).
- Probe failure-code dishonesty (EF-042).
- Robots default off + fail-open.
- No retry layer — transient blips escalate or fail once.
- CAP-165 listed here: rotation reach hole (also proxy family).

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-042** | Probe masks OutboundUrlBlocked as `network_error` |
| **EF-043** | css-extract lacks private-ip pin + body cap |
| GAP-003/004/018 | Matching gaps |

## Code evidence

- `PrivacyClassifier.cs`, `FetchPreflight`, `OutboundHttpGuard.cs`
- `workers/shared/lib` private-ip / response-body-cap / egress
- `Services/RobotsThrottleService.cs:29-64,107-146`
- `HttpProbeFetcher.cs:164-175` (EF-042)
- `workers/css-extract/css-extract.mjs:39,78` (EF-043)
- `docs-audit/subsystems/network-fetch-proxy.md` §§1,5

## Public-doc relevance

**HIGH.** Document private URL block, allow flag, robots opt-in, size caps, and honesty limits (probe codes, css path).

## Handbook relevance

Security & networking chapter; troubleshooting `private_url_blocked` vs misleading `network_error`.
