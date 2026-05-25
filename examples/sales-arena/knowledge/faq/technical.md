# FAQ — Technical

## How does the analytics engine work?

See [`product/02-analytics-engine.md`](../product/02-analytics-engine.md).
Short version: nightly batch job over CRM + activity + email-metadata
signals, calibrated against the customer's own closed-won history, with
a per-deal slip-risk score and a forecast band that rolls up to a
quarterly confidence interval.

## What does "calibration window" mean?

The first 90 days (Starter) or 45 days (Pro/Enterprise) after
installation. During the window the engine emits scores as
advisory-only and the forecast band defaults to `<50%` (very wide).
After the window, the engine's confidence narrows based on the
customer's actual closed-won outcomes.

## Why don't you read email bodies by default?

Metadata is enough for the signal the Suite wants — open rates,
reply latency, thread length, time-to-first-reply. Reading bodies
adds privacy/security review weight without proportional analytics
lift. Customers who want body indexing can opt in per the documented
consent flow.

## What's the data residency?

US tenants in `us-east-1` and `us-west-2`. EU tenants in `eu-central-1`
(Frankfurt) and `eu-west-1` (Dublin). EU data does not cross to US
regions. Enterprise tier supports custom residency. See
[`product/07-security-and-compliance.md`](../product/07-security-and-compliance.md).

## Do you write back to my CRM?

Yes, as custom fields (`dna_health`, `dna_slip_risk`,
`dna_engagement`, `dna_nba`, `dna_forecast_band`, `dna_reason_codes`).
Customers can disable any of the writebacks if their CRM admin
prefers.

## Can I export the data?

Yes. The Suite ships a daily JSON / CSV export to a customer-chosen
S3 bucket, GCS bucket, or Azure Blob container. The Pro and
Enterprise tiers include the export; Starter is hosted-dashboard
only.

## What about API access?

REST API on the Pro and Enterprise tiers. Rate-limited at 60
req/min per environment by default; higher limits available on
Enterprise.

## How do you handle CRM stage changes mid-deal?

The engine reads the full stage transition history (not just the
current stage). Mid-deal stage changes are first-class signals.

## What programming language is the engine written in?

C# (.NET) on top of the `DotNetAgents.Knowledge` and
`DotNetAgents.Database.Learning` packages. The full Mitch & Murray
Analytics Suite is internal IP; the public framework underneath is
the open-source DotNetAgents toolkit.

## What's the SLA?

99.9% uptime SLA on Pro and Enterprise. Starter is best-effort with
business-hours support. Enterprise includes a 4-hour business-day
incident-response commit and 24/7 high-severity coverage for the
`dna_slip_risk = high` flag on deals above an operator-defined
threshold.
