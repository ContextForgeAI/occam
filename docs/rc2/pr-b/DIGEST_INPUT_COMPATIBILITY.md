# Digest input compatibility in RC.2

## Preferred contract

Pass `occam_digest.urls` as a native JSON array of URL strings:

```json
{
  "urls": ["https://example.com/a", "https://example.com/b"]
}
```

The runtime `tools/list` schema exposes a string-array/string union. Native arrays must be non-empty
and contain only strings. Mixed, nested, malformed, empty, and oversized inputs return a typed tool
result with `ok: false` and `failureCode: "invalid_arguments"`.

## Temporary legacy compatibility

RC.2 prerelease continues to accept the former string transport forms:

- a JSON-encoded string array;
- a JSON-encoded array of `{url, focus_query?}` objects;
- newline, comma, or semicolon-delimited URLs.

These forms are deprecated. New clients must send native arrays. Per-entry `focus_query` is available
only through the legacy object-string form until a separately reviewed additive native entry schema is
approved; clients should normally use the top-level `focus_query`.

## One boundary

`DigestInputNormalizer` is the only transport compatibility boundary. It validates and converts every
accepted form to `IReadOnlyList<DigestUrlEntry>`. `DigestService` receives only that canonical
collection and does not parse MCP transport variants. This is the implementation of INV-3.

Normalization is bounded to 256 entries and 65,536 input characters. This protects the transport
boundary; it does not raise the execution limit. `max_urls` remains 1–8 and extra normalized entries
are not executed.

## Error ownership

| Layer | Responsibility | Failure form |
|---|---|---|
| JSON-RPC/schema | A JSON value can be bound to the declared union | Protocol error only for invalid JSON/protocol framing |
| Digest normalization | Shape, element type, non-empty input, size, and URL validity | Typed `invalid_arguments` |
| Source discovery | `source_url` produced no usable links | Typed `invalid_urls` |
| Digest execution | Per-URL extraction and aggregate outcome | Existing digest/per-item typed failures |

When both `source_url` and `urls` are supplied, the existing compatibility rule remains unchanged:
`source_url` wins and `urls` is ignored.
