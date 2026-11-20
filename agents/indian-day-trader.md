# Indian Day-Trader Agent

This agent follows the local skill chain inspired by `reference/day-trader-skill`:

```text
trader-scanner -> trader-news -> trader-analyst -> trader-entry
               -> trader-exit -> trader-timemachine -> trader-budget
               -> trader-risk -> trader-broker -> trader-alerts
```

The current implementation is intentionally read-only through the research
stage. The broker skill can prepare a ticket later, but no automated or
background process may submit an Angel One order.

## Required gates

1. Scanner signal is current and comes from the configured screener API.
2. News is attached to the same symbol and timestamp window.
3. Analyst output separates evidence, assumptions, catalysts, and risk flags.
4. Entry and exit plan contain an explicit stop, target, and expiry.
5. Position sizing respects max loss and available capital.
6. Risk returns PASS for the exact order fingerprint.
7. User explicitly confirms that exact ticket.
8. Only then may a broker adapter be called; live mode requires a separate deliberate configuration change.

`agent.py run --limit 12` writes convenience copies under `trader/` and a
versioned run under `runs/YYYY-MM-DD/run-####/` containing `scan.v1.json`,
`news.v1.json`, `triage.v1.json`, and `audit.log`.
