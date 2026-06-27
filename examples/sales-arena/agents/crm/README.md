# SalesArena.Crm.Agent

> *"The leads are weak? The pipeline isn't."*

The CRM Agent's foundation — the 11-stage lead lifecycle, the per-lead state
machine, and the append-only activity log. Every other Arena agent talks to
the pipeline through this surface.

## The 11-stage lifecycle

```
Lead  →  Researched  →  Contacted  →  Qualified  →  DemoBooked  →
DemoHeld  →  ProposalSent  →  Negotiating  →  ClosedWon | ClosedLost | Nurture
```

- **Active** stages can transition to most other active stages or terminal
- **`ClosedWon` / `ClosedLost`** are absorbing terminals — no further transitions
- **`Nurture`** is a *re-engageable* terminal — a re-signalling prospect can
  transition `Nurture → Contacted` or `Nurture → Researched` (signal-driven)

See `CrmPipelineStateMachine.LegalTransitions` for the canonical edge list.

## Key types

| Type | What it is |
|---|---|
| `CrmStages` | The 11 stage constants + terminal-set helper |
| `CrmRecord` | Mutable per-lead state (Stage + Persona + Metadata) |
| `CrmStageChangedEvent` | The event emitted on every accepted transition |
| `ICrmEventPublisher` | Pub/sub seam for stage-changed events |
| `InMemoryCrmEventPublisher` | Synchronous in-proc default |
| `CrmPipelineStateMachine` | The validator + writer; wraps `DotNetAgents.Agents.StateMachines.AgentStateMachine` |
| `IActivityLog` / `SqliteActivityLog` | Append-only per-lead activity log |
| `ActivityLogEntry` | One row in the activity log |
| `CrmStateException` | Structured error with stable `Code` |

## Usage

```csharp
var publisher = new InMemoryCrmEventPublisher();
publisher.StageChanged += (_, e) => Console.WriteLine($"{e.LeadId}: {e.FromStage} -> {e.ToStage}");

await using var log = new SqliteActivityLog("Data Source=./.arena/activity.db");
var pipeline = new CrmPipelineStateMachine(publisher, log);

var lead = new CrmRecord
{
    LeadId = "L-0001",
    Stage = CrmStages.Lead,
    Persona = "roma",
    CreatedAtUtc = DateTimeOffset.UtcNow,
};

await pipeline.AdvanceAsync(lead, CrmStages.Researched, evidenceRef: "research-brief-123");
await pipeline.AdvanceAsync(lead, CrmStages.Contacted, evidenceRef: "touch-001");
// Illegal: throws CrmStateException with Code = CRM_ILLEGAL_TRANSITION.
// await pipeline.AdvanceAsync(lead, CrmStages.ClosedWon);
```

## Pure-graph design

The `CrmPipelineStateMachine` is one singleton that serves the entire lead
population. The wrapped `AgentStateMachine<CrmRecord>` is used in **pure-graph
mode** — `CanTransition(from, to, ctx)` checks transition legality without
mutating any internal SM state. The per-record stage lives on the
`CrmRecord.Stage` field.

This avoids the alternative ("one SM instance per lead"), which would mean
200 leads × 200 SM-instance metric-emitters + lock objects + history buffers.
Excessive for a pipeline-per-aggregate domain.

## Activity log conventions

`SqliteActivityLog` writes every transition to a single `activity_log` table
indexed by `(lead_id, occurred_utc)`. The replay engine (SA-04-01) reads this
to reconstruct the deal timeline.

Connection strings:
- `"Data Source=:memory:"` for unit tests
- `"Data Source=./.arena/activity.db"` for the demo Arena (local-relative)
- Any sqlite-compatible connection string works in production

## What's next

This story (SA-01-01) lands the foundation. Downstream stories that build on
it:

- **SA-01-02** — Next-Best-Action behavior tree (uses `GetAvailableNextStages`)
- **SA-01-08** — Supporting cast (Invoice subscribes to `ClosedWon`; Training reads the activity log)
- **SA-02-03** — Arena Ledger (subscribes to `CrmStageChanged` events)

## See also

- [Sales Arena Flagship Plan §4.1](../../../../examples/sales-arena/README.md)
- `DotNetAgents.Agents.StateMachines` — the public state-machine kit this wraps
