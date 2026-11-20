---
name: trader-entry
description: Define a confirmable entry trigger for a selected research candidate.
---

# trader-entry

Entry is not automatic. For a user-selected symbol, require fresh price data,
an explicit trigger, an entry window, and an invalidation level. A scanner
signal alone cannot trigger an entry. Do not generate an order until the user
has selected the candidate and the trigger has occurred.
