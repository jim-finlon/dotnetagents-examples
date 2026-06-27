# CRM Next-Best-Action (NBA) Engine

Story `ecf26755` (SA-01-02). Behavior-tree-shaped NBA engine with four persona variants
under the existing `SalesArena.Crm.Agent` project ([SALES-ARENA-FLAGSHIP-PLAN.md §4.1](../../../../../examples/sales-arena/README.md)).

## Surface

- `INextBestActionEngine.Decide(personaId, CrmContext)` → `NbaDecision { Action, Reason, PersonaId, Trace }`.
- Minimal behavior-tree nodes in `BehaviorTree.cs`: `Selector`, `Condition` (predicate → action), `FallbackAction` (default leaf). No external BT dependency — small enough to keep self-contained.
- Four `IPersonaStrategy` implementations (`PersonaStrategies.Defaults`):
  - **roma** (consultative) — disqualify low fit; resolve objections first; book warm meetings; close only when qualified + proposal out.
  - **levene** (talker) — re-engage on any silence; pitch on any intent signal; throw proposals at late stages.
  - **moss** (hardballer) — aggressively disqualify weak fit/no power; force meetings when power is present; demo before discovery when there's any fit.
  - **williamson** (rule-follower) — strict stage-gated mapping (Discovery → Qualification → Demo → Proposal → Negotiation); never skips a stage; waits when a meeting is on the books.
- The result `Trace` is a list of strings recording each node visited — useful for the Coach/Bell-stream narration plane.

## Companion files in `agents/crm/`

The existing `SalesArena.Crm.Agent` project already ships `CrmPipelineStateMachine`,
`CrmRecord`, `CrmStageChangedEvent`, `ICrmEventPublisher`, `Scoring/` — this slice
adds the NBA decision layer on top of that scoring + state.

## Deferred to follow-up

- Behavior-tree replacement via `DotNetAgents.Agents.BehaviorTrees` once the Sales
  Arena Orchestrator (SA-02-01) commits to it as the shared BT runtime.
- LLM-explained rationale paragraph alongside the deterministic `Reason` (current
  `Reason` is a hard-coded string from the rule that fired).
- Additional persona variants (aaronow nervous, mitch-and-murray narrator) once
  their strategy contracts are pinned in design.
