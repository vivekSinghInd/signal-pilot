#!/usr/bin/env python3
"""Run the local Indian equity skill pipeline.

The agent consumes the standalone .NET opportunity service and materializes
artifacts in trader/. It never places an order.
"""

from __future__ import annotations

import argparse
import json
import os
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

from paper_trader import simulate_paper_orders


ROOT = Path(__file__).resolve().parent
TRADER = ROOT / "trader"
STATE = TRADER / "state"
RUNS = ROOT / "runs"
API_URL = os.environ.get(
    "OPPORTUNITY_API_URL", "http://127.0.0.1:5010/api/opportunities"
)


def fetch_opportunities(limit: int) -> dict:
    separator = "&" if "?" in API_URL else "?"
    url = f"{API_URL}{separator}limit={limit}"
    with urllib.request.urlopen(url, timeout=120) as response:
        return json.load(response)


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def new_run_dir() -> Path:
    day = RUNS / datetime.now().astimezone().strftime("%Y-%m-%d")
    day.mkdir(parents=True, exist_ok=True)
    existing = [
        int(path.name.removeprefix("run-"))
        for path in day.glob("run-*")
        if path.name.removeprefix("run-").isdigit()
    ]
    run_dir = day / f"run-{max(existing, default=0) + 1:04d}"
    run_dir.mkdir()
    return run_dir


def run(limit: int, paper: bool) -> int:
    result = fetch_opportunities(limit)
    now = datetime.now(timezone.utc).isoformat()
    candidates = result.get("candidates", [])
    run_dir = new_run_dir()

    watchlist = {
        "mode": "custom-screener",
        "as_of": result.get("generatedAt", now),
        "latest_signal_date": result.get("latestSignalDate"),
        "source": API_URL,
        "scanned": len(candidates),
        "hits": [
            {
                "symbol": item.get("symbol"),
                "price": item.get("recognizedPrice"),
                "score": item.get("preliminaryScore"),
                "market_cap": item.get("marketCapCategory"),
                "evidence": item.get("evidence", []),
                "status": item.get("decision", {}).get("status", "WATCHLIST"),
            }
            for item in candidates
        ],
    }
    write_json(run_dir / "scan.v1.json", watchlist)
    write_json(TRADER / "watchlist.json", watchlist)

    news = {
        "as_of": now,
        "items": [
            {
                "symbol": item.get("symbol"),
                "articles": item.get("news", []),
            }
            for item in candidates
        ],
    }
    write_json(run_dir / "news.v1.json", news)
    write_json(TRADER / "news.json", news)

    research = {
        "as_of": now,
        "posture": "research_triage_only",
        "ai_enabled": result.get("aiEnabled", False),
        "live_orders_enabled": False,
        "candidates": [
            {
                "symbol": item.get("symbol"),
                "score": item.get("preliminaryScore"),
                "decision": item.get("decision", {}),
                "signals": item.get("signals", []),
                "news": item.get("news", []),
            }
            for item in candidates
        ],
    }
    write_json(run_dir / "triage.v1.json", research)
    write_json(TRADER / "research.json", research)

    paper_result = simulate_paper_orders() if paper else {"status": "skipped", "new_orders": []}
    write_json(run_dir / "paper-orders.v1.json", paper_result)

    audit = [
        f"run_dir={run_dir}",
        f"source={API_URL}",
        f"generated_at={now}",
        f"candidate_count={len(candidates)}",
        "broker_action=none",
        "live_orders=false",
        f"paper_mode={'enabled' if paper else 'skipped'}",
        f"paper_new_orders={len(paper_result.get('new_orders', []))}",
    ]
    (run_dir / "audit.log").write_text("\n".join(audit) + "\n", encoding="utf-8")

    print(f"Run artifacts written to {run_dir}")
    print(f"Wrote {len(candidates)} candidates to trader/watchlist.json")
    print(f"Paper mode: {paper_result.get('status')} ({len(paper_result.get('new_orders', []))} new simulated orders)")
    print("No broker action was attempted.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Indian equity skill-agent pipeline")
    subparsers = parser.add_subparsers(dest="command", required=True)
    run_parser = subparsers.add_parser("run", help="scan, enrich, and write artifacts")
    run_parser.add_argument("--limit", type=int, default=12)
    run_parser.add_argument("--no-paper", action="store_true", help="skip the paper-only simulator")
    args = parser.parse_args()
    if args.command == "run":
        return run(max(1, min(args.limit, 50)), paper=not args.no_paper)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
