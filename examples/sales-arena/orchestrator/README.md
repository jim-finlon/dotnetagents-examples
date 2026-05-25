# SalesArena.Orchestrator

The runtime that runs the show. This project hosts the Arena Orchestrator
(SA-02-01), the Ledger (SA-02-03 — *this story*), the Leaderboard (SA-02-04),
the Contest Lifecycle (SA-02-05), the Narrator + Bell (SA-02-06), and the
Glengarry-drip policy (SA-02-07).

## What's in here today (SA-02-03)

### The Ledger

Append-only event log. Every Arena event lands here. The single source of
truth for the leaderboard and the replay engine.

**14 event kinds** (`ArenaEventKinds`):

| Kind | Fires when |
|---|---|
| `LeadAssigned` | Orchestrator assigns a lead to a persona pod |
| `LeadResearched` | Persona completes research on a lead |
| `TouchSent` | Outbound touch (email/SMS/LinkedIn/chat) |
| `InboundReceived` | Inbound classified + correlated to CRM |
| `MeetingBooked` | Calendar event created on persona's calendar |
| `MeetingHeld` | Meeting transcript captured + summary attached |
| `ProposalSent` | Proposal sent to prospect |
| `Objection` | Prospect raised an objection + persona's response |
| `DealClosed` | Won or lost |
| `GlengarryLeadDripped` | Premium leads dripped to top-tier persona |
| `LeadsRevoked` | Leads returned from bottom-tier persona to pool |
| `BellRung` | Theatrical bell event (narrator-fired) |
| `LeaderboardSnapshot` | Periodic snapshot for replay reconstruction |
| `ContestPhaseChanged` | Init/Started/Paused/Resumed/Ended |

Each kind has a strongly-typed payload record in `EventPayloads.cs`.

### Storage shape

```sql
arena_event:
    id            INTEGER PRIMARY KEY AUTOINCREMENT
    contest_id    TEXT    NOT NULL    -- every event is contest-scoped
    kind          TEXT    NOT NULL    -- one of ArenaEventKinds
    occurred_utc  TEXT    NOT NULL    -- ISO-8601 round-trip
    lead_id       TEXT                -- indexed for per-deal trace queries
    persona       TEXT                -- indexed for per-persona leaderboards
    payload_json  TEXT    NOT NULL    -- typed payload, serialized

Indexes: contest_id+time, lead_id+time, persona+time, kind+time
```

### Usage

```csharp
await using var ledger = new SqliteArenaLedger("Data Source=./.arena/ledger.db");

// Append a typed event
var dealClosed = new DealClosedPayload(
    LeadId: "L-0001",
    Persona: "roma",
    Outcome: "Won",
    ValueUsd: 48_000m,
    LossReason: null);

var saved = await ledger.AppendAsync(new ArenaEvent
{
    ContestId = "Tuesday-Steak-Knives",
    Kind = ArenaEventKinds.DealClosed,
    OccurredAtUtc = DateTimeOffset.UtcNow,
    LeadId = "L-0001",
    Persona = "roma",
    PayloadJson = ArenaEvent.SerializePayload(dealClosed),
});

// Query: every deal Roma closed in the contest
var query = ArenaEventFilter.ForPersona("Tuesday-Steak-Knives", "roma") with { Kind = ArenaEventKinds.DealClosed };
await foreach (var evt in ledger.QueryAsync(query))
{
    var payload = evt.GetPayload<DealClosedPayload>()!;
    Console.WriteLine($"{evt.OccurredAtUtc}: {payload.LeadId} ({payload.Outcome}, ${payload.ValueUsd})");
}
```

### Filter shapes

`ArenaEventFilter` lets callers compose:

```csharp
ArenaEventFilter.ForContest(contestId);
ArenaEventFilter.ForLead(contestId, leadId);          // deal-trace drill-down (SA-04-02)
ArenaEventFilter.ForPersona(contestId, persona);      // leaderboard input
ArenaEventFilter.OfKind(contestId, ArenaEventKinds.DealClosed);

// And any combination via record-with-syntax:
ArenaEventFilter.ForContest(contestId) with { Kind = ArenaEventKinds.TouchSent, FromUtc = todayStart };
```

## What's next

This story (SA-02-03) lands the Ledger. Downstream stories that build on it:

- **SA-02-04** — Leaderboard engine reads `DealClosed`, `LeaderboardSnapshot`
- **SA-02-05** — Contest lifecycle writes `ContestPhaseChanged`
- **SA-02-06** — Narrator writes `BellRung`
- **SA-02-07** — Glengarry-drip writes `GlengarryLeadDripped` + `LeadsRevoked`
- **SA-04-01** — Replay engine reads everything by `ContestId`
- **SA-04-02** — Deal-trace explorer reads by `LeadId`

## See also

- [Sales Arena Flagship Plan §6.2](../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)
- `SalesArena.Crm.Agent` (SA-01-01) — emits CRM stage-changed events that the orchestrator forwards into the ledger as `LeadAssigned` / `MeetingBooked` / etc.
