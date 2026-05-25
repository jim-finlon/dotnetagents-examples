# Sales-Arena Knowledge Pack

> The product, the objections, the case studies. What every persona reads
> before they touch a prospect.

The fictional knowledge base for the Arena. Everything in this tree is
**invented**: the product (Mitch & Murray Analytics Suite), the company,
every named customer, every named competitor, every quoted metric. No
real customers. No real pricing claims.

## Folder map

```
knowledge/
├── product/                 # 10 pages on the fictional product
├── objections/              # 15 numbered objections with per-persona responses
├── case-studies/            # 10 fictional client stories with measurable outcomes
├── faq/                     # 4 FAQ collections (general / technical / pricing / integration)
└── competitors/             # 3 invented-competitor positioning pages
```

42 files plus this README = the 40+ floor the SA-05-03 acceptance
criterion calls for.

## How personas use it

The Knowledge Agent (SA-01-08) indexes this folder via
`DotNetAgents.Knowledge`. Every other agent queries it through:

```csharp
var answer = await knowledgeAgent.AskAsync(
    "How do we handle 'we already have a vendor' for a healthcare prospect?",
    persona: "moss");
```

Personas adapt the same answer to their voice. The objection files
under `objections/` explicitly carry per-persona response sections so
the Training Agent can score which persona shape wins on a given
objection.

### Persona pull preferences

- **Roma** (consultative closer) — favors objections that open with a
  question; cites case studies where the buyer's stated problem
  matched.
- **Levene** (talker) — favors long product pages; quotes whole
  paragraphs.
- **Moss** (hardballer) — favors competitor-comparison pages; blunt
  objection responses.
- **Aaronow** (nervous closer) — leans on FAQ for safety; defers
  hardballs to written follow-up.
- **Williamson** (middle-management) — pulls case studies that match
  the buyer's company size.
- **Mitch & Murray** (the exec) — pulls roadmap + funding + team
  pages when an executive is on the call.

## Authoring your own pack

To plug the Arena into a real product you actually sell:

1. Replace these files with your product's docs (Markdown only).
2. Keep file paths under the five subdirectories above.
3. Run `dna-arena knowledge reindex` (once SA-01-08 ships).

See [`docs/public/SALES-ARENA-REAL-DATA.md`](../../../docs/public/SALES-ARENA-REAL-DATA.md)
for the safety + compliance frame before pointing the Arena at real
products and real prospects.

## Constraints

- **100% fictional.** No real company names. No real metrics. No real
  pricing claims.
- **No medical or financial guarantees.** Product is sales analytics
  SaaS; claims stay in the "this is how customers report using it"
  shape rather than "guarantees X% revenue lift."
- **No identifying detail.** Case-study buyers are invented characters
  with invented job titles at invented companies. Any resemblance to
  real people is unintended; if a contributor notices a real-world
  parallel, rename the character in the next commit.

## Reviewer gate

Before merging any change to this tree:

1. No real-world brand names or quoted real-world metrics.
2. No real customer or employee names that map to a real person.
3. Each file remains distinctive — generic boilerplate fails the
   "personas pull different talking points" AC.
4. Markdown encoding is plain UTF-8 with no BOM.

## See also

- [Sales Arena Flagship Plan](../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)
  §5.4 — knowledge-pack design.
- SA-01-08 — Knowledge Agent that indexes this corpus.
- SA-05-03 — this story.
