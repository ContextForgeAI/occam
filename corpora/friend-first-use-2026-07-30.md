# Friend first-use acceptance fixture (sanitized)

**Date:** 2026-07-30  
**Platform:** macOS (external friend machine)  
**Source:** Independent install of public one-liner; screenshots + chat transcript summarized without personal data.

## Public install command

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

## Observed discovery (installer)

```text
Looking for AI apps...
✓ Hermes Agent
✓ OpenClaw

Occam can connect to 2 apps.
```

## Observed apply

```text
Hermes Agent configured
OpenClaw configured
```

## Observed verification

```text
Hermes Agent:
  Config written
  host config registration valid
  host discovery not confirmed
  rolled back

OpenClaw:
  verified
```

## Observed final summary (product bug)

```text
Connected and ready:
✓ OpenClaw

Needs your action:
! Hermes Agent — Config written; host discovery not confirmed; rolled back
```

Friend interpretation: unclear what to do next; only the install step felt understandable.

## Friend next action (failed path)

Opened **Hermes** (the app they actually use) and typed:

> Use Occam to read https://example.com

## Hermes response (summary)

- `which occam` found nothing
- no Occam skill loaded
- no Occam tool available
- did not know what Occam was

## Product assessment

**NOT FRIEND-READY** until:

1. OpenClaw is not offered Ready when absent / residue-only / npx-only.
2. Hermes rollback is humanized and listed under **Not connected**.
3. Summary never steers the user to test Occam inside a rolled-back host.
4. One concrete next action + exact first-success guide URL.

## Lab note (Mac Mini residue — same class of signal)

Read-only check on lab Mac Mini (not the friend’s identity):

- `openclaw` **not** on PATH
- `~/.openclaw/` **exists** with stale `openclaw.json` (prior integration leftover)
- `npx` available under Homebrew PATH → **pre-fix** OpenClaw adapter treated npx as invoker → false connectable host

## Expected after fix

| Scenario | Expected |
|----------|----------|
| Stale `~/.openclaw` without `openclaw` binary | Not listed under connectable apps; not Ready |
| Hermes apply + verify fail + rollback | **Not connected:** Hermes + human explanation; no “test in Hermes” |
| Ollama present, no Ready MCP host | Next step: `occam chat` + guide URL |
| One Ready MCP host | Exact app name + new-chat prompt |
| No ready hosts | Installed success + guide URL; no false Ready |
