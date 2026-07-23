#!/usr/bin/env python3
"""
Honest Occam vs crawl4ai benchmark (no-VPN corpus).

Compares three explicitly labeled arms on the same URLs:
  - occam_browser          — Occam backend_policy=browser (Playwright path)
  - occam_http_then_browser — Occam default escalation policy
  - crawl4ai_playwright    — crawl4ai AsyncWebCrawler (always browser)

Usage:
  python scripts/run-occam-vs-crawl4ai-bench.py
  python scripts/run-occam-vs-crawl4ai-bench.py --rounds=10
  python scripts/run-occam-vs-crawl4ai-bench.py --corpus=corpora/no-vpn-bench.jsonl
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import statistics
import subprocess
import threading
import time
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path

import psutil
from crawl4ai import AsyncWebCrawler

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CORPUS = ROOT / "corpora" / "no-vpn-bench.jsonl"


@dataclass
class RunStat:
    arm: str
    url: str
    round: int
    ms: int
    ok: bool
    markdown_len: int
    backend_used: str | None = None
    failure: str | None = None


class MemorySampler:
    def __init__(self, root_pid: int | None = None) -> None:
        self._root_pid = root_pid
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self.peak_rss_mb = 0.0

    def start(self) -> None:
        self._thread = threading.Thread(target=self._loop, daemon=True)
        self._thread.start()

    def stop(self) -> float:
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=2)
        return round(self.peak_rss_mb, 1)

    def _loop(self) -> None:
        while not self._stop.is_set():
            try:
                rss = self._collect_rss_mb()
                if rss > self.peak_rss_mb:
                    self.peak_rss_mb = rss
            except Exception:
                pass
            time.sleep(0.05)

    def _collect_rss_mb(self) -> float:
        if self._root_pid is None:
            procs = [psutil.Process(), *psutil.Process().children(recursive=True)]
        else:
            root = psutil.Process(self._root_pid)
            procs = [root, *root.children(recursive=True)]
        total = 0
        for p in procs:
            try:
                total += p.memory_info().rss
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        return total / (1024 * 1024)


class McpClient:
    def __init__(self, proc: subprocess.Popen[str]) -> None:
        self.proc = proc
        self._id = 1

    def _read_line(self) -> dict:
        while True:
            line = self.proc.stdout.readline()
            if not line:
                raise RuntimeError("MCP host stdout closed")
            line = line.strip()
            if not line:
                continue
            try:
                return json.loads(line)
            except json.JSONDecodeError:
                continue

    def request(self, method: str, params: dict) -> dict:
        msg_id = self._id
        self._id += 1
        payload = {"jsonrpc": "2.0", "id": msg_id, "method": method, "params": params}
        self.proc.stdin.write(json.dumps(payload) + "\n")
        self.proc.stdin.flush()
        while True:
            msg = self._read_line()
            if msg.get("id") != msg_id:
                continue
            if "error" in msg:
                raise RuntimeError(f"MCP error: {msg['error']}")
            return msg.get("result", {})

    def notify(self, method: str, params: dict | None = None) -> None:
        payload = {"jsonrpc": "2.0", "method": method, "params": params or {}}
        self.proc.stdin.write(json.dumps(payload) + "\n")
        self.proc.stdin.flush()


def load_corpus(path: Path) -> list[dict]:
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line:
            rows.append(json.loads(line))
    return rows


def pct(values: list[int], p: float) -> int:
    if not values:
        return 0
    s = sorted(values)
    return s[int((len(s) - 1) * p)]


def is_browser_backend(name: str | None) -> bool:
    if not name:
        return False
    v = name.lower()
    return "browser" in v or "playwright" in v or "chromium" in v


def parse_occam_result(result: dict) -> tuple[bool, int, str | None, str | None]:
    if result.get("isError"):
        txt = next((c.get("text", "") for c in result.get("content", []) if c.get("type") == "text"), "")
        return False, 0, None, txt or "tool_error"
    txt = next((c.get("text", "") for c in result.get("content", []) if c.get("type") == "text"), "")
    if not txt:
        return False, 0, None, "empty_tool_text"
    payload = json.loads(txt)
    backend = payload.get("backend")
    if payload.get("ok") is True:
        md = payload.get("markdown") or ""
        return True, len(md), backend, None
    failure = (payload.get("failure") or {}).get("code") or payload.get("failureCode") or "failed"
    return False, 0, backend, str(failure)


def summarize_arm(stats: list[RunStat], arm: str) -> dict:
    subset = [s for s in stats if s.arm == arm]
    lat = [s.ms for s in subset]
    ok = [s for s in subset if s.ok]
    ok_lat = [s.ms for s in ok]
    browser_ok = [s for s in ok if is_browser_backend(s.backend_used)]
    browser_lat = [s.ms for s in browser_ok]
    return {
        "arm": arm,
        "runs": len(subset),
        "ok_runs": len(ok),
        "success_rate_pct": round((len(ok) / len(subset) * 100.0), 1) if subset else 0.0,
        "browser_backend_ok_runs": len(browser_ok),
        "browser_backend_rate_pct_ok": round((len(browser_ok) / len(ok) * 100.0), 1) if ok else 0.0,
        "p50_ms_all": pct(lat, 0.5),
        "p95_ms_all": pct(lat, 0.95),
        "p50_ms_ok": pct(ok_lat, 0.5),
        "p95_ms_ok": pct(ok_lat, 0.95),
        "p50_ms_browser_ok": pct(browser_lat, 0.5),
        "p95_ms_browser_ok": pct(browser_lat, 0.95),
        "mean_ms_all": round(statistics.fmean(lat), 1) if lat else 0.0,
        "median_markdown_len_ok": int(statistics.median([s.markdown_len for s in ok])) if ok else 0,
        "top_failures": _top_failures(subset),
    }


def _top_failures(subset: list[RunStat], limit: int = 6) -> list[dict]:
    counts: dict[str, int] = {}
    for row in subset:
        if not row.ok:
            key = row.failure or "unknown"
            counts[key] = counts.get(key, 0) + 1
    return [{"code": k, "count": v} for k, v in sorted(counts.items(), key=lambda x: -x[1])[:limit]]


def run_occam_arm(
    stats: list[RunStat],
    corpus: list[dict],
    rounds: int,
    arm: str,
    backend_policy: str,
) -> float:
    env = os.environ.copy()
    env["OCCAM_HOME"] = str(ROOT)
    proc = subprocess.Popen(
        ["node", str(ROOT / "scripts" / "launch-mcp-host.mjs")],
        cwd=str(ROOT),
        env=env,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        encoding="utf-8",
        bufsize=1,
    )
    sampler = MemorySampler(root_pid=proc.pid)
    sampler.start()
    try:
        if proc.stdin is None or proc.stdout is None:
            raise RuntimeError("Failed to start MCP host pipes")
        client = McpClient(proc)
        client.request(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "occam-vs-crawl4ai-honest-bench", "version": "1.0"},
            },
        )
        client.notify("notifications/initialized")
        for r in range(1, rounds + 1):
            for item in corpus:
                url = item["url"]
                t0 = time.perf_counter()
                try:
                    res = client.request(
                        "tools/call",
                        {
                            "name": "occam_transcode",
                            "arguments": {"url": url, "backend_policy": backend_policy},
                        },
                    )
                    elapsed = int((time.perf_counter() - t0) * 1000)
                    ok, md_len, backend_used, failure = parse_occam_result(res)
                    stats.append(
                        RunStat(arm, url, r, elapsed, ok, md_len, backend_used, failure)
                    )
                    print(
                        f"[{arm}] r{r} {item.get('id', url)}: ok={ok} "
                        f"backend={backend_used or '-'} t={elapsed}ms"
                    )
                except Exception as ex:
                    elapsed = int((time.perf_counter() - t0) * 1000)
                    stats.append(RunStat(arm, url, r, elapsed, False, 0, None, str(ex)))
                    print(f"[{arm}] r{r} {item.get('id', url)}: error t={elapsed}ms")
    finally:
        proc.kill()
        return sampler.stop()


async def run_crawl4ai_arm(stats: list[RunStat], corpus: list[dict], rounds: int) -> float:
    arm = "crawl4ai_playwright"
    sampler = MemorySampler()
    sampler.start()
    try:
        async with AsyncWebCrawler() as crawler:
            for r in range(1, rounds + 1):
                for item in corpus:
                    url = item["url"]
                    t0 = time.perf_counter()
                    try:
                        result = await crawler.arun(url=url)
                        elapsed = int((time.perf_counter() - t0) * 1000)
                        markdown = getattr(result, "markdown", None)
                        if isinstance(markdown, str):
                            text = markdown
                        else:
                            text = (
                                getattr(markdown, "raw_markdown", None)
                                or getattr(markdown, "markdown", None)
                                or str(markdown or "")
                            )
                        ok = bool(getattr(result, "success", False)) and len(text) > 0
                        stats.append(
                            RunStat(
                                arm,
                                url,
                                r,
                                elapsed,
                                ok,
                                len(text),
                                "playwright",
                                None if ok else "failed",
                            )
                        )
                        print(f"[{arm}] r{r} {item.get('id', url)}: ok={ok} t={elapsed}ms")
                    except Exception as ex:
                        elapsed = int((time.perf_counter() - t0) * 1000)
                        stats.append(RunStat(arm, url, r, elapsed, False, 0, "playwright", str(ex)))
                        print(f"[{arm}] r{r} {item.get('id', url)}: error t={elapsed}ms")
    finally:
        return sampler.stop()


def main() -> None:
    parser = argparse.ArgumentParser(description="Honest Occam vs crawl4ai benchmark")
    parser.add_argument("--rounds", type=int, default=int(os.environ.get("BENCH_ROUNDS", "5")))
    parser.add_argument("--corpus", type=str, default=str(DEFAULT_CORPUS))
    args = parser.parse_args()

    corpus_path = Path(args.corpus)
    corpus = load_corpus(corpus_path)
    stats: list[RunStat] = []

    peak_occam_browser = run_occam_arm(stats, corpus, args.rounds, "occam_browser", "browser")
    peak_occam_escalate = run_occam_arm(
        stats, corpus, args.rounds, "occam_http_then_browser", "http_then_browser"
    )
    peak_crawl4ai = asyncio.run(run_crawl4ai_arm(stats, corpus, args.rounds))

    arms = {
        "occam_browser": {**summarize_arm(stats, "occam_browser"), "peak_rss_mb": peak_occam_browser},
        "occam_http_then_browser": {
            **summarize_arm(stats, "occam_http_then_browser"),
            "peak_rss_mb": peak_occam_escalate,
        },
        "crawl4ai_playwright": {
            **summarize_arm(stats, "crawl4ai_playwright"),
            "peak_rss_mb": peak_crawl4ai,
        },
    }

    out = {
        "benchmark": "honest_occam_vs_crawl4ai",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "corpus": str(corpus_path.relative_to(ROOT)).replace("\\", "/"),
        "rounds": args.rounds,
        "notes": {
            "occam_browser": "Occam forced browser path (Playwright).",
            "occam_http_then_browser": "Occam may stay on HTTP when sufficient; reports actual backend per run.",
            "crawl4ai_playwright": "crawl4ai AsyncWebCrawler always uses Playwright/Chromium.",
            "fair_browser_compare": "Compare occam_browser vs crawl4ai_playwright on p50_ms_ok / p95_ms_ok.",
        },
        "arms": arms,
        "raw": [asdict(s) for s in stats],
    }

    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H-%M-%S")
    out_dir = ROOT / "artifacts" / "occam-vs-crawl4ai-bench"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"honest-{stamp}.json"
    out_path.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")

    print("\n=== honest occam vs crawl4ai ===")
    for name, summary in arms.items():
        print(name, json.dumps(summary, ensure_ascii=False))
    print(f"OUT={out_path}")


if __name__ == "__main__":
    main()
