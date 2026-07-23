#!/usr/bin/env python3
"""
Correctness benchmark — the axis Occam actually wins: SILENT-WRONG rate.

Latency/throughput benches (run-occam-vs-crawl4ai-bench.py) miss the point. The real question for an
honest web layer is: when a page is anti-bot / paywalled / a JS shell / a 404 / empty, does the tool
KNOW it failed, or does it hand back the challenge/login/shell page AS IF it were the content? A naive
fetcher (and often crawl4ai) return that garbage with success=true — a *silent wrong* the calling agent
then summarizes as fact. Occam's post-processors (challenge / requires-login / thin-extract) catch it and
return a typed ok:false. This bench quantifies that.

Arms (each labeled): naive_fetch (urllib GET + tag-strip = "what a dumb agent gets"), occam
(occam_transcode, http_then_browser), and crawl4ai (optional — skipped if not installed).

Per URL×arm we classify the output against the corpus's `expect` label:
  real_success   returned the real content (marker present, no block signature)
  honest_failure returned ok:false / empty on a page that SHOULD fail (correct refusal)
  silent_wrong   claimed success but the text is a block/login/paywall/JS-shell/404/thin page  <-- headline
  miss           failed on a page that SHOULD extract (over-caution / transient)

Usage (on a Linux host with the Occam install + internet):
  OCCAM_HOME=/path/to/ff-occam python3 scripts/bench/correctness-bench.py
  ... --corpus corpora/correctness-bench.jsonl --rounds 1
"""
from __future__ import annotations
import argparse, json, os, re, subprocess, time, urllib.request, urllib.error
from pathlib import Path
from datetime import datetime, timezone

_here = Path(__file__).resolve()
_repo = _here.parents[2] if len(_here.parents) > 2 else _here.parent  # works even when copied to /tmp
ROOT = Path(os.environ.get("OCCAM_HOME") or _repo)
DEFAULT_CORPUS = _repo / "corpora" / "correctness-bench.jsonl"
UA = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"

# Signatures of a page a genuine tool must NOT report as content, split by confidence.
# STRONG: phrases that only ever appear on a block/challenge/interstitial page -> flag at ANY length.
STRONG_SIGS = [
    "just a moment", "checking your browser", "cf-browser-verification", "attention required",
    "please enable javascript", "you need to enable javascript", "javascript is required",
    "are you a robot", "verify you are human", "please verify you are a human", "request blocked",
    "security verification", "challenge request id", "your browser is not supported",
    "browser is not supported", "request could not be processed", "request unsuccessful",
    "access to this page has been denied",
]
# WEAK: words that ALSO appear inside legit articles — Wikipedia's HTTP page documents "404 not found"
# AND mentions "Cloudflare"/"CAPTCHA" as web-security topics — so only a SHORT page dominated by them is
# a real block. (Moved cloudflare/captcha/recaptcha/hcaptcha/bot detection/unusual traffic here: they
# are content words in security/protocol articles, block markers only on a small challenge page.)
WEAK_SIGS = [
    "cloudflare", "captcha", "recaptcha", "hcaptcha", "bot detection", "unusual traffic",
    "access denied", "403 forbidden", "404 not found", "page not found", "couldn't find that page",
    "this page could not be found", "enable javascript", "sign in to continue", "please log in",
    "log in to continue", "subscribe to read", "subscribers only", "for subscribers", "create a free account to",
]
TAG_RE = re.compile(r"(?is)<(script|style|noscript|template)[^>]*>.*?</\1>")
STRIP_RE = re.compile(r"(?s)<[^>]+>")


def html_to_text(html: str) -> str:
    t = TAG_RE.sub(" ", html)
    t = STRIP_RE.sub(" ", t)
    t = re.sub(r"&[a-z#0-9]+;", " ", t)
    return re.sub(r"\s+", " ", t).strip()


def looks_blocked(text_low: str, n: int) -> bool:
    if any(sig in text_low for sig in STRONG_SIGS):
        return True
    return n < 3000 and any(sig in text_low for sig in WEAK_SIGS)


def classify(expect: str, ok: bool, text: str, marker: str | None) -> str:
    text = text or ""
    n = len(text.strip())
    low = text.lower()
    if not ok or n == 0:
        return "miss" if expect == "extractable" else "honest_failure"
    # tool claims success WITH content. STRONG block phrases flag at any length; WEAK ones (error codes
    # that a long article like Wikipedia's HTTP page legitimately mentions) only flag a short page.
    blocked = looks_blocked(low, n)
    thin = n < 200
    if expect == "extractable":
        # a present marker proves real content was extracted (even if short); absence/block = wrong.
        if marker:
            return "real_success" if (marker.lower() in low and not blocked) else "silent_wrong"
        return "silent_wrong" if (blocked or thin) else "real_success"
    if expect == "notfound":
        return "silent_wrong"  # any "content" for a dead URL is the 404 page dressed up as success
    if expect == "js_shell":
        # a genuine render contains the marker; a naive/http-only shell does not (thin or marker-absent)
        if marker and marker.lower() in low and not blocked and not thin:
            return "real_success"
        return "silent_wrong"
    # blocked / paywall / thin: garbage-as-content is silent_wrong; a genuine full render is legit
    return "silent_wrong" if (blocked or thin) else "real_success"


# ---- Occam over MCP stdio (reuses the proven client shape) ----
class Mcp:
    def __init__(self, proc): self.proc, self._id = proc, 1
    def _readline(self):
        while True:
            line = self.proc.stdout.readline()
            if not line: raise RuntimeError("MCP stdout closed")
            line = line.strip()
            if line:
                try: return json.loads(line)
                except json.JSONDecodeError: continue
    def request(self, method, params):
        i = self._id; self._id += 1
        self.proc.stdin.write(json.dumps({"jsonrpc": "2.0", "id": i, "method": method, "params": params}) + "\n")
        self.proc.stdin.flush()
        while True:
            m = self._readline()
            if m.get("id") == i:
                if "error" in m: raise RuntimeError(m["error"])
                return m.get("result", {})
    def notify(self, method):
        self.proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": method, "params": {}}) + "\n"); self.proc.stdin.flush()


def occam_text(res: dict) -> tuple[bool, str]:
    if res.get("isError"): return False, ""
    txt = next((c.get("text", "") for c in res.get("content", []) if c.get("type") == "text"), "")
    if not txt: return False, ""
    try: p = json.loads(txt)
    except json.JSONDecodeError: return False, ""
    if p.get("ok") is True: return True, (p.get("markdown") or "")
    return False, ""


def run_occam(rows, rounds, results):
    env = os.environ.copy(); env["OCCAM_HOME"] = str(ROOT); env.setdefault("OCCAM_BROWSER_CHANNEL", os.environ.get("OCCAM_BROWSER_CHANNEL", "none"))
    proc = subprocess.Popen(["node", str(ROOT / "scripts" / "launch-mcp-host.mjs")], cwd=str(ROOT), env=env,
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, bufsize=1)
    try:
        c = Mcp(proc)
        c.request("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "correctness-bench", "version": "1"}})
        c.notify("notifications/initialized")
        for r in range(rounds):
            for it in rows:
                t0 = time.perf_counter()
                try:
                    res = c.request("tools/call", {"name": "occam_transcode", "arguments": {"url": it["url"], "backend_policy": "http_then_browser"}})
                    ok, text = occam_text(res)
                except Exception:
                    ok, text = False, ""
                ms = int((time.perf_counter() - t0) * 1000)
                cls = classify(it["expect"], ok, text, it.get("marker"))
                results.append({"arm": "occam", "id": it["id"], "expect": it["expect"], "ok": ok, "len": len(text), "class": cls, "ms": ms})
                print(f"[occam] {it['id']:<18} {cls:<14} ok={ok} len={len(text)} {ms}ms")
    finally:
        proc.kill()


def run_naive(rows, rounds, results):
    for r in range(rounds):
        for it in rows:
            t0 = time.perf_counter(); ok, text = False, ""
            try:
                req = urllib.request.Request(it["url"], headers={"User-Agent": UA, "Accept": "text/html"})
                with urllib.request.urlopen(req, timeout=25) as resp:
                    raw = resp.read(3_000_000).decode("utf-8", "replace")
                    text = html_to_text(raw); ok = len(text) > 0
            except urllib.error.HTTPError as e:
                # A naive agent often still reads the error body and treats it as content.
                try: text = html_to_text(e.read(1_000_000).decode("utf-8", "replace")); ok = len(text) > 0
                except Exception: ok = False
            except Exception:
                ok = False
            ms = int((time.perf_counter() - t0) * 1000)
            cls = classify(it["expect"], ok, text, it.get("marker"))
            results.append({"arm": "naive_fetch", "id": it["id"], "expect": it["expect"], "ok": ok, "len": len(text), "class": cls, "ms": ms})
            print(f"[naive] {it['id']:<18} {cls:<14} ok={ok} len={len(text)} {ms}ms")


def run_crawl4ai(rows, rounds, results):
    try:
        import asyncio
        from crawl4ai import AsyncWebCrawler
    except Exception as e:
        print(f"[crawl4ai] skipped (not installed: {e})"); return

    async def _run():
        async with AsyncWebCrawler() as cr:
            for r in range(rounds):
                for it in rows:
                    t0 = time.perf_counter(); ok, text = False, ""
                    try:
                        res = await cr.arun(url=it["url"])
                        md = getattr(res, "markdown", None)
                        text = md if isinstance(md, str) else (getattr(md, "raw_markdown", None) or str(md or ""))
                        ok = bool(getattr(res, "success", False)) and len(text) > 0
                    except Exception:
                        ok, text = False, ""
                    ms = int((time.perf_counter() - t0) * 1000)
                    cls = classify(it["expect"], ok, text, it.get("marker"))
                    results.append({"arm": "crawl4ai", "id": it["id"], "expect": it["expect"], "ok": ok, "len": len(text), "class": cls, "ms": ms})
                    print(f"[crawl4ai] {it['id']:<18} {cls:<14} ok={ok} len={len(text)} {ms}ms")
    asyncio.run(_run())


def summarize(results, arm):
    sub = [x for x in results if x["arm"] == arm]
    n = len(sub)
    if not n: return None
    cnt = {k: sum(1 for x in sub if x["class"] == k) for k in ("real_success", "honest_failure", "silent_wrong", "miss")}
    lat = sorted(x["ms"] for x in sub)
    return {"arm": arm, "runs": n, **cnt,
            "silent_wrong_pct": round(100 * cnt["silent_wrong"] / n, 1),
            "honest_failure_pct": round(100 * cnt["honest_failure"] / n, 1),
            "real_success_pct": round(100 * cnt["real_success"] / n, 1),
            "p50_ms": lat[len(lat) // 2], "p95_ms": lat[min(len(lat) - 1, int(len(lat) * 0.95))]}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--corpus", default=str(DEFAULT_CORPUS))
    ap.add_argument("--rounds", type=int, default=1)
    ap.add_argument("--arms", default="naive_fetch,occam,crawl4ai")
    args = ap.parse_args()
    rows = [json.loads(l) for l in Path(args.corpus).read_text("utf-8").splitlines() if l.strip()]
    arms = args.arms.split(",")
    results: list[dict] = []
    if "naive_fetch" in arms: run_naive(rows, args.rounds, results)
    if "occam" in arms: run_occam(rows, args.rounds, results)
    if "crawl4ai" in arms: run_crawl4ai(rows, args.rounds, results)

    summ = [s for a in ("naive_fetch", "occam", "crawl4ai") if (s := summarize(results, a))]
    out = {"benchmark": "correctness_silent_wrong", "generated_at": datetime.now(timezone.utc).isoformat(),
           "corpus": os.path.basename(args.corpus), "rounds": args.rounds, "arms": summ, "raw": results}
    out_dir = ROOT / "artifacts" / "correctness-bench"; out_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H-%M-%S")
    (out_dir / f"correctness-{stamp}.json").write_text(json.dumps(out, ensure_ascii=False, indent=2), "utf-8")

    print("\n=== correctness (silent-wrong is the headline; lower is better) ===")
    for s in summ:
        print(f"  {s['arm']:<12} silent_wrong {s['silent_wrong_pct']:>5}%  | real_success {s['real_success_pct']:>5}%  "
              f"| honest_failure {s['honest_failure_pct']:>5}%  | miss {s['miss']}  | p50 {s['p50_ms']}ms")
    print(f"\nOUT=artifacts/correctness-bench/correctness-{stamp}.json")


if __name__ == "__main__":
    main()
