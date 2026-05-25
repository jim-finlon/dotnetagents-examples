# Analytics Engine

The Suite's analytics engine is the data-model + scoring layer that
turns raw CRM activity into pipeline signals. It runs nightly on the
customer's data warehouse (or, on Starter tier, in a hosted analytics
sandbox) and ships per-deal scores back to the CRM as custom fields.

## Inputs the engine reads

- Deal record (stage, amount, close date, owner, fields).
- Activity log (email send/reply, meeting booked, call logged, demo
  delivered).
- Pipeline event stream (stage transitions with timestamps).
- Email-engagement signals (open rates, reply rates, thread length).
- Calendar signals (proposed → booked conversion, meeting cancel rate).
- Proposal acceptance signals (proposal sent → opened → forwarded →
  signed).

The engine does **not** read message bodies by default. The Starter
tier is metadata-only; bodies opt-in per the data-residency contract.

## Outputs the engine writes

| Output | Field name | Range |
| --- | --- | --- |
| Health score | `dna_health` | 0–100 |
| Slip risk | `dna_slip_risk` | low / medium / high |
| Buyer engagement | `dna_engagement` | 0–10 |
| Next-best-action | `dna_nba` | enum (call / email / proposal / wait) |
| Forecast confidence | `dna_forecast_band` | <50% / 50-79% / 80-95% / >95% |

Every output carries a `dna_reason_codes` JSON column listing the top
three signals that drove the score, so reps can see *why* the engine
flagged the deal at risk.

## Calibration

Scores are calibrated against the customer's own historical close
outcomes. The first 90 days of data are the calibration window; before
that, the engine emits scores as advisory-only and the forecast band
defaults to `<50%`.

## What the engine deliberately does not score

- Rep performance.
- Lead quality at the top of funnel (a separate input that the engine
  consumes; it does not generate it).
- Sentiment in conversation bodies (the engine reads metadata; sentiment
  scoring is a separate opt-in).

## Related pages

- [Pipeline intelligence](03-pipeline-intelligence.md) — how the
  scores roll up to the pipeline view.
- [Revenue forecasting](04-revenue-forecasting.md) — how the forecast
  band turns into a per-quarter commit number.
- [Security and compliance](07-security-and-compliance.md) — what
  the engine reads and where it stores it.
