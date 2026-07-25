# Installation and connect safety

## Release artifacts

The canonical installer downloads a GitHub Release archive and verifies **SHA-256** against the published manifest before extract.

## Config changes (`occam connect`)

When Occam registers itself with an AI host it:

- **Never overwrites** an existing `ff-occam` entry it did not create (unless you pass `--force`)  
- **Backs up** before writing  
- Writes **atomically** (no half-written config on interrupt)  
- **Rolls back** a broken registration it owns  
- **Keeps** registrations that only need a restart or trust prompt  
- **Does not mutate desktop configs in CI** by default  

Connect makes **no network calls** for registration — it only touches local files and may start the local Occam server to verify it responds.

## Sessions and secrets

- Session profiles are local JSON under `OCCAM_SESSIONS_ROOT`  
- Do not commit them  
- Do not put LLM API keys in Occam’s environment  

## Next

- [Supported hosts](../mcp-hosts.md)
- [Trust & Safety](../trust-and-safety.md)
- [Security policy](security-policy.md)
