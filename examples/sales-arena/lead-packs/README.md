# Sales-Arena Lead Packs

Synthetic B2B prospect data for the Arena. Each pack is a JSON file conforming
to the `lead-pack.schema.json` (v1 ships in SA-05-02; v2 with renewal fields
ships in SA-06-03).

## What's a lead pack?

A JSON file with an array of prospect records. The schema covers:

- **Firmographics** — company, industry, region, size, mrr (v2)
- **Contact** — name, role, email, phone
- **Intent signal** — web visits, content downloads (when known)
- **Tier** — `glengarry` (premium, full intel) or `cold` (minimal intel)
- **Optional v2 fields** — `customer_tier`, `renewal_date`, `churn_risk_score`, `expansion_signal[]`

## What ships here

```
lead-packs/
├── lead-pack.schema.json              # ✅ v1 schema (SA-05-02)
├── glengarry-v1/                      # ✅ Flagship pack (SA-05-02)
│   ├── leads.json                     # 200 prospects (20 glengarry + 180 cold)
│   └── README.md
├── lead-pack.schema.v2.json           # ✅ v2 renewal-aware schema (SA-06-03)
├── saas-renewal-v1/                   # ✅ SaaS-renewal pack (SA-06-03)
│   ├── leads.json                     # 100 existing customers (v2 fields)
│   └── README.md
└── tools/                             # ✅ Generators + validators
    ├── generate-glengarry-v1.py
    ├── generate-saas-renewal-v1.py
    ├── test-lead-pack.py
    ├── test-lead-pack-v2.py
    └── test-validator-rejects-malformed.py
```

## Quick validate

```bash
# v1 flagship pack
python3 examples/sales-arena/lead-packs/tools/test-lead-pack.py \
    examples/sales-arena/lead-packs/glengarry-v1/leads.json

# v2 renewal pack (also accepts v1 paths)
python3 examples/sales-arena/lead-packs/tools/test-lead-pack-v2.py \
    examples/sales-arena/lead-packs/saas-renewal-v1/leads.json
```

Expected: `PASS` with 200 leads (20 glengarry, 180 cold) for Glengarry; `PASS` with
100 leads for SaaS-renewal.

## Regenerate deterministic packs

```bash
python3 examples/sales-arena/lead-packs/tools/generate-glengarry-v1.py
python3 examples/sales-arena/lead-packs/tools/generate-saas-renewal-v1.py
```

Seeds live in each script (`20260517` Glengarry, `20260518` SaaS-renewal); deterministic
output keeps diffs reviewable.

## Safety

**100% synthetic.** All names, companies, domains, and contact details are
fictional. The Arena refuses to load packs containing live-looking PII unless
the operator explicitly attests they've followed `examples/sales-arena/README.md`.

## Authoring your own pack

After SA-05-02 ships:

```bash
# Validate
dna-arena leads validate /path/to/your-pack.json

# Use in a contest
dna-arena init --leads /path/to/your-pack.json
```

The schema is forkable. Companies of any industry, role-stack, or signal-shape
can be expressed.

## See also

- [Sales Arena Flagship Plan](../../../examples/sales-arena/README.md) §3.3
- SA-05-02 public provenance marker
- SA-06-03 (v2 schema + SaaS-renewal pack)
