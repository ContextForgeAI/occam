# Current Occam proof

This fixture records one successful live page read and one controlled failure
from Occam Core `1.0.0-rc.2` at source SHA
`acb1e1b31b13ba19a2d0ee115ae8389b9887deef`.

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

The script starts the local MCP host over stdio, calls `occam_transcode` for
both cases, refreshes the JSON/Markdown artifacts, and exits non-zero if either
contract changes.

Expected marker:

```text
CURRENT_PROOF_OK ... failure=private_url_blocked
```

Timing, signature, and capture timestamp fields are expected to change between
runs. The success content, byte counts, and policy result are bounded by the
recorded source page and current Occam behavior.
