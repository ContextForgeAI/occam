# Lifecycle identity model (PR-G)

**Invariant:** INV-10
**ADR:** ADR-0010 (host lifecycle is identity-scoped, not globally singleton)

## Purpose

Make Occam process ownership observable and shutdown targeted. PR-G does **not** rewrite the
launcher into a full process supervisor and does **not** invent vendor lifecycle APIs.

## Core types

| Type | Role |
|---|---|
| `RuntimeId` | Stable per-process id (`OCCAM_RUNTIME_ID`) |
| `SessionId` | Client/session correlation (`OCCAM_SESSION_ID`) |
| `StartTimestamp` | UTC start time |
| `ParentHostIdentity` | Parent PID + optional label (`OCCAM_PARENT_PID` / `OCCAM_PARENT_LABEL`) |
| `Ownership` | `SelfManaged` \| `ExternalClient` \| `Unknown` + optional owner label |
| `HostIdentity` | Exact identity: runtime, pid, parent, session, start, ownership, home, binary, transport |
| `HostIdentityDescriptor` | Read-only doctor/diagnostics projection |
| `ShutdownTarget` | Exact `{runtimeId, pid, parentPid?}` selector |
| `LifecycleDiagnostics` | Self + observed peers + overlap warnings |
| `ILifecycleAdapter` / `LocalLifecycleAdapter` | Host-agnostic boundary |
| `HostIdentityRegistry` | In-memory multi-tree registry for diagnostics/tests |

## Rules

1. Stop/refresh requires an exact `ShutdownTarget`. Blank or ambiguous targets are rejected.
2. Overlapping instances produce **warnings only** — never an automatic global kill.
3. Independent host trees (different sessions/owners) must coexist.
4. No OS-global singleton lock and no process-name-wide termination API in Core.
5. Parent-side `prctl(PR_SET_PDEATHSIG)` does not configure a child PID; the misleading parent call was
   removed. Death-signal setup, if needed, belongs in the child process.

## Operator surface

```text
OccamMcp.Core lifecycle self
OccamMcp.Core lifecycle diagnose [--peers peers.json]
```

Launcher (`scripts/launch-mcp-host.mjs`) stamps identity env vars and forwards SIGINT/SIGTERM/SIGHUP to
the exact child PID only.

## External adapters

Hermes (or any other host) may implement `ILifecycleAdapter` or call the CLI diagnose verb when a
stable external lifecycle API exists. Until then, Occam ships only the internal model and adapter
boundary. See [HERMES_LIFECYCLE_NOTES.md](HERMES_LIFECYCLE_NOTES.md).
