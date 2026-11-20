---
name: trader-paper
description: Simulate paper entries with a hard ₹20,000 maximum capital per trade.
---

# trader-paper

Paper mode is the only execution mode enabled by default. It reads the
research artifact, calculates whole-share quantity from the configured entry
price, and enforces `max_capital_per_trade` before recording a simulated fill.

Default configuration:

- maximum notional per trade: ₹20,000;
- maximum open paper positions: 3;
- minimum research score: 80;
- stop-loss: 2% below simulated entry;
- target: 4% above simulated entry;
- flat-by time: 15:20 IST.

Paper fills are simulations at the scanner's recognized price. They are not
Angel One orders and must not be described as live fills.
