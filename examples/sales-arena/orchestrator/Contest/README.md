# Contest Lifecycle + Rules Engine

Story `6edc804e` (SA-02-05). Deterministic, in-process contest lifecycle for the
Sales Arena ([SALES-ARENA-FLAGSHIP-PLAN.md §6.1 + §13](../../../../examples/sales-arena/README.md)).

## State machine

```
Uninitialized → Initialized → Running ⇄ Paused → Ended
```

`Init(config)` creates a contest from a `ContestConfig` (name, leadsPackRef, personas[],
durationHours, prizeTier, timeCompressionFactor). Default `TimeCompressionFactor` is
`1.0`; demo replay uses `60.0` to compress one simulated hour into one real minute.

`Pause/Resume` preserves the leaderboard — scores are not zeroed across the boundary.
`End` is allowed from `Running`, `Paused`, or `Initialized` (never from `Uninitialized`
or `Ended`). All transitions append a `ContestPhaseChangedEvent` to the per-contest
phase log.

`AccumulatedSimulatedRunTime` tracks total elapsed simulated time so demo replays can
ship a stable wall-clock-to-simulated-clock conversion regardless of pause/resume.

## Rules engine

Five starter rules (`RulesEngine` defaults):

1. **no-double-touch** — same lead touched by two personas in one contest.
2. **send-rate-cap** — outbound sends exceeding the per-hour cap.
3. **blackout-hours** — outbound sends inside any blackout window.
4. **scoring-locked-mid-contest** — scoring config changes while Running/Paused.
5. **persona-active-set-locked** — persona set changes while Running/Paused.

Each rule emits a `RuleViolation { RuleId, Message }` or `null`. The engine returns
the first violation; rules can be replaced by passing a custom list to the constructor.

## Deferred to follow-up

- Persistence into the SA-02-03 ArenaLedger event stream (current ledger is in-memory).
- Real `IContestRuleEvaluationContext` adapters wired into the Orchestrator pod
  manager (SA-02-01); the in-memory test fakes here are illustrative only.
- Operator override receipts for rule-violation acks during a live demo.
- Wall-clock scheduling that ends contests automatically when `DurationHours` elapses
  in simulated time.
