# Compare sources

Recorded context pack for two official nginx pages.

**Task:** what `proxy_pass` does, and whether `proxy_read_timeout` was returned.

**Sources:** [proxy module](https://nginx.org/en/docs/http/ngx_http_proxy_module.html),
[beginner's guide](https://nginx.org/en/docs/beginners_guide.html)  
**Settings:** digest, `per_url_max_tokens=500`, focus `proxy_read_timeout proxy_pass`  
**Host:** `ff-occam/1.0.0-rc.2` on 2026-09-05

Both pages succeeded. Focus was **weak** on each item. `proxy_read_timeout`
was not in the excerpts — that miss is recorded, not filled from memory.

Payload: [`_inputs/compare-sources.json`](../_inputs/compare-sources.json).
Golden capture: [compare-sources workflow](../../golden-workflows/compare-sources/).
