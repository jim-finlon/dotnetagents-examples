# Training Agent — Post-Contest Analysis

Story `3174d001` (SA-01-08f). Deterministic prompt-variant suggester for the
Sales Arena Training Agent.

This subdir is added to the existing `SalesArena.Training` assembly that already
houses the Persona Diary work shipped under SA-08-03. New code lives in the
`SalesArena.Training.PostContestAnalysis` sub-namespace to keep the two
features cleanly separated.

## Surface

- `IPostContestAnalyzer.AnalyzeAsync(ledger, contestId)` →
  `PromptVariantSuggestionSet` with per-persona suggestions sorted ordinally.
- Suggestion kinds (deterministic): `tighten-qualification`,
  `lead-with-calibration-value`, `soften-tone`, `no-change`.
- Thresholds (documented constants on `PostContestAnalyzer`):
  - `LowWinRateThreshold = 0.25` → tighten qualification.
  - `HighWinRateThreshold = 0.65` → soften tone + export pattern.
  - `MinDecisionsForWinRate = 4` → minimum closes before win-rate suggestions
    fire; otherwise emits `no-change` with an explanatory reason.
  - `PriceObjectionDominanceThreshold = 0.50` → if price ≥ 50 % of objections,
    suggest leading with calibration-value framing.
- `IContestLedgerReader` interface + `InMemoryContestLedger` test impl. Live
  `ArenaLedger` (SA-02-03) wiring is deferred.

## Deferred to follow-up

- Live `ArenaLedger` reader once SA-02-03 ships.
- LLM-generated suggestion text (current text is template-driven).
- Materialization of the suggestion into a candidate `system-prompt.md` file in
  the persona-pack repository.
- Multi-contest trend rollups + AB-test comparison.
