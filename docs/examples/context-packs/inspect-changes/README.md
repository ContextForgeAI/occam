# Inspect changes

Recorded context pack for a cheap re-read.

**Task:** has the MDN Functions page changed since the stored hash?

**Source:** same URL and materialization as the understand-instruction pack  
**Settings:** `if_none_match` of the prior `contentHash`  
**Host:** `ff-occam/1.0.0-rc.2` on 2026-09-05

The pack preserves `unchanged:true` and an empty excerpt. That is success,
not a missing extract.

Payload: [`_inputs/inspect-changes.json`](../_inputs/inspect-changes.json).
Golden capture: [inspect-changes workflow](../../golden-workflows/inspect-changes/).
