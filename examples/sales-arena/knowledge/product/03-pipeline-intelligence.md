# Pipeline Intelligence

The pipeline-intelligence surface is the operator-facing dashboard
the Suite ships on top of the analytics engine. It lives at
`https://suite.mitchmurray.example/pipeline` (a hosted route)
or embedded as a Power BI / Tableau panel for customers who want it
inside their existing BI tool.

## The four views

### 1. Today's risk list

Every deal where `dna_slip_risk = high` and the rep hasn't logged an
activity in the last 5 business days. Sortable by deal size and stage.
This is the manager's Monday-morning starting point.

### 2. Stage progression

A funnel chart of deals in each stage with their median time-in-stage
and the deal-size distribution. The stage durations are computed from
the customer's own data; the Suite ships no industry benchmark.

### 3. Rep cohort comparison

Reps clustered by tenure, segment, or quota tier. Shows median
deal-cycle length, win rate, and average deal size per cluster. The
clustering is operator-configurable; the Suite ships three default
clusters (new hire / mid-tenure / veteran) the operator can rename
or replace.

### 4. Coaching prompts

For each deal flagged `high` slip risk, the Suite emits a single-
sentence coaching prompt the manager can paste into a 1:1. The prompt
references the actual reason codes from the analytics engine. Example:
*"Acme Corp has gone seven days without a buyer reply after a long
discovery thread — recommend Sarah re-engage with a Loom-style summary
rather than a follow-up email."*

## Refresh cadence

- Real-time signals (stage transitions, meeting bookings) push
  immediately.
- Engagement signals refresh hourly.
- Cohort comparisons refresh nightly.

## Operator overrides

Managers can override any `dna_slip_risk` flag with a single click; the
override is timestamped and the engine learns from it. The next
calibration window weights overridden deals less heavily so the model
adapts to the manager's risk tolerance.

## Related pages

- [Analytics engine](02-analytics-engine.md) — the data model
  underneath this view.
- [Revenue forecasting](04-revenue-forecasting.md) — how the pipeline
  rolls up to a quarter forecast.
- [Integrations](06-integrations.md) — what BI tools embed this view.
