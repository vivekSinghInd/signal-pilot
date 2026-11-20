---
name: trader-broker
description: Prepare an Angel One ticket only after risk PASS and explicit confirmation.
---

# trader-broker

This is the only skill allowed to reach a broker adapter. It must remain paper
mode by default. Required gates:

1. exact order passed `trader-risk` today;
2. user explicitly confirms that one ticket;
3. a separate tool permission/approval is granted for that call.

Never place orders from the scheduled agent, never accept "yes to all", never
read or print credentials, and never switch live mode implicitly.
