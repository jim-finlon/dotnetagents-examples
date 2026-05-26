# DNA Sales Arena — Flagship Public Agent Package (Public Teaser)

> *"Coffee's for closers."* — the Arena, every time a deal closes

A competitive multi-agent sales floor. 4–6 AI sales-rep personas work the same lead pool. Live leaderboard, voice narration, replay engine, "Glengarry premium leads" that drip to the top performer. Built entirely on `staging/public-dotnetagents/` packages — no premium-factory disclosure.

This directory is **runnable as a public-safe game-style teaser**. It allows running a deterministic offline smoke simulation that demonstrates competitive agent comparison, ledger events, ASCII leaderboard updates, and replay generation. The full production implementation lives in epics SA-01 through SA-07 (Mission Control). See [`docs/public/SALES-ARENA-FLAGSHIP-PLAN.md`](../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md) for the strategy and full story breakdown.

## Public Arena Teaser (Smoke Mode)

This repository includes a safe, zero-dependency teaser simulation to verify the CLI, ledger, leaderboard, and replay composition pipeline:

```bash
dotnet run --project public/dotnetagents-examples/examples/sales-arena/cli/SalesArena.Cli/SalesArena.Cli.csproj -- --smoke
```

This runs a deterministic local simulation between Roma, Levene, and Moss, verifies their events are indexed by the ledger, computes the leaderboard standings (Cadillac tier for Roma, SteakKnives tier for Levene, YouAreFired tier for Moss), and outputs a compliant `PublicExampleResultEnvelope` JSON to stdout.

## Open-Core Boundary Note

This teaser does not expose private scoring internals, live prompt strategies, premium datasets, genetic/evolution optimizer mechanics, or hosted simulator control plane operations. Those capabilities reside exclusively in the premium enterprise layers.


## What goes where

| Directory | What it holds | Built by epic |
|---|---|---|
| `agents/crm/` | CRM Agent: pipeline state machine, NBA tree, scoring | SA-01 |
| `agents/calendar/` | Calendar + Meeting Agent: ICS adapter, proposal engine, transcription | SA-01 |
| `agents/communications/` | Communications Agent: multi-channel inbox + persona-aware outbound | SA-01 |
| `agents/research/` | Research Agent: company intel + meeting briefs | SA-01 |
| `agents/proposal/` | Proposal Agent: persona-aware 3-tier proposals | SA-01 |
| `agents/invoice/` | Invoice Agent: fires on ClosedWon | SA-01 |
| `agents/knowledge/` | Knowledge Agent: product KB + objection encyclopedia | SA-01 |
| `agents/training/` | Training Agent: post-contest prompt-variant suggestions | SA-01 |
| `agents/ops/` | Sales-Pod Ops: contest setup + daily queue | SA-01 |
| `orchestrator/` | Arena Orchestrator + Ledger + Leaderboard + Glengarry-drip + theatre cues | SA-02 |
| `manager-web/` | Blazor Manager UI: Floor, Leaderboard, Lead Pool, Replay, Settings, Gallery | SA-03 + SA-07 |
| `replay/` | Replay engine: Markdown narrative + OTEL deal trace explorer | SA-04 |
| `cli/` | `dna-arena` CLI: init / contest / floor / bell / replay | SA-04 |
| `personas/` | Persona packs: 6 base personas + `_template/` + `community/` | SA-05 + SA-07 |
| `lead-packs/` | Synthetic lead packs: Glengarry v1 (200) + SaaS-renewal | SA-05 + SA-06 |
| `knowledge/` | Product / objection / case-study sample (40 markdown docs) | SA-05 |
| `assets/` | Theatre assets: audio cues, ASCII art, banner SVGs, launch artifacts | SA-06 |
| `scenarios/` | Arena Scenarios: pack format, sandbox, tournament engine, seasons | SA-07 |

## When to use the Arena

- **Demo flagship** — Live competitive sales floor (12-minute YouTube-ready storyline)
- **SMB starter kit** — Any small business can fork it + ship a real sales motion
- **Persona R&D** — Build your own AI salesperson and tournament-test it
- **Showcase for DNA orchestration** — 30+ public packages exercised across the package

## 5-minute walking tour

Full step-by-step + "what you'll see" milestones + troubleshooting:
[`docs/public/SALES-ARENA-QUICKSTART.md`](../../docs/public/SALES-ARENA-QUICKSTART.md).

```bash
# 1. Build the Arena.
dotnet build samples/sales-arena/

# 2. Seed a contest workspace from the synthetic 200-lead pack.
dna-arena init --leads samples/sales-arena/lead-packs/synthetic-200.json

# 3. Run a one-hour speed contest (about a minute of wall clock at
#    --time-compression 60).
dna-arena contest start \
    --name "tuesday-steak-knives" \
    --personas roma,levene,moss \
    --hours 1 \
    --time-compression 60

# 4. Watch the floor live at http://localhost:5005/floor — vintage
#    trading-floor leaderboard with a SignalR bell that rings on
#    every close.

# 5. After the bell, read the replay.
dna-arena replay summary --contest tuesday-steak-knives
```

Everything runs in **demo mode** by default — synthetic leads,
in-memory ledger, no real outbound. To swap in real customer data,
read [`docs/public/SALES-ARENA-REAL-DATA.md`](../../docs/public/SALES-ARENA-REAL-DATA.md)
first; that guide is loud about preconditions for a reason.

For authoring custom personas, see
[`docs/public/SALES-ARENA-FORK-YOUR-PERSONA.md`](../../docs/public/SALES-ARENA-FORK-YOUR-PERSONA.md)
(30-minute tutorial) or
[`docs/public/SALES-ARENA-WORKSHOP.md`](../../docs/public/SALES-ARENA-WORKSHOP.md)
(60-minute workshop). Open-core boundary + what Premium adds:
[`docs/public/SALES-ARENA-PREMIUM-ROUTES.md`](../../docs/public/SALES-ARENA-PREMIUM-ROUTES.md).

## Build status

The public teaser is fully implemented. The 45 production SDLC stories that fill out the remaining live/simulator features live in Mission Control:

- **Epic SA-01** Agent foundation (8 stories) — CRM, Calendar, Comms, supporting cast
- **Epic SA-02** Orchestrator + ledger + leaderboard + theatre (7 stories)
- **Epic SA-03** Manager UI (6 stories)
- **Epic SA-04** Replay engine + CLI (4 stories)
- **Epic SA-05** Persona / lead / knowledge / template packs (5 stories)
- **Epic SA-06** Demo polish + docs + launch (8 stories)
- **Epic SA-07** Arena Scenarios: bring-your-own-salesman (7 stories)

Stories are AI-ready (score 100) and autonomous-lane friendly. Pick them up via the `select_next_story` flow.

## The plan

Full strategy + theatre + persona designs + build sequencing live at:

[`docs/public/SALES-ARENA-FLAGSHIP-PLAN.md`](../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)

---

*"The leads are weak? The leads are weak? You're weak."* — Blake, 1992
*"Our leads are deterministic, scored, and replayable."* — DNA, 2026
