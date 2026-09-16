#!/usr/bin/env python3
"""DonSeTch WRB adapter with archive=off (live-fetch only).

WRB's upstream runner omits --archive, so the CLI default archive=auto can
credit Wayback snapshots. This overlay keeps the same fetch/search/crawl
shape and forces archive=off for a fair live comparison.
"""

import json
import os
import re
import subprocess
import time

from base import Runner as BaseRunner

DONSETCH_PATH = os.environ.get("DONSETCH_PATH", "donsetch")
ENV = {**os.environ, "PATH": os.path.expanduser("~/.npm-global/bin:") + os.environ.get("PATH", "")}
FETCH_TIMEOUT_S = max(1, int(os.environ.get("OCCAM_WRB_TIMEOUT_MS", "90000")) / 1000)


def count_tokens(text):
    if not text:
        return 0
    return max(1, len(text) // 4)


def run_cmd(args, timeout=60):
    start = time.time()
    try:
        result = subprocess.run(
            args, capture_output=True, text=True, timeout=timeout, env=ENV
        )
        latency = int((time.time() - start) * 1000)
        return result.stdout, result.stderr, latency, result.returncode
    except subprocess.TimeoutExpired:
        latency = int((time.time() - start) * 1000)
        return "", "timeout", latency, -1
    except Exception as e:
        latency = int((time.time() - start) * 1000)
        return "", str(e), latency, -1


class Runner(BaseRunner):
    name = "DonSeTch"

    def fetch(self, url, max_chars=5000):
        stdout, stderr, latency, rc = run_cmd([
            DONSETCH_PATH, "fetch", url,
            "--max-chars", str(max_chars),
            "--archive", "off",
            "--deadline-ms", str(int(FETCH_TIMEOUT_S * 1000)),
        ], timeout=FETCH_TIMEOUT_S)

        content = stdout

        lines = content.split("\n")
        fetch_meta = {}
        content_lines = []
        for line in lines:
            if line.strip().startswith("[fetch]"):
                meta_line = line.strip()
                if "ok" in meta_line:
                    fetch_meta["success"] = True
                elif "error" in meta_line.lower():
                    fetch_meta["success"] = False
                tier_match = re.search(r"tier\s+(\S+)", meta_line)
                if tier_match:
                    fetch_meta["tier"] = tier_match.group(1)
                tok_match = re.search(r"~(\d+)\s*tokens", meta_line)
                if tok_match:
                    fetch_meta["tokens"] = int(tok_match.group(1))
            else:
                content_lines.append(line)

        content = "\n".join(content_lines).strip()
        success = fetch_meta.get("success", len(content) > 100)
        tokens = fetch_meta.get("tokens", count_tokens(content))

        return {
            "success": success,
            "content": content,
            "latency_ms": latency,
            "tokens": tokens,
            "tier": fetch_meta.get("tier", "unknown"),
            "archive": "off",
        }

    def search(self, query, max_results=10):
        stdout, stderr, latency, rc = run_cmd([
            DONSETCH_PATH, "search", query, "--max-results", str(max_results), "--json"
        ], timeout=30)

        try:
            data = json.loads(stdout)
            results = data.get("meta", {}).get("results", [])
            elapsed = data.get("meta", {}).get("elapsed_ms", latency)
            ok = data.get("ok", True)
            content = data.get("content", "")

            parsed_results = []
            for r in results[:max_results]:
                parsed_results.append({
                    "title": r.get("title", ""),
                    "url": r.get("url", ""),
                    "snippet": r.get("snippet", ""),
                })

            return {
                "success": ok,
                "results": parsed_results,
                "latency_ms": elapsed,
                "tokens": count_tokens(content),
            }
        except json.JSONDecodeError:
            return {
                "success": False,
                "results": [],
                "latency_ms": latency,
                "tokens": 0,
            }

    def crawl(self, seed_url, focus, max_pages=30):
        stdout, stderr, latency, rc = run_cmd([
            DONSETCH_PATH, "crawl", seed_url,
            "--topic", focus,
            "--max-pages", str(max_pages),
            "--max-chars", "100000",
            "--json"
        ], timeout=120)

        try:
            data = json.loads(stdout)
            meta = data.get("meta", {})
            pages_meta = meta.get("pages", [])
            elapsed_s = meta.get("elapsed_s", latency / 1000)
            content = data.get("content", "")

            pages = []
            for p in pages_meta:
                pages.append({
                    "url": p.get("url", ""),
                    "title": p.get("title", ""),
                    "content": "",
                })

            return {
                "success": True,
                "pages": pages,
                "latency_ms": int(elapsed_s * 1000),
                "tokens": count_tokens(content),
            }
        except json.JSONDecodeError:
            return {
                "success": False,
                "pages": [],
                "latency_ms": latency,
                "tokens": 0,
            }
