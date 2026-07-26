# Hermes lifecycle integration notes (remaining)

## Status

No stable, documented Hermes lifecycle callback / lease / ownership API was available to RC.2 PR-G.
Occam therefore does **not** invent Hermes-specific public hooks.

## What Occam owns today

- Exact descendant identity (`RuntimeId`, PID, parent PID, session, home, transport).
- Read-only diagnostics (`lifecycle self` / `lifecycle diagnose`).
- Targeted shutdown planning that rejects non-exact selectors.
- Launcher signal forwarding to the exact Core child.

## What an external host (including Hermes) would need to supply later

| Needed capability | Why | Current disposition |
|---|---|---|
| Stable instance lease / desired-state owner id | Distinguish dashboard vs gateway ownership | Use `OCCAM_OWNER_LABEL` / `Ownership.ExternalClient` until a real API exists |
| Lifecycle callback on stop/refresh | Coordinate host UI with Occam shutdown | Not implemented; adapter boundary ready |
| Enumerate peer Occam instances | Overlap diagnostics without process-name scans | Operator supplies `--peers` JSON to `lifecycle diagnose` |

## Explicit non-goals for PR-G

- Do not auto-kill processes named `OccamMcp.Core` / `FFOccamMcp.Core`.
- Do not treat Hermes verbose/quiet stream hangs as Occam lifecycle defects.
- Do not merge independent dashboard and gateway host trees into one singleton.

## Next step when evidence appears

If Hermes publishes a lifecycle API, implement a thin `ILifecycleAdapter` wrapper in an optional
integration package or script — keep Core host-agnostic.
