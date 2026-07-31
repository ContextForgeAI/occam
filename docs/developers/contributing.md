# Contributing

Contributor setup, gates, and pull-request expectations live in the repository root:

**[CONTRIBUTING.md](https://github.com/ContextForgeAI/occam/blob/main/CONTRIBUTING.md)**

Also read:

- [AGENTS.md](https://github.com/ContextForgeAI/occam/blob/main/AGENTS.md) — agent/contributor entry point  
- [Semantic contract](../architecture/semantic-contract.md) — durable extract invariants  
- [Quality baseline](../quality-baseline.md) — public quality claims  

Git authorship is human accountability: see **Git authorship** in the root
[CONTRIBUTING.md](https://github.com/ContextForgeAI/occam/blob/main/CONTRIBUTING.md).
AI tools must not appear as author, committer, or co-author.

### Preview this documentation site

```bash
python -m venv .venv-docs
# Windows: .\.venv-docs\Scripts\Activate.ps1
source .venv-docs/bin/activate
pip install -r docs/requirements.txt
mkdocs serve
```
