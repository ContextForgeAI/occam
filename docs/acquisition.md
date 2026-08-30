# Acquisition

How Occam obtains a page. This documents the **locked** acquisition contract (EF-056). Older “always HTTP→browser→managed” stories are obsolete.

**Status:** STABLE (core ladder) · managed path LIMITED / EXPERIMENTAL depending on operator config

## The ladder (`http_then_browser`)

<div class="oc-diagram" role="region" aria-label="Acquisition ladder diagram (scroll horizontally on narrow screens)" markdown="1">

```text
HTTP extract
   │
   ├─ usable success ─────────────────────────────► done (stop)
   ├─ configured public source adapter succeeds ──► done (source disclosed)
   ├─ 404 / 410 ──────────────────────────────────► fail (no browser, no managed)
   ├─ public-reference short-circuit (failed HTTP) ► fail (no browser)
   ├─ thin / short challenge / other escalate ────► Browser extract
   │                                                      │
   │                              ├─ usable success ────► done
   │                              └─ fail ──────────────► dual-fail ranking
   │                                                          │
   └─ (optional) Managed provider only after BOTH locals fail
                      ├─ managed success may surface
                      └─ managed failure NEVER becomes the user-facing result
```

</div>

## Locked truths

| Behavior | Meaning |
|----------|---------|
| Usable HTTP success stops | Non-empty Markdown, not thin, not a short challenge body → no browser |
| Public source adapters are explicit | A bundled playbook may map a presentation page to a sanctioned public source. npm package permalinks can use latest-version metadata from `registry.npmjs.org`. Exact crates.io `/crates/<name>` permalinks resolve the latest non-yanked version through the sparse index, then use the official rendered README. `backend` and `url.finalUrl` disclose the actual source |
| Thin / challenge may escalate | Bad extraction or short challenge-like body can open the browser rung |
| Browser escalation is conditional | Not every failure escalates; terminal HTTP failures do not |
| 404 / 410 short-circuit | No browser chase; no managed |
| Public-reference short-circuit | Some well-known public-reference hosts: failed HTTP ends the ladder |
| Dual failure uses `FailureRanking` | Surfaces the more informative local attempt — **not** “whichever had denser markdown” |
| Managed only after local failure | Only on the cascade policy; **not** a `backend_policy` enum value |
| Managed failure never surfaces | Recorded; the caller still sees a local failure outcome |
| No CAPTCHA solving | Walls become typed failures; use sessions / browser / operator-configured provider |
| No browser-bypass claim | The npm metadata adapter does not bypass Cloudflare and does not promise the package README; unsupported routes keep the honest original failure |
| Private-IP protections | Apply on specific paths (HTTP/browser/CSS workers with guards); scope is not universal across every helper client |

## `backend_policy`

Public values: `http` | `browser` | `http_then_browser` (default on most read tools).

There is **no** `managed` policy value. Managed acquisition is a separate, operator-configured escalation after both local backends fail on the cascade.

## What agents should do

1. Prefer default `http_then_browser` unless you already know you need browser-only.  
2. On `ok: false`, read `failure.code` — content is **unknown**.  
3. For login walls, use a [session profile](guides/sessions.md) — not CAPTCHA bypass fantasies.  
4. For networking / proxy / SSRF scope, see [Networking](networking.md).

## Related

- [How Occam works](how-occam-works.md)
- [Sessions](guides/sessions.md)
- [Failure codes](failure-codes.md)
- Handbook: Acquisition chapter
