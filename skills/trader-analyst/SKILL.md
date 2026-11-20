---
name: trader-analyst
description: Produce structured research triage from scanner evidence and news.
---

# trader-analyst

Read `trader/watchlist.json` and `trader/news.json`; write
`trader/research.json`.

For every candidate provide:

- status: `STRONG_BUY_CANDIDATE`, `RESEARCH_CANDIDATE`, `WATCHLIST`, or `AVOID`;
- conviction from 0 to 100;
- thesis summary and why now;
- catalyst and evidence;
- upside case and downside case;
- risk flags and missing data;
- what would confirm or kill the setup.

This is research triage, not a final recommendation or order instruction.
