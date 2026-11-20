# Stock Opportunity Automation

Standalone service for the custom screener API. This project is separate from `D:\MyStockMarketTool`.

The project now follows a local skill-and-agent layout inspired by the
referenced `day-trader-skill` repository. See `agents/indian-day-trader.md`,
the `skills/` folders, and `agent.py` for the artifact pipeline.

## What it does

- Reads the latest signals from the configured screener API.
- Scores confluence across strategies, daily/weekly timeframes, RSI, volume, and market-cap category.
- Fetches recent TradingView news for shortlisted symbols.
- Optionally asks OpenAI for a structured research decision.
- Runs a read-only scan every 15 minutes during the Indian market window.
- Starts paper-only simulated trades from qualifying candidates.
- Enforces a maximum notional of ₹20,000 per paper trade.
- Does not place Angel One orders. Live execution remains intentionally disabled.

The artifact chain is `scanner -> news -> analyst -> entry -> exit ->
timemachine -> budget -> risk -> broker -> alerts`. Only scanner/news/analyst
are currently automated; entry, risk, and broker remain gated human steps.

## Run

```powershell
cd D:\StockOpportunityAutomation
$env:ASPNETCORE_URLS = "http://127.0.0.1:5010"
dotnet run --no-launch-profile
```

Endpoints:

- `GET http://127.0.0.1:5010/api/health`
- `GET http://127.0.0.1:5010/api/opportunities?limit=12`

Run the local agent after the service is running:

```powershell
python .\agent.py run --limit 12
```

The default run also starts the paper simulator. It currently uses a score
threshold of 80, at most 3 open paper positions, a 2% stop, a 4% target, and
whole-share sizing that never exceeds ₹20,000 per trade. To run the scan
without opening simulated positions:

```powershell
python .\agent.py run --limit 12 --no-paper
```

It writes the latest convenience files under `trader/` and immutable
versioned artifacts under `runs/YYYY-MM-DD/run-####/`:

- `scan.v1.json`
- `news.v1.json`
- `triage.v1.json`
- `paper-orders.v1.json`
- `audit.log`

The current paper portfolio is stored in `trader/state/paper_portfolio.json`
and the latest simulated orders are in `trader/paper_orders.json`. Paper
entries use the scanner's recognized price as the simulated fill; they are not
live market fills. Configuration is in `config/paper.json`.

## Enable LLM analysis

Keep secrets out of source control:

```powershell
$env:OpenAI__Enabled = "true"
$env:OpenAI__ApiKey = "YOUR_OPENAI_API_KEY"
$env:OpenAI__Model = "gpt-6.2-astra"
$env:OpenAI__ReasoningEffort = "high"
```

Angel One credentials, if later needed for a separate paper/live execution adapter, should also be supplied only through environment variables. No credentials are copied from the original project.
