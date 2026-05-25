# Proposal Agent

Deterministic 3-tier proposal composer for the Sales Arena ([SALES-ARENA-FLAGSHIP-PLAN.md §5](../../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)).
Slice scope: story `ce3309c5` — child of parent SA-01-08 (`77f86038`).

## Surface

- `IProposalAgent` + `ProposalAgent` — composes `Proposal` containing exactly three tiers (starter / pro / enterprise) in stable order.
- `Proposal.ToMarkdown()` extension renders deterministic Markdown with stable section ordering and citation footer.
- Records: `ProposalContext`, `ProposalPackage`, `ProposalTier`, `ValueProp`, `PricingLine`, `Proposal`.

## Deferred to follow-up

- PDF rendering via `DotNetAgents.MultiModal`.
- Live persona-pack loader that reads `samples/sales-arena/personas/*/proposals/{starter,pro,enterprise}.md` and feeds the value props through `ProposalContext`.
- Per-tier add-on catalogs surfaced from the Knowledge Agent.
