# occam_probe

Cheaply diagnose a URL before paying for a full fetch: page class, risks, redirect chain, an
extractability score (0–1), and the recommended backend for `occam_transcode`.

## When to use

- Before transcoding an unknown/suspicious URL — is it a paywall, anti-bot wall, JS stub, or dead?
- To choose `backend_policy` up front instead of paying for an escalation.
- Already confident the page is fine → skip straight to [`occam_transcode`](occam_transcode.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS URL to probe |
| `timeout_ms` | int | `10000` | no | Probe timeout in milliseconds |
| `include_social_meta` | bool | `false` | no | Extract OpenGraph/Twitter meta from the HTML head |
| `session_profile` | string? | null | no | Headers profile id under `OCCAM_SESSIONS_ROOT/<id>.json` |

## Returns

Success envelope:

- `ok: true`, `url: {requested, final}`
- `classification` — `{pageClass, requiresJavascript, likelyCookieConsent, likelyChallenge,
  likelyLoginRequired, likelyPaywall, riskFlags[], domainTier?, httpOnlyRoute?, challenge?}`;
  `challenge` (when present) is `{kind, healEligible, recommendedAction}`
- `recommendation` — `{backend, estimatedLatencyMs, extractability}` — extractability is the
  0–1 score (low = paywall/anti-bot/JS-stub/dead; high = clean article/docs)
- `policy.privacyMode` — `local_public` / `local_private` / `blocked_by_policy`
- `statusCode`, `contentType`, `probeLatencyMs`, `redirectChain[]?`, `socialMeta?`, `timestamp`
- `agentHints` — `{suggestedNextTool, warnings[], decisions[]?}`

`likelyLoginRequired` uses the same access decision as transcode. Authentication terminology or a
login-like requested path cannot set it by themselves; direct status, redirect, or blocking identity-UI
evidence is required.

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `policy`, `statusCode?`,
`redirectChain?`, `probeLatencyMs`, `agentHints?`, `timestamp`. `ok:false` = the page's nature is
unknown, not "the page is bad".

## Failure codes

`invalid_arguments`, `timeout`, `dns_error`, `tls_error`, `network_error`,
`unsupported_content_type` (not HTML/PDF), `invalid_url`, `private_url_blocked`, and
`http_<status>` codes. See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://example.com/article", "include_social_meta": true }
```

Trimmed response:

```json
{
  "ok": true,
  "url": { "requested": "https://example.com/article", "final": "https://example.com/article" },
  "classification": { "pageClass": "article", "requiresJavascript": false, "likelyPaywall": false, "riskFlags": [] },
  "recommendation": { "backend": "http", "estimatedLatencyMs": 900, "extractability": 0.91 },
  "statusCode": 200,
  "agentHints": { "suggestedNextTool": "occam_transcode", "warnings": [] }
}
```

## Related

- [occam_transcode](occam_transcode.md) — the fetch this probe de-risks
- [occam_search](occam_search.md) — `rerank=true` runs this same scorer over search hits
- [Failure codes](../failure-codes.md)
