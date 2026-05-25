# Counterfactual Replay (SA-08-06)

> *"What if Levene had used Roma's templates?"*

Re-runs a contest with one variable changed and produces a side-by-side
diff. The closed set of mutations is pinned at v1:

- `SwapOutreachTemplatesMutation(fromPersona, toPersona)`
- `SwapModelTierMutation(persona, newTier)`
- `SwapCadenceMutation(persona, newCadenceRef)`

Adding new mutation types is intentionally a follow-up story so the
surface stays auditable.

## How it composes

`CounterfactualRunner.Run(...)`
1. Applies the mutation to a snapshot of the original persona configs
   (pure record-with-mutation; the originals are unchanged).
2. Calls the registered `IContestSimulator` twice — original + mutated —
   with the **same seed + same lead-pool size**. The simulator must be
   deterministic: same input → same output (this is the load-bearing
   invariant that makes the diff meaningful).
3. Builds a `ContestOutcomeDiff` per persona (touches / meetings / wins
   / losses / revenue / final position) and returns the bundle as
   `CounterfactualResult`.

`CounterfactualDiffRenderer.RenderMarkdown(result)` formats the bundle
into a side-by-side Markdown table that can be inlined into a replay
report.

## What it deliberately does NOT do

- Real-channel sends — every "touch" is simulated. SecurityNote enforced.
- Real-money side effects — revenue figures are simulator outputs only.
- Mutation chaining — exactly one mutation per run. Compose at the
  caller level.

## Production simulator

The reference `DeterministicHashSimulator` exists to validate the
diff + renderer pipeline end-to-end while SA-02-05 contest lifecycle
matures. Production hosts plug a real `IContestSimulator`; the
public-facing API and tests already pin the determinism contract
(no-op zero-delta + same-seed-same-output).
