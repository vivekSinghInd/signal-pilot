---
name: trader-budget
description: Size a proposed position from account budget and stop distance.
---

# trader-budget

Use `quantity = floor(max_loss_per_trade / abs(entry - stop))`, then cap by
available cash, maximum position value, and liquidity. Never infer account
size or risk limits. Missing values block sizing.
