# SaaS-renewal v1 lead pack

**Schema:** `lead-pack.schema.v2.json` (`version: "v2"`)

One hundred **fictional existing customers** tuned for renewal contests: every lead
carries `customer_tier`, `mrr`, `renewal_date`, `churn_risk_score`, and
`expansion_signal[]` in addition to the v1 pool fields (`tier`, `company`, etc.).

| Stat | Value |
|------|-------|
| Total leads | 100 |
| Glengarry-tier (full intel) | 15 |
| Cold-tier | 85 |

## When to use this pack

| Pack | Best for |
|------|----------|
| **glengarry-v1** (v1) | New-business prospecting — 200 B2B leads, Glengarry premium drip |
| **saas-renewal-v1** (v2) | Customer-success / renewal sprints — churn risk + expansion signals |

## Validate

```bash
python3 samples/sales-arena/lead-packs/tools/test-lead-pack-v2.py \
  samples/sales-arena/lead-packs/saas-renewal-v1/leads.json

python3 samples/sales-arena/lead-packs/tools/test-lead-pack-v2.py \
  samples/sales-arena/lead-packs/glengarry-v1/leads.json
```

The v2 validator delegates v1 packs to `test-lead-pack.py` (backwards-compatible).

## Regenerate

```bash
python3 samples/sales-arena/lead-packs/tools/generate-saas-renewal-v1.py
```

Seed: `20260518` (deterministic).

## See also

- [`../README.md`](../README.md) — pack index
- [Sales Arena Flagship Plan](../../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md) — SA-06-03
