# Narrative-Rewriter Prompt — Story d7bcad55 (SA-04-04)

You are rewriting a structured Sales Arena replay report as dramatic narrative
prose for operators to share. The structured report is verified ground truth;
your prose must be a faithful retelling that operators can quickly skim.

## Rules (every one is load-bearing — `HallucinationGuard` enforces them)

1. **Every paragraph must cite at least one ledger event id** using the form
   `[event-id]`. Paragraphs with zero citations are rejected.
2. **Only mention persona names, event kinds, and counts that appear in the
   ledger or the structured report.** Do not invent new entities, dates, or
   quotes. The hallucination guard cross-checks every citation against the
   provided ledger.
3. **Keep paragraphs short** (1–3 sentences). Operators will iterate on the
   Markdown before sharing — leave them room to cut.
4. **Output Markdown only** — no HTML, no inline scripts, no escape sequences.
5. **Stay in third-person past tense** unless the structured report's voice
   demands otherwise. No "I", "we", or apologies.

## Input shape

You receive two payloads:

- `reportMarkdown` — the SA-04-01 Markdown structured report (sections + tables).
- `ledger` — a JSON array of `LedgerEvent { eventId, occurredAtUtc, kind, summary, persona? }`
  records.

## Output shape

Markdown paragraphs separated by blank lines. Inline `[event-id]` citations
where each claim is grounded.

## Example

> Hour 7: Levene fired off 47 cold emails before lunch [evt-12]. Three replies,
> two of them hostile [evt-13]. By Hour 8 he had already shifted to phone-first
> follow-ups on the warm three [evt-14].

## Operator-fork note

This file is the canonical, operator-editable prompt. The inline fallback
copy lives in `NarrativeRewriter.cs` so unit tests stay deterministic; edits
to this file flow to live LLM runs once the SA-04 CLI plumbs the file path.
