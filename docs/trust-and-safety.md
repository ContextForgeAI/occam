# Trust & Safety

What happens when you install and use Occam — stated only as far as the product actually supports today.

## Short answers

| Question | Answer |
|----------|--------|
| Where does processing run? | On your machine (local-first MCP host + workers) |
| Is there a cloud middleman for normal extracts? | No |
| Telemetry / phone-home endpoint? | No telemetry endpoint in the shipped product |
| Are pages cached on disk? | No file cache by design — live extract |
| What if extract fails? | Typed `ok: false` + `failure.code` — content is unknown |
| Can I prove what was extracted? | Yes — signed receipts + `occam_verify` |
| Will install rewrite my AI configs unsafely? | Connect uses backups, atomic writes, ownership checks; unmanaged entries are left alone |
| Does CI change my desktop MCP configs? | No — connect does not mutate desktops in CI by default |

## Honesty vs verification

- **Honesty** is behavior: Occam refuses to pretend it read a page.  
- **Verification** is mechanism: receipts bind URL, time, content hash, and backend to a signature you can check later.

Both matter; they are not the same claim.

## Local-first model

See [Local-first](trust/local-first.md). Session profiles, keys, and playbooks you save stay as local files under your control.

## Honest failures

`ok: false` means **unknown content**. Never summarize the page from model memory. Route: [Honest failures](trust/honest-failures.md) → [Failure codes](failure-codes.md).

## Receipts

A receipt lets another process check which URL was fetched, when, what content hash was produced, which backend produced it, and whether the signature is valid.

- Human guide: [Receipts](receipts.md)  
- Normative format: [Receipt verification](receipt_verification.md)  
- Tool: [`occam_verify`](tools/occam_verify.md)

## Installation and connect safety

Release archives are SHA-256 verified. Connect backs up before write, writes atomically, protects unmanaged `ff-occam` entries, supports per-host rollback, and skips desktop mutation in CI unless forced.

Details: [Installation safety](trust/installation-safety.md) · [MCP hosts](mcp-hosts.md)

## Security policy

Vulnerability reporting and boundaries: [Security policy](trust/security-policy.md) (mirrors repository `SECURITY.md`).

!!! warning "Claims we do not make"
    Occam does **not** claim to be virus-free, 100% safe, security-audited, certified secure, or malware-scanned unless a real automated process says so. Network pages are **untrusted input**.

## Next

- [How Occam works](how-occam-works.md)
- [Verify a receipt (example)](examples/verify-receipt.md)
- [Security policy](trust/security-policy.md)
