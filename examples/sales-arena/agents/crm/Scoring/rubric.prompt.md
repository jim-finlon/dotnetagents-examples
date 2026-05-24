# Sales Arena lead scoring rubric (SA-01-03)

Score each lead on three axes from 0-100:

- **Fit** — firmographic match vs the supplied ICP (industry, region, headcount).
- **Intent** — engagement signals (visits, downloads, reply sentiment).
- **Power** — decision-maker authority of the primary contact.

Return JSON-shaped reasoning in prose; hosts may swap the backing model via DotNetAgents.PromptRuntime.

Premium tease: pass `premium_routing=true` to route through AEQ model selection (feature-flag pass-through only).
