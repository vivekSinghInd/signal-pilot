---
name: trader-risk
description: Gate every exact order proposal against configured risk limits.
---

# trader-risk

Return `PASS` or `BLOCK` for the exact order fingerprint. Check stop presence,
loss at stop, reward-to-risk, daily loss, trades per day, cash, position cap,
market window, duplicate orders, and blocked symbols. A PASS is not user
approval and never sends an order.
