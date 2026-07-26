# Managed acquisition

**Slug:** `managed-acquisition` · **Product system:** PS-1 Acquisition · **CAPs:** 10 · **Public relevance:** HIGH

**Member CAPs:** CAP-054–CAP-058, CAP-238–CAP-242  
**Product capability:** CAP-054  
**Engineering findings:** EF-003 (SSRF guard absent on managed HttpClient)

## What it is

Optional **last-rung** escalation to third-party extraction APIs (Jina, Firecrawl, Scrapfly, Spider) after **both** local HTTP and browser attempts fail under `http_then_browser`. Off by default. **Not** a value of `backend_policy`.

## Why it exists

Operator-configured escape hatch for hosts locals cannot extract, without making third-party egress the default path.

## User-visible entrypoints

| Surface | Behavior | Evidence |
|---------|----------|----------|
| Cascade only | `OccamRouter` after dual local fail | `OccamRouter.cs:163-175` |
| Env configuration | `OCCAM_MANAGED_*` | `ManagedExtractBackend.cs:28-95` |
| MCP param | **None** — cannot select managed directly | CAP-054 |
| Policy `http` / `browser` | Never reaches managed | `OccamRouter.cs:81-86` |

## Core behavior

### Attempt gate

`_managed is not null && ShouldAttempt(url)`:

1. Provider resolved from `OCCAM_MANAGED_PROVIDER`.
2. API key present if `provider.RequiresApiKey`.
3. Host allowlist: `OCCAM_MANAGED_DOMAINS` — **unset means all hosts eligible**.

### Providers

| Provider | CAP | Key required | Call shape | Notes |
|----------|-----|--------------|------------|-------|
| `jina` | CAP-055/240 | no (optional) | GET `{base}/{fullUrl}` | |
| `firecrawl` | CAP-056/239 | yes | POST `/v1/scrape` markdown | |
| `spider` | CAP-058/241 | yes | POST `/crawl` limit:1 markdown | |
| `scrapfly` | CAP-057/242 | yes | GET scrape; **`render_js=true` hardcoded**; key in query | `ScrapflyProvider.cs:23-25` |

Default client timeout **60s** (`OCCAM_MANAGED_TIMEOUT_MS`). Uses **sync** `HttpClient.Send` on `occam.managed` named client.

### Success vs failure surface (EF-056 critical)

- **Managed success** → Finish(managed); surfaces as ordinary `backend` string.
- **Managed failure** → recorded in `recovery[]` only; **never** passed to `ChooseRawFallback`; surface = ranked(http, browser) only (`OccamRouter.cs:171-182`; GAP-014).

## Advanced behavior

| Topic | Behavior | Evidence |
|-------|----------|----------|
| CAP-238 framework | Provider registry + domain filter | `ManagedExtractBackend` |
| Empty markdown | Treated as extract failure | acquisition model |
| Codes | `managed_error`, `managed_disabled`, `http_*`, `timeout`, `extraction_failed` | same |
| CAP-194 | API-key authenticated egress; credentials env-only by design comment | network-fetch-proxy |

## Automatic / silent behavior

When provider env is set and domains unset, managed may run for **any** dual-fail URL — easy to miss third-party egress (`AUTOMATION-MODEL.md` routing note). Success looks like a normal backend win unless operator inspects `backend` / recovery.

## Parameters

**None** on MCP tools. Configuration is environment-only.

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_MANAGED_PROVIDER` | unset | Enables framework; names provider |
| `OCCAM_MANAGED_API_KEY` | unset | Required for keyful providers |
| `OCCAM_MANAGED_BASE_URL` | provider default | Override API base |
| `OCCAM_MANAGED_DOMAINS` | **all hosts** | Comma allowlist when set |
| `OCCAM_MANAGED_TIMEOUT_MS` | 60000 | Client timeout |

## Backends

Third-party HTTP APIs only. Does not spawn local workers. **Does not** honor `OCCAM_HTTP_PROXY` (Core HttpClient — CAP-166 / EF-007).

## Sessions / state

No session_profile forwarding modeled as storageState to providers. User URL is sent to third party (path/body). Stateless per call aside from HttpClient handler.

## Network behavior

- Egress to operator-configured third-party base URL with **target page URL** in request.
- **`AddHttpClient("occam.managed")` has no `OutboundHttpGuard.ConnectCallback`** — **EF-003**.
- Not covered by worker DNS-pin SSRF.
- Sync send can block thread for up to timeout.

## Artifacts produced

Provider markdown on success; failure codes as above; recovery attempt entry even when not surface winner.

## Trust / provenance properties

Third-party rewritten content — **weaker provenance** than local workers. No SSRF guard on managed client (EF-003). Do not present managed markdown as “local extract.” Receipts (if enabled) attest host packaging, not provider honesty.

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Not configured | Silent skip |
| Host not in domains | Skip |
| Provider fail | Locals ranked; managed not surface |
| Missing browser workers | Cascade never starts; managed unreachable (`AreBackendsReady`) |
| Empty MD | Fail attempt |

## Platform differences

None specific beyond general HttpClient/.NET.

## Composition with other capabilities

- Only after HTTP+browser fail in `acquisition-routing`.
- Independent of proxy-egress worker path (bypasses it).
- Post-processors still apply if managed returns “ok” markdown that looks like challenge/login/thin.

## Known limitations

- Not selectable as `backend_policy`.
- Domains default = all hosts when provider set — footgun.
- Scrapfly always bills JS render.
- CAP-238 status **BUGGY** in canonical ledger — treat framework as shipped-but-flagged.
- Provider fidelity vs local markdown unmeasured (acquisition model UNCERTAIN).

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-003** | Managed HttpClient lacks OutboundHttpGuard |
| EF-056 / GAP-014 | Managed fail never wins surface (model correction) |
| EF-007 / CAP-166 | No OCCAM proxy on Core clients |

## Code evidence

- `Routing/OccamRouter.cs:163-182`
- `Backends/ManagedExtractBackend.cs:28-95`
- Providers: `JinaProvider`, `FirecrawlProvider`, `SpiderProvider`, `ScrapflyProvider`
- `docs-audit/ACQUISITION-ROUTING-MODEL.md` Rung 3
- `docs-audit/subsystems/network-fetch-proxy.md` CAP-194

## Public-doc relevance

**HIGH** for operators enabling managed; **must** state: last resort only, not a policy enum, fail does not replace local failure codes, third-party egress + EF-003 honesty.

## Handbook relevance

Operator “escalation providers” appendix — env table + domain allowlist warning + trust downgrade.
