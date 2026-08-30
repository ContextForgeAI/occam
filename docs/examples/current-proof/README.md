# Current Occam proof

This evidence bundle records a minimal live page read, a representative
webpage-noise transformation, and one controlled failure from Occam Core
`1.0.0-rc.5`.

It is deliberately small. The fixture proves the behavior of this example; it
is not a universal success-rate, size-reduction, or token-savings claim.

## Successful read

**Input:** `https://example.com/`

**Invocation:**

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com/"
  }
}
```

Only the required `url` argument was supplied. Occam used its default output
options and returned `ok: true`, source and final URLs, compact Markdown, an
extraction backend, a content hash, and a signed Receipt v1.

The recorded output begins:

```markdown
# Example Domain

This domain is for use in documentation examples without needing permission.
Avoid use in operations.
```

The full artifacts are
[success-output.md](success-output.md) and
[success-result.json](success-result.json).

## Representative webpage transformation

The controlled [input page](representative-input.html) wraps an engineering
article in primary navigation, search, related links, a newsletter form, a
cookie notice, scripts, layout CSS, and a footer. It is deliberately more
representative than `example.com` while remaining stable and inspectable.

Occam preserved the article heading, paragraphs, section structure, and code
sample while removing the presentation-only chrome. The recorded output begins:

```markdown
# Web context without the chrome

Agent infrastructure · 8 minute read

AI agents rarely need the whole interface of a webpage. They need the useful
text, its structure, and enough source information to explain where the
material came from.
```

For this controlled page:

- **Input:** 5,297 UTF-8 HTML bytes (this page only).
- **Output:** 1,736 UTF-8 Markdown bytes (this page only).
- **Note:** The byte ratio is a property of the fixture — not a product average
  or “Occam saves X%” claim. Other sites will differ.

No tokenizer was used. This is not a universal size, token-savings, or
answer-quality claim. The input body hash and runtime source revision are in
[representative-input-metadata.json](representative-input-metadata.json); the
method is in
[representative-measurement.json](representative-measurement.json). Inspect the
complete [Markdown output](representative-output.md) or
[tool result](representative-result.json).

## Measurement

The reproduction script fetched the same URL separately and compared:

- **Input:** 559 UTF-8 bytes in the HTML response body after HTTP content
  decoding.
- **Output:** 167 UTF-8 bytes in `result.markdown`.
- **Result:** 70.1% fewer bytes in this example.

No tokenizer was used. This is a byte measurement, not a token claim. See
[measurement.json](measurement.json) for the exact method and
[input-metadata.json](input-metadata.json) for the capture metadata.

## Controlled failure

The script also asks Occam to read
`http://127.0.0.1:9/occam-marketing-proof` with private-URL access disabled.
Occam returns:

```json
{
  "ok": false,
  "failure": {
    "code": "private_url_blocked",
    "message": "Private or local URLs are blocked."
  }
}
```

No external failure site is contacted. The full artifact is
[failure-result.json](failure-result.json).

## Reproduce

Prerequisites are the same as a source checkout: Node.js 20+, the .NET 10 SDK,
and the Occam worker dependencies prepared by `occam doctor`.

=== "Windows"

    ```powershell
    .\docs\examples\current-proof\reproduce.ps1
    ```

=== "Linux / macOS"

    ```bash
    bash ./docs/examples/current-proof/reproduce.sh
    ```

The wrapper first refreshes the minimal live success and controlled failure.
It then starts a loopback server for the representative input, launches a
second local MCP session with private-URL access explicitly enabled for that
fixture only, and refreshes its JSON/Markdown artifacts. It exits non-zero if
any expected contract or article-content check changes.

Expected marker:

```text
CURRENT_PROOF_OK ... failure=private_url_blocked
REPRESENTATIVE_PROOF_OK ... reduction=67.2%
```

Timing, signature, local port, and capture timestamp fields are expected to
change between runs. The success content, byte counts, and policy result are
bounded by the recorded source pages and current Occam behavior.
