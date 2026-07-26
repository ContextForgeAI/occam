# Connection troubleshooting

| Symptom | What to try |
|---------|-------------|
| Host not detected | Install/open the host once; re-run `occam connect` |
| Config validated host unchanged | Pass `--only <id>` |
| “Unmanaged entry” | Existing `ff-occam` not owned by Occam — inspect it; `--force` only if you intend to replace |
| Commented JSON (VS Code / Zed) | Connect refuses to rewrite (would strip comments) — paste manually |
| Almost ready forever | Restart the named app; reload MCP servers |
| Action required | Complete the host’s trust/approve UI |
| Not ready | `occam doctor` then `occam smoke`; see [Troubleshooting](../troubleshooting.md) |
| CI mutated nothing | Expected — desktop mutation is off in CI |

## Next

- [Automatic connection](automatic.md)
- [Manual setup](manual.md)
- [Product troubleshooting](../troubleshooting.md)
