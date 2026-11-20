---
name: trader-scanner
description: Read the custom Indian screener API and create a ranked research watchlist.
---

# trader-scanner

The scanner identifies symbols that matched the user's custom TradingView
indicator. It does not call a stock a buy. It writes `trader/watchlist.json`
with the signal date, screeners, timeframes, RSI values, volume, price, and
market-cap category.

Source: `Scanner:ResultsUrl` or `OPPORTUNITY_API_URL`.

Rules:

- Use only the latest `recognizeDate` returned by the API.
- Prefer multi-screener confluence and daily plus weekly confirmation.
- Exclude MicroCap by default.
- Treat missing RSI values as a data gap.
- Label results as research candidates or watchlist items, never guaranteed buys.
