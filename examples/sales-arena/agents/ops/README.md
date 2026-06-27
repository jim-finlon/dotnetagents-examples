# Sales-Pod Ops Agent

Thin, deterministic implementation of the **Sales-Pod Ops Agent** from the Sales
Arena flagship plan ([SALES-ARENA-FLAGSHIP-PLAN.md §5](../../../../examples/sales-arena/README.md)).
Role: contest setup and the per-rep daily queue.

## Surface

- `IOpsAgent` — interface
  - `SetupContest(ContestRequest) -> ContestPlan`
  - `BuildDailyQueue(BuildQueueRequest) -> DailyQueue`
- `OpsAgent` — deterministic implementation. No LLM, no external HTTP.
  Accepts a clock delegate for testable time.
- Records: `ContestRequest`, `ContestPlan`, `BuildQueueRequest`, `DailyQueue`, `QueueItem`.

## Behavior

`SetupContest` validates the request (non-blank name, at least one non-blank
persona, positive duration), computes the UTC start/end window from the
optional `StartUtc` (default: now) and `DurationHours`, and returns a plan with
the persona seat count and prize tier echo.

`BuildDailyQueue` deduplicates the lead handles, orders them ordinally for
deterministic replay, optionally caps the count to `OptCap` (0 means no cap),
and assigns 1-based positions.

## Slice scope (story aa2dc010 — child of SA-01-08 / 77f86038)

This child ships only the Ops Agent. Sibling supporting-cast agents (Research,
Proposal, Invoice, Knowledge, Training) remain under the parent for follow-up
slices. Defer to follow-ups:

- Live wiring to `IContestLifecycle` once SA-02-05 lands.
- Real CRM queue source once SA-01-01 publishes a stable lead-handle stream.
- Communications Agent delegation for outbound notifications.
