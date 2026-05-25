# Outreach templates and A/B promotion (SA-05-05)

Canonical corpus: **six personas × four channels × three variants = 72** markdown files under:

`samples/sales-arena/personas/{persona}/outreach/{email|sms|linkedin|chat}/{variant-1|variant-2|variant-3}.md`

## Frontmatter contract

Each file includes:

- `hypothesis` — what this variant is testing
- `variant_id` — channel-scoped id (e.g. `email-2`)
- `word_count_target` — soft length guard for demos
- `substitutions` — comma-separated token names (`prospect.first_name`, etc.)
- `narrator_only: true` — Mitch & Murray placeholders (orchestrator skips sends)

## Loader

`IOutreachTemplateLoader` in `SalesArena.OutreachTemplates` loads the full corpus for SA-01-07 `AbPromotionTracker` integration.

## A/B promotion rule (demo)

A variant **wins** for a `(persona, channel)` pair when:

1. At least **20 sends** are recorded for that variant, and
2. **Reply rate** exceeds the runner-up by the configured significance gate.

The winning variant becomes the default template until the next contest reset. All amounts and customer names in templates are fictional and demo-safe.

## Regenerating templates

```bash
python3 samples/sales-arena/personas/tools/Generate-Sa0505OutreachTemplates.py
```

The generator is idempotent: it skips existing long-form `variant-1` email files that already include `word_count_target`.
