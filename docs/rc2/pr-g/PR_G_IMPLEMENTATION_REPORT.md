# PR-G lifecycle identity — implementation report

## Scope

PR-G implements INV-10 ownership and diagnostics only. It is not a full launcher rewrite and does not
invent Hermes external APIs.

## Production changes

- `OccamMcp.Core.Lifecycle` types: `HostIdentity`, `ParentHostIdentity`, `SessionId`, `StartTimestamp`,
  `RuntimeId`, `Ownership`, `ShutdownTarget`, `LifecycleDiagnostics`, `HostIdentityDescriptor`,
  `HostIdentityRegistry`, `ILifecycleAdapter`, `LocalLifecycleAdapter`.
- CLI verbs: `lifecycle self` and `lifecycle diagnose` (read-only JSON).
- `scripts/launch-mcp-host.mjs` stamps `OCCAM_RUNTIME_ID`, `OCCAM_SESSION_ID`, `OCCAM_PARENT_PID`,
  `OCCAM_PARENT_LABEL` and forwards termination signals to the exact child PID.
- `scripts/lib/stop-occam-processes.mjs` adds `stopOccamHostByPid` for exact-PID stops.
- Removed the misleading parent-side `prctl(PR_SET_PDEATHSIG, childPid)` call from
  `WorkerProcessGroup.Attach`; documented that prctl applies only to the calling process.

## Compatibility

No MCP tool schemas or public response fields from PR-B–PR-F were removed. Bulk maintainer stop by
root/path remains a separate script path and is not promoted as the Core shutdown API.

## Limitations

- Peer discovery is operator-supplied (`--peers`); Core does not scan the OS process table for
  duplicates.
- Hermes adapter remains a documented boundary only until an external API is confirmed.
