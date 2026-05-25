# Revenue Forecasting

The Suite produces a quarterly revenue forecast that competes directly
with the manual rep-rollup forecast every sales org already runs. The
two forecasts are surfaced side-by-side in the executive view so the
operator can see where they diverge and ask why.

## How the forecast is built

- Each deal in the pipeline carries a `dna_forecast_band` from the
  analytics engine (one of `<50%`, `50-79%`, `80-95%`, `>95%`).
- The Suite multiplies each deal's amount by the band midpoint
  (e.g., a $80,000 deal at `50-79%` contributes `$80,000 × 0.645 =
  $51,600` to the forecast).
- The Suite sums the contributions across all deals expected to close
  in the quarter.
- The output is a single quarter total plus a 90% confidence interval
  derived from the bands' historical accuracy on this customer.

## How it differs from the rep rollup

| Dimension | Rep rollup | Suite forecast |
| --- | --- | --- |
| Source | Rep self-reports per deal | Engagement + pipeline-event signals |
| Bias | Tends optimistic; sandbagging exists in some orgs | Calibrated to customer's own historical outcomes |
| Refresh | Weekly (or whenever the manager nags) | Nightly |
| Confidence interval | None | 90% interval from historical accuracy |

The Suite does **not** replace the rep rollup. Both forecasts are
shipped. The operator decides which to commit to the CFO.

## Forecast accuracy expectations

In the first 90 days, the Suite's forecast has no calibration window
and the confidence interval is wide (`±25%`). After three quarters of
data, the interval typically narrows to `±8-12%` on customers with
clean activity logging. Customers with chronic under-logging see
wider intervals — the Suite cannot predict deals that aren't in the
CRM.

## Honest claim

The Suite does not guarantee a percentage forecast improvement. We
ship the methodology and the interval; customers measure their own
accuracy delta against their pre-Suite forecast. A case-study client
([Stratham Logistics](../case-studies/03-stratham-logistics.md))
reports an interval narrowing from `±22%` to `±9%` over four
quarters; another ([Hartfield Industries](../case-studies/07-hartfield-industries.md))
saw smaller improvement (±18% → ±14%) because their data was noisier.

## Related pages

- [Pipeline intelligence](03-pipeline-intelligence.md) — the
  per-deal scores that roll up here.
- [Roadmap](09-roadmap.md) — multi-quarter forecasting is on the
  roadmap for Q4.
