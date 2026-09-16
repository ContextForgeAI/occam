# Canary demo — proving (and failing to prove) a read

A runnable walkthrough of the four verdicts in
[PROBE_PROTOCOL.md](../../PROBE_PROTOCOL.md). No agent, no API key, no network beyond loopback.

## Run it

```bash
node samples/canary-demo/demo.mjs
```

The script starts the probe server from the built host, then plays four roles against it: an honest
reader, a fabricator, a reader whose answer arrives too late, and a relay that knows a valid
sentinel it was never served.

Prerequisite — the host must be built once:

```bash
dotnet build src/FFOccamMcp.Core -c Release
```

Takes about 15 seconds, most of it waiting for a time bucket to roll over.

## What you should see

```
probe server on http://127.0.0.1:8971 (buckets of 30s, fresh tolerance 0)

 [1/4] honest reader            -> READ_VERIFIED    ok=true
 [2/4] fabricator               -> HALLUCINATED     ok=false
 [3/4] late reader              -> READ_STALE       ok=true
 [4/4] out-of-band relay        -> REPLAY_SUSPECT   ok=false

All four verdicts behaved as specified.
```

Two of those are worth pausing on.

**`READ_STALE` reports `ok=true`.** The read genuinely happened; it is just no longer current. A
protocol that collapsed this into `HALLUCINATED` would accuse a truthful agent of making things up,
which is the worst failure mode available to a trust mechanism.

**`REPLAY_SUSPECT` reports `ok=false`, even though the sentinel is cryptographically valid.**
Authenticity is not provenance. The value is real, but this host never recorded serving it, so
something other than a logged read delivered it.

## Nothing here is simulated

All four steps are real HTTP round trips against a running server. What the demo changes is the
server's *configuration*, not the mechanism, because two verdicts are unreachable inside a
15-second script at protocol defaults:

| Verdict | Default would need | Demo configuration |
|---------|--------------------|--------------------|
| `READ_STALE` | a 300-second bucket to roll over, ±1 tolerance — up to 10 minutes | `OCCAM_CANARY_BUCKET_SECONDS=30`, `OCCAM_CANARY_FRESH_TOLERANCE=0`, then genuinely wait for the boundary |
| `REPLAY_SUSPECT` | an authentic sentinel with no issuance record | issuance log shrunk to its 256-record minimum, then overflowed — the eviction caveat from [PROBE_PROTOCOL.md §6.2](../../PROBE_PROTOCOL.md) |

The step-4 trick is worth understanding: the sentinel really was issued by this host, and the host
really has no record of it any more. That is the *same observable state* as a value delivered by an
unlogged path, which is why the verdict is `REPLAY_SUSPECT` rather than something stronger — the
protocol cannot tell those two cases apart, and says so.

## Trying it with a real agent

Point an agent at the probe URL and ask it to report the sentinel:

```bash
dotnet run --project src/FFOccamMcp.Core -c Release -- canary serve --port 8971
# stderr prints a ready-made probe URL, e.g.
#   canary_probe_example: http://127.0.0.1:8971/probe/canary/AbC…
```

Then ask your agent: *"read <that URL> and tell me the value of the `occam-sentinel` meta tag"*, and
check its answer:

```bash
curl "http://127.0.0.1:8971/probe/canary/<sessionId>/verify?sentinel=<what the agent said>"
```

An honest agent that reads the page returns `READ_VERIFIED`. One that recognises the URL shape and
answers from prior knowledge cannot — the value did not exist when it was trained.

## What this does not demonstrate

The demo shows the protocol works. It says nothing about whether canaries *reduce* hallucination on
real pages — that is [H1](../../docs/research/hypothesis.md), and it has not been measured.
