#!/usr/bin/env python3
"""Paper-only position planner with a hard per-trade capital cap."""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parent
CONFIG_PATH = ROOT / "config" / "paper.json"
RESEARCH_PATH = ROOT / "trader" / "research.json"
STATE_PATH = ROOT / "trader" / "state" / "paper_portfolio.json"
ORDERS_PATH = ROOT / "trader" / "paper_orders.json"


def load_json(path: Path, default: object) -> object:
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def simulate_paper_orders() -> dict:
    config = load_json(CONFIG_PATH, {})
    research = load_json(RESEARCH_PATH, {})
    state = load_json(STATE_PATH, {"mode": "paper", "open_positions": [], "trade_log": []})

    if not config.get("enabled", False) or not config.get("paper_mode", False):
        return {"status": "disabled", "new_orders": [], "blocked": []}

    max_capital = float(config.get("max_capital_per_trade", 20000))
    max_positions = int(config.get("max_open_positions", 3))
    min_score = float(config.get("min_score", 80))
    stop_pct = float(config.get("stop_loss_percent", 2)) / 100
    target_pct = float(config.get("target_percent", 4)) / 100
    open_positions = state.setdefault("open_positions", [])
    open_symbols = {str(position.get("symbol", "")).upper() for position in open_positions}
    candidates = research.get("candidates", [])
    new_orders = []
    blocked = []

    for candidate in candidates:
        symbol = str(candidate.get("symbol", "")).upper()
        score = float(candidate.get("score", 0) or 0)
        signals = candidate.get("signals", [])
        entry = float(signals[0].get("recognizedPrice", 0) or 0) if signals else 0

        if score < min_score:
            blocked.append({"symbol": symbol, "reason": "score below paper threshold"})
            continue
        if not symbol or entry <= 0:
            blocked.append({"symbol": symbol, "reason": "missing symbol or entry price"})
            continue
        if symbol in open_symbols:
            blocked.append({"symbol": symbol, "reason": "paper position already open"})
            continue
        if len(open_positions) + len(new_orders) >= max_positions:
            blocked.append({"symbol": symbol, "reason": "maximum open paper positions reached"})
            continue

        quantity = int(max_capital // entry)
        if quantity < 1:
            blocked.append({"symbol": symbol, "reason": "one share exceeds per-trade capital cap"})
            continue

        notional = round(quantity * entry, 2)
        stop = round(entry * (1 - stop_pct), 2)
        target = round(entry * (1 + target_pct), 2)
        fingerprint = hashlib.sha256(f"{symbol}|{quantity}|{entry}|{stop}|{target}".encode()).hexdigest()[:16]
        new_orders.append({
            "id": fingerprint,
            "symbol": symbol,
            "exchange": config.get("exchange", "NSE"),
            "side": "BUY",
            "quantity": quantity,
            "entry_price": entry,
            "stop_loss": stop,
            "target": target,
            "notional": notional,
            "max_capital_per_trade": max_capital,
            "flat_by": config.get("flat_by", "15:20"),
            "status": "PAPER_OPEN",
            "created_at": datetime.now(timezone.utc).isoformat(),
            "broker_action": "none",
        })

    open_positions.extend(new_orders)
    state["mode"] = "paper"
    state["max_capital_per_trade"] = max_capital
    state["last_updated"] = datetime.now(timezone.utc).isoformat()
    save_json(STATE_PATH, state)
    save_json(ORDERS_PATH, {"mode": "paper", "orders": new_orders, "blocked": blocked})
    return {"status": "ok", "new_orders": new_orders, "blocked": blocked}


if __name__ == "__main__":
    print(json.dumps(simulate_paper_orders(), indent=2))
