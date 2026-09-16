# Occam Probe Protocol v1 — Proof-of-Read Canaries

**Status:** Experimental
**Protocol identifier:** `occam-canary-v1`
**Reference implementation:** `src/FFOccamMcp.Core/Canary/` (this repository)
**Cross-platform evidence:** [`docs/testing/RESULTS.md`](docs/testing/RESULTS.md)

This document specifies the proof-of-read canary: a mechanism that lets a verifier distinguish
"the agent actually read this page" from "the agent produced text that sounds like it did".

The key words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT** and **MAY** are to be interpreted
as described in [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).

---

## 1. Problem statement

An MCP host hands a language model a tool that fetches web content. The model then writes a summary.
Nothing in that loop establishes that the summary derives from the fetched bytes. The model may
produce a fluent, plausible, entirely fabricated account of a page it never received — and a fetch
tool that returns an empty shell, a consent wall or a challenge page makes this *more* likely, not
less, because the model falls back on prior knowledge of the URL.

Existing mitigations attack adjacent problems:

- retrieval citations prove a *span exists in a document the pipeline holds*, not that it reached the
  model's context;
- content hashes prove *what the fetcher got*, not *what the model saw*;
- self-report ("I read the page") is unfalsifiable.

This protocol closes the specific gap: **did the content of this fetch enter the context of the
entity that is now answering?**

## 2. Overview

A verifier controls an HTTP endpoint that serves a probe document. The document embeds a
**sentinel**: a short, unpredictable, time-bound token derived by keyed hashing from a server-side
secret, the session identifier and the current time bucket.

An agent is asked to report the sentinel. Because the sentinel

- cannot be guessed (it is a MAC tag under a secret the agent never sees),
- cannot be recalled from training data (it did not exist at training time),
- cannot be reused for long (it changes every bucket),

a correct report is evidence that the document's content passed through the agent's context during
the bucket window. An incorrect or absent report is evidence that it did not.

```
        ┌──────────────┐  1. GET /probe/canary/{sessionId}
        │    agent     │ ────────────────────────────────────────►┐
        │  (under test)│                                          │
        └──────┬───────┘  2. HTML carrying sentinel S             │
               │         ◄────────────────────────────────────────┤
               │                                          ┌───────▼────────┐
               │ 3. "the sentinel is S'"                  │    verifier    │
               └─────────────────────────────────────────► │ secret K       │
                                                          │ issuance log   │
                              4. verdict ◄────────────────┤                │
                                                          └────────────────┘
```

## 3. Sentinel derivation

### 3.1 Secret

The verifier **MUST** generate a root key of 32 bytes from a cryptographically secure random source
at process start. The root key:

- **MUST NOT** be written to disk, logs, telemetry, error messages or any serialised form;
- **MUST NOT** be shared between hosts or embedded in a repository or container image;
- **SHOULD** be zeroed once subkeys are derived.

Two subkeys **MUST** be derived with HKDF-SHA256 ([RFC 5869](https://www.rfc-editor.org/rfc/rfc5869))
from the root key, with an empty salt and these `info` strings:

| Subkey        | `info`                              | Purpose                              |
|---------------|-------------------------------------|--------------------------------------|
| sentinel MAC  | `occam-canary-v1/sentinel-mac`      | deriving sentinels                   |
| identifier pepper | `occam-canary-v1/identifier-pepper` | hashing client identifiers for audit |

Separate subkeys are **REQUIRED** so that a component holding the audit pepper cannot forge a
sentinel, and so that the two purposes can be re-keyed independently in a future revision.

Because the root key is process-scoped, a restart invalidates every outstanding sentinel. This is
intentional: it bounds the lifetime of any leaked proof material to one process lifetime.

### 3.2 Time bucket

```
bucket = floor(unixSeconds / bucketSeconds)
```

`bucketSeconds` **MUST** default to 300. Implementations **MUST** use floor semantics, not
truncation-toward-zero, so bucket ordering stays monotonic for clocks reporting instants before the
Unix epoch (a misconfigured container can report them).

### 3.3 MAC input

The MAC input **MUST** be exactly:

```
input = ASCII("occam-canary-v1")        // 15 bytes, domain label
      || 0x00                           // separator
      || int64be(bucket)                // 8 bytes, big endian, two's complement
      || int32be(len(sessionIdUtf8))    // 4 bytes, big endian
      || sessionIdUtf8                  // variable
```

The fixed-width bucket and the explicit length prefix make this encoding **injective**: no two
distinct `(bucket, sessionId)` pairs frame to the same byte sequence. Concatenating the fields
without a length prefix would not have this property once a future revision appends a field.

The domain label **MUST** be changed whenever the framing or the semantics of the tag change. Doing
so makes sentinels from the previous revision unverifiable rather than silently reinterpreted.

### 3.4 Tag and encoding

```
tag      = HMAC-SHA256(sentinelKey, input)
sentinel = base64url_unpadded(tag[0 .. sentinelBytes])
```

`sentinelBytes` **MUST** be in `[16, 32]` and **SHOULD** default to 32.

The encoding is RFC 4648 §5 base64url **without padding**. Standard base64 is **NOT** used: `+`,
`/` and `=` do not survive URL embedding, chat transcript round-trips and copy-paste reliably, and a
sentinel that gets mangled in transit produces a false `HALLUCINATED` — the worst possible failure
mode for this protocol, because it accuses a truthful agent.

Truncation is a deliberate knob, analysed in §7.3.

## 4. The probe document

### 4.1 Embedding

The probe document **MUST** carry the sentinel in all three of these placements:

1. `<meta name="occam-sentinel" content="…">` — survives head-only fetches, and is the placement a
   cooperating client reads programmatically.
2. `<span style="display:none" data-occam-sentinel="1">…</span>` — the classic canary placement,
   invisible to a human reader.
3. At least one **visible** text node.

The third placement is not redundant. Readability-style extractors — including the ones this very
host runs — legitimately discard hidden nodes and head metadata. A canary that lives only where the
extraction pipeline strips it measures the extractor, not the agent.

### 4.2 Caching

Probe responses **MUST** be served with:

```
Cache-Control: no-store, no-cache, must-revalidate
Pragma: no-cache
Expires: 0
```

An intermediary that caches a probe document hands out a sentinel from an earlier bucket and
silently converts a genuine current read into `READ_STALE`. The headers are therefore normative, not
cosmetic. Implementations **SHOULD** additionally send `X-Content-Type-Options: nosniff` and
`Referrer-Policy: no-referrer`.

### 4.3 Session identifiers

Each agent session **MUST** receive its own identifier, and it **SHOULD** be generated from at least
128 bits of CSPRNG output. Session identifiers **MUST** match `[A-Za-z0-9._-]{1,maxLength}`
(`maxLength` defaulting to 128). The identifier reaches an HTML document, a URL path and structured
log fields; restricting the charset is defence in depth on top of contextual escaping.

## 5. Verification

Given a session identifier and a claimed sentinel, the verifier computes the candidate sentinel for
every bucket in `[current + freshTolerance, current - staleHorizon]` and compares in constant time.
`freshTolerance` **MUST** default to 1 and `staleHorizon` to 24.

Precedence is fixed and total. Implementations **MUST** evaluate in this order:

| Order | Condition                                                         | Verdict          |
|-------|-------------------------------------------------------------------|------------------|
| 1     | session id invalid, claim absent, or claim matches no bucket       | `HALLUCINATED`   |
| 2     | claim matches a bucket, but no issuance record exists              | `REPLAY_SUSPECT` |
| 3     | claim matches, issuance exists, `abs(distance) <= freshTolerance`  | `READ_VERIFIED`  |
| 4     | claim matches, issuance exists, distance within `staleHorizon`     | `READ_STALE`     |

where `distance = currentBucket - matchedBucket` (positive means the past).

Two consequences of this ordering are load-bearing:

- **Authenticity is checked before freshness.** A valid tag from an old bucket is `READ_STALE`, never
  `HALLUCINATED`: the read really happened, it is just no longer current.
- **Provenance is checked before freshness.** A valid tag inside the fresh window with no issuance
  record is `REPLAY_SUSPECT`, never `READ_VERIFIED`. A value can be authentic and still have arrived
  by a path that is not a read.

Beyond `staleHorizon` the verifier reports `HALLUCINATED` rather than inventing a fifth state. This
is a deliberate loss of resolution in exchange for bounded work: verification cost is a function of
configuration, never of the claim.

Comparison **MUST** be constant time with respect to tag content. Claim normalisation **MUST** be
limited to trimming surrounding whitespace and quote characters (`"`, `'`, backtick), which chat
models habitually add. Normalisation **MUST NOT** fold case: base64url is case-significant, and
folding would widen the accepted set by a factor of 2^(characters).

## 6. Audit records

### 6.1 Content

Each issuance **SHOULD** be recorded with: session identifier, bucket, a peppered digest of the
client identifier, the issue timestamp, and a truncated user-agent.

Records **MUST NOT** contain the sentinel. A log line carrying a sentinel would let anybody who can
read logs produce a `READ_VERIFIED` without reading anything — it converts the audit trail into a
proof-material dispenser.

Records **MUST NOT** contain raw client addresses. The pepper never leaves the process, so the
digest is not reversible by a log reader and is not correlatable across restarts.

User-agent strings **MUST** be truncated and stripped of control characters before storage, so a
hostile header cannot forge additional log records by injecting newlines.

### 6.2 Retention

Retention **MUST** be bounded in both time and size, because the log is fed by unauthenticated
requests. Retention **SHOULD** cover at least `staleHorizon * bucketSeconds`; a shorter window makes
genuine older reads report as `REPLAY_SUSPECT`, since their issuance record has been evicted. The
reference implementation defaults to 24 hours of retention against a 2-hour stale horizon.

## 7. Security considerations

### 7.1 Threat model

**In scope.** An agent that fabricates page content; an agent that reports a stale sentinel as
current; a caller that farms sentinels to build a table; a log reader who wants to forge a verdict;
a hostile session identifier or user-agent aimed at the verifier's own storage and logs.

**Out of scope.** An adversary who can read process memory (they have the secret). An adversary who
controls the network between agent and verifier (they can read the sentinel in transit — deploy over
TLS, see §7.5). A human who reads the page and types the sentinel into the model by hand.

### 7.2 What a `READ_VERIFIED` does and does not prove

**Proves:** the sentinel value transited from the verifier's endpoint into the entity that reported
it, within the bucket window, and the verifier has a record of serving it.

**Does not prove:**

- that the model *understood* the page, or read anything beyond the sentinel;
- that the model read the *whole* page — a sentinel is a spot check, not coverage;
- that a human or a second tool did not relay the value;
- anything about a *different* URL. The proof is scoped to the probe document.

Over-claiming here would be worse than not measuring at all, because `READ_VERIFIED` is exactly the
kind of signal a downstream system will be tempted to treat as ground truth.

### 7.3 Tag truncation

A 16-byte tag gives 128 bits of forgery resistance and a 22-character sentinel; a 32-byte tag gives
256 bits and 43 characters. For online forgery both are far beyond reach: an attacker must produce a
tag inside a 300-second window against a rate-limited endpoint, so even 64 bits would resist.

The reason to keep 32 bytes by default is not forgery resistance but **provenance**: a shorter tag
raises the chance of an accidental collision across the whole population of sessions and buckets a
long-lived deployment sees, and a collision manifests as a `READ_VERIFIED` for an agent that read a
*different* session's page. The reason an operator might choose 16 is **echo fidelity**: a shorter
string is measurably easier for a small model to reproduce verbatim, and a truncation-induced
transcription error is scored as a hallucination. Implementations **MUST NOT** go below 16 bytes.

Note that a truncated tag is a prefix of the full tag. An operator who shortens `sentinelBytes` does
not change the derivation, only how much of it is published.

### 7.4 The verifier is an oracle

Both endpoints are oracles. The probe endpoint mints fresh proof material on request; the verify
endpoint reveals whether a value is authentic. Therefore:

- both **MUST** be rate limited, per session and per client;
- the rate-limit key space **MUST** be bounded, because the key derives from caller-controlled input
  and an unbounded map is itself the vulnerability;
- the verify response **MUST NOT** echo the expected sentinel on a failed claim.

### 7.5 Deployment

The endpoints **SHOULD** bind to loopback by default, and an implementation **SHOULD** reject
non-loopback `Host`/`Origin` headers to mitigate DNS rebinding against a locally bound probe. An
operator exposing the probe to a remote agent **MUST** use TLS: the sentinel is a bearer token for
the duration of its bucket, and a passive observer who captures it can produce a `REPLAY_SUSPECT` —
or, if the observer's fetch is what got logged, a `READ_VERIFIED`.

### 7.6 Clock dependence

The protocol is clock-dependent by construction. A verifier whose clock jumps backwards past a
bucket boundary will report `HALLUCINATED` for sentinels it issued moments earlier.
`freshTolerance` absorbs ordinary skew; it does not absorb a clock reset. Hosts **SHOULD** run NTP.

### 7.7 Privacy

The probe document is a tracking-adjacent artefact: it is unique per session and uncacheable. It
**MUST** be served with `robots: noindex, nofollow`, and it **MUST NOT** be used to profile
end users. The audit record is deliberately reduced to a peppered digest for this reason.

## 8. Configuration

Reference-implementation environment variables. All are optional; defaults are the normative values.

| Variable                         | Default | Range      | Meaning                               |
|----------------------------------|---------|------------|---------------------------------------|
| `OCCAM_CANARY_BUCKET_SECONDS`    | 300     | 30–3600    | bucket width                          |
| `OCCAM_CANARY_FRESH_TOLERANCE`   | 1       | 0–8        | buckets still counted as fresh        |
| `OCCAM_CANARY_STALE_HORIZON`     | 24      | 1–512      | oldest recognised bucket              |
| `OCCAM_CANARY_SENTINEL_BYTES`    | 32      | 16–32      | tag length                            |
| `OCCAM_CANARY_ISSUE_LOG_HOURS`   | 24      | 1–168      | issuance retention                    |
| `OCCAM_CANARY_ISSUE_LOG_CAPACITY`| 8192    | 256–1048576| issuance record cap                   |
| `OCCAM_CANARY_RATE_LIMIT`        | 30      | 1–10000    | requests per window                   |
| `OCCAM_CANARY_RATE_WINDOW_SECONDS`| 60     | 1–3600     | rate-limit window                     |

A configuration where `staleHorizon <= freshTolerance` is rejected at startup: it makes `READ_STALE`
unreachable, so the verifier would silently lose a state.

## 9. Test vectors

Vectors are derived with the **public, non-production** root key
`000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f` (byte *i* = *i*).

The canonical file is [`docs/testing/canary-vectors.json`](docs/testing/canary-vectors.json)
(SHA-256 `d0cc1b15096451f9c63d9c53d428f7b3f468800f3ac8e5cddbdbb838da0e50b5`).

| sessionId                      | bucket  | bytes | sentinel                                      |
|--------------------------------|---------|-------|-----------------------------------------------|
| `vector-session`               | 0       | 32    | `mENHHl1pbVogGlYUtGkyvSXzZosi-jQiJ6kpN5jmQhw` |
| `vector-session`               | 1       | 32    | `SsRmlFHeM5mZGPJA1fCiBHvd7p2aMN21ofVstQL8v8U` |
| `vector-session`               | -1      | 32    | `sNlf-meFmnSUFKgsEiQacMFf8GE_1_EN3QeKhgfOZv4` |
| `vector-session`               | 6000000 | 32    | `e-jPUfHCPr-WmQWy1B82PSpluyQtPPL5BnKqYvb3PKY` |
| `vector-session`               | 6000000 | 16    | `e-jPUfHCPr-WmQWy1B82PQ`                      |
| `a`                            | 0       | 32    | `PWtR1dbF8dK1vJTcq4_j-fI9pLrhRomuL_vtTRtG-JI` |
| `session.with_dots-and-dashes` | 123456  | 32    | `7Z11tKy2enm5zVuXj-b2dhaSqJZw97YjI68YKQfDpfM` |

Verify an implementation against them with:

```bash
occam canary vectors --verify docs/testing/canary-vectors.json   # expects CANARY_VECTORS_OK
occam canary vectors --emit --out /tmp/local.json                # must be byte-identical
```

The emitted file is byte-identical on macOS arm64, Linux x64 and Windows x64 — see
[`docs/testing/RESULTS.md`](docs/testing/RESULTS.md).

## 10. Conformance

An implementation conforms when it:

1. reproduces every vector in §9 from the published root key;
2. produces the verdict precedence of §5, including `REPLAY_SUSPECT` ahead of freshness;
3. serves the three embeddings of §4.1 and the no-store headers of §4.2;
4. keeps the secret out of every persisted or logged artefact (§3.1, §6.1);
5. bounds verification work by configuration rather than by the claim (§5).

The reference implementation exposes items 1–3 and 5 as executable checks:

```bash
occam canary selftest   # 17 assertions over the state machine and its invariants
occam canary smoke      # HTTP round trip: issue, read, verify, reject
```

## 11. Open questions

These are research questions, not implementation gaps. They are tracked in
[`docs/research/hypothesis.md`](docs/research/hypothesis.md).

1. **Echo fidelity by model size.** How does verbatim reproduction of a 43-character base64url token
   degrade for small models, and does `sentinelBytes = 16` recover it? Unmeasured.
2. **Does measurement change behaviour?** If an agent knows a canary is present, does its
   fabrication rate on *non-probe* pages drop? Unmeasured, and the more interesting question.
3. **Coverage, not presence.** A single sentinel proves arrival, not that the page was read. Would
   *k* sentinels at known document depths estimate read coverage, or just teach agents to scan for
   sentinel-shaped strings?
4. **Replay in multi-agent systems.** When agent A reads and agent B reports, `REPLAY_SUSPECT` is
   arguably the correct verdict for B — but the *system* did read the page. The protocol has no
   notion of delegated reads.

## 12. References

- RFC 2104 — HMAC: Keyed-Hashing for Message Authentication
- RFC 5869 — HKDF: HMAC-based Extract-and-Expand Key Derivation Function
- RFC 4648 §5 — base64url
- RFC 2119 — Key words for use in RFCs
- [Model Context Protocol specification](https://modelcontextprotocol.io)
