# Case Study — Fenwick Industrial

> **Industry.** Industrial-automation hardware + service contracts ·
> **Headcount.** ~620 · **Reps.** 84 (across two business units) ·
> **CRM.** Custom (Snowflake-backed) · **Tier.** Enterprise

## The before

Fenwick's pipeline lived in a homegrown CRM their internal team
maintained on Snowflake — the standard Salesforce / HubSpot
connectors didn't apply. Their CFO, **Halvor Bergstrom**, was
skeptical of the Suite's claim that a warehouse-mode connector could
fit their schema in under three weeks. Two of his analysts had
estimated 9 weeks.

## What changed

The Suite's data-engineering team owned the warehouse-mode connector
implementation under the Enterprise tier's paid-implementation
engagement. The schema-mapping file took 14 business days from
kickoff to production calibration. Halvor's analysts did a parallel
implementation; the Suite team finished first.

## The measured outcome

Fenwick runs the Suite as the primary forecast for one of their two
business units (the hardware side; the services side is too
project-based for the current calibration model). Forecast accuracy
for the hardware unit moved from `±28%` to `±12%` over six
quarters.

## Quotable

> *"I expected to be ripping out a half-built integration in six
> weeks. I'm running it in production six quarters later."* — Halvor
> Bergstrom, CFO, Fenwick Industrial

## Why this story matters

The custom-CRM-via-warehouse story. Reps working an Enterprise
prospect on a custom CRM should reach for Fenwick to prove the
warehouse-mode connector ships in production-time, not consulting-
project-time.

## Honest caveat

Fenwick's services-business pipeline did not benefit from the Suite —
project-based revenue with single-customer concentrations doesn't fit
the calibration model. The case study explicitly names this in the
quote-able section above; reps should not pretend the Suite works
for every revenue shape.
