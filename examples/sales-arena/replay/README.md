# SalesArena.Replay

> *"The leads are weak, the leads are weak — but the replay isn't."*

Reads the SA-02-03 Arena Ledger and the SA-02-04 Leaderboard, produces a
narrative Markdown post-contest report. The single artifact the operator
shares after a contest.

## Sections (5 default)

| Section | What it shows |
|---|---|
| **🏆 Leaderboard** | Final tier-ranked board (Cadillac / Steak Knives / You're Fired) |
| **📜 Persona Deal Logs** | Every deal each closer worked, sorted by final standing |
| **🔪 Closest Call** | The biggest deal that almost slipped (largest lost-proposal value) |
| **🚀 Best Comeback** | Persona that climbed the most positions over the contest |
| **🔔 MVP Touch** | Single touch (channel + template + variant) that flipped the most value |

Each section also produces 0-1 `ReplayHighlight`s — the narrative-mode
rewriter (SA-04-04) uses these as anchor events when dramatizing.

## Usage

```csharp
await using var ledger = new SqliteArenaLedger("Data Source=./.arena/ledger.db");
var leaderboard = new LeaderboardEngine(ledger);
var generator = new ReplayGenerator(ledger, leaderboard);

var report = await generator.GenerateAsync(new ReplayOptions(
    ContestId: "Tuesday-Steak-Knives",
    FinalScoring: new RevenueScoring(),
    ContestDisplayName: "Tuesday's Steak-Knives Bake-Off"));

// `report.Markdown` is ready to publish; `report.Sections` is one per kind; 
// `report.Highlights` feeds the narrative rewriter.
File.WriteAllText("replay.md", report.Markdown);

// Or in one call:
await generator.ExportToFileAsync(options, "out/tuesday-replay.md");
```

## Forking the section templates

The headers for each section live in [`templates/`](./templates) as plain
Markdown files. Tokens supported: `{{contest_id}}`, `{{contest_name}}`,
`{{generated_at_utc}}`.

To customize:

1. Copy the `templates/` directory next to your config.
2. Edit any header.
3. Pass `TemplateDir = "/path/to/templates"` in `ReplayOptions`.

Falling back to the built-in defaults when the directory or specific file is
missing is the default behavior — the engine is safe to run in test
environments without copy-to-output template dirs.

## See also

- [Sales Arena Flagship Plan §6.3](../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)
- SA-04-02 — Trace-explorer drill-down (deal-by-deal OTEL spans)
- SA-04-03 — `dna-arena CLI` that calls this engine
- SA-04-04 — Narrative-mode LLM rewriter (turns the structured report into dramatic prose with cited events)
