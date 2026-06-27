# Sales Arena Scenarios — The Multiplayer Layer

> *"Wait. There's a guy with a custom persona in the Gallery who just beat Roma in a head-to-head?"*

After the flagship Arena ships (SA-01..06), this directory becomes the home of
**Arena Scenarios** — the multiplayer extension where anyone can author a
custom persona pack, upload it, and watch it compete.

## What lives here (when SA-07 lands)

```
scenarios/
├── PersonaPackFormat/      # SA-07-01: .salesman.zip read/write + signed manifest
├── Sandbox/                # SA-07-02: sandboxed persona host with hard limits
├── Tournament/             # SA-07-04: head-to-head + N-persona brackets
└── Seasons/                # SA-07-07: ELO ranking + Glengarry/Wolf/Office-Space themes
```

## Sandbox host (SA-07-02)

`Sandbox/` contains a deterministic v1 persona host that evaluates a proposed
persona action list before any live LLM or tool adapter is invoked. It enforces:

- outbound touch-volume caps per run
- tool-call budget caps per run
- runtime budget caps per run

The host returns a receipt with executed-step counts, consumed budgets, and the
first limit violation. This keeps community persona packs testable without
granting them unbounded CRM, email, or runtime access.

## The pitch

The Arena is a *game engine for AI sales personas*. You write the prompt,
the cadence, the templates. The Arena runs the contest. You see whether
your persona is better than the seven other community personas, and whether
it can take down the canonical Roma.

## Three reasons it works

1. **Kaggle for sales prompts** — measurable head-to-head where prompt design is the variable
2. **Glengarry leaderboard** — ELO + seasons keep the meta fresh; new themes change what wins
3. **No deploy friction** — `.salesman.zip` → drag into Gallery → contest in 30 seconds

## How to participate (when SA-07-05 workshop lands)

1. Follow `examples/sales-arena/README.md`
2. Author a persona using `examples/sales-arena/personas/_template/`
3. `dna-arena persona export` → `.salesman.zip`
4. Drop into someone else's Arena Gallery, or open a PR with a community pack
5. Run a head-to-head
6. Climb the ELO ladder
7. Brag

## Seasons (SA-07-07)

Each season introduces:

- A **theme** (Glengarry / Wolf-of-Wall-Street / Office-Space / your-own)
- **Custom scoring weights** that favor different sales motions
- **Persona buffs/nerfs** that change the meta (e.g., "Glengarry season penalizes consultative pace")
- A **fresh ELO leaderboard** (carry-forward as "All-Time")

Operators can author new seasons. The community can vote on which goes live.

## See also

- [Sales Arena Flagship Plan](../../../examples/sales-arena/README.md) §SA-07
- SA-07-01 through SA-07-07 in the private enterprise roadmap
