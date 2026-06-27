#!/usr/bin/env python3
"""
Generates the SaaS-renewal v1 lead pack — 100 fictional existing customers with
renewal-relevant v2 fields (MRR, renewal_date, churn_risk_score, expansion_signal).

Run:
    python3 examples/sales-arena/lead-packs/tools/generate-saas-renewal-v1.py

Output: examples/sales-arena/lead-packs/saas-renewal-v1/leads.json
"""

from __future__ import annotations

import json
import random
from datetime import date, timedelta
from pathlib import Path

SEED = 20260518
GLENGARRY_COUNT = 15

COMPANIES = [
    ("Nimbus Ledger", "fintech", "mid-market", "nimbus-ledger.example"),
    ("Patchwork HR", "saas", "smb", "patchwork-hr.example"),
    ("Quill & Compass Logistics", "logistics", "enterprise", "quill-compass.example"),
    ("Brightfolio Education", "education", "mid-market", "brightfolio.example"),
    ("Copperline Health", "healthcare", "enterprise", "copperline-health.example"),
    ("Driftwood Media", "media", "smb", "driftwood-media.example"),
    ("Evergreen Agritech", "agritech", "mid-market", "evergreen-agritech.example"),
    ("Fable Street Commerce", "ecommerce", "smb", "fable-street.example"),
    ("Harborview Hospitality", "hospitality", "mid-market", "harborview-hosp.example"),
    ("Ironclad Manufacturing", "manufacturing", "enterprise", "ironclad-mfg.example"),
]

CUSTOMER_TIERS = ["starter", "growth", "enterprise"]
EXPANSION_SIGNALS = [
    "seat_count_up_18pct_qoq",
    "api_calls_up_42pct_30d",
    "requested_enterprise_sso",
    "opened_premium_analytics_trial",
    "champion_promoted_to_vp",
    "usage_spike_after_board_meeting",
    "added_second_business_unit",
]


def main() -> int:
    rng = random.Random(SEED)
    leads: list[dict] = []
    base_renewal = date(2026, 6, 1)

    for i in range(1, 101):
        lid = f"L-{i:0004}"
        is_glengarry = i <= GLENGARRY_COUNT
        tier = "glengarry" if is_glengarry else "cold"
        co_name, industry, size, domain = COMPANIES[(i - 1) % len(COMPANIES)]
        customer_tier = CUSTOMER_TIERS[i % 3]
        mrr = round(rng.uniform(800, 42000) if customer_tier != "enterprise" else rng.uniform(12000, 95000), 2)
        renewal = (base_renewal + timedelta(days=rng.randint(5, 120))).isoformat()
        churn = round(rng.uniform(0.05, 0.92) if not is_glengarry else rng.uniform(0.35, 0.88), 2)

        lead: dict = {
            "id": lid,
            "tier": tier,
            "company": {
                "name": co_name,
                "industry": industry,
                "size": size,
                "region": "us-west" if i % 2 else "emea",
                "domain": domain,
                "headcount": rng.randint(40, 8000),
            },
            "customer_tier": customer_tier,
            "mrr": mrr,
            "renewal_date": renewal,
            "churn_risk_score": churn,
            "expansion_signal": rng.sample(EXPANSION_SIGNALS, k=rng.randint(1, 3)),
        }

        if is_glengarry:
            lead["contact"] = {
                "firstName": "Alex",
                "lastName": f"Renewal-{i:02d}",
                "role": "Director of Customer Success",
                "email": f"renewal.{i:02d}@{domain}",
                "phone": f"555-{(1000 + i) % 10000:04d}",
            }
            lead["signals"] = [
                {
                    "kind": "content_engagement",
                    "summary": "Opened renewal ROI deck twice this week.",
                    "timestampUtc": "2026-05-10T00:00:00Z",
                }
            ]
            lead["notes"] = "High-touch renewal — contest narrators love the drama."

        leads.append(lead)

    pack = {
        "version": "v2",
        "name": "saas-renewal-v1",
        "description": "100 fictional existing SaaS customers for renewal-focused Arena contests. Every record carries v2 renewal fields (MRR, renewal_date, churn_risk_score, expansion_signal).",
        "synthetic": True,
        "createdAtUtc": "2026-05-18T00:00:00Z",
        "tags": ["saas", "renewal", "customer-success", "fictional"],
        "leads": leads,
    }

    root = Path(__file__).resolve().parents[1]
    out_dir = root / "saas-renewal-v1"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "leads.json"
    out_path.write_text(json.dumps(pack, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {out_path} ({len(leads)} leads, {GLENGARRY_COUNT} glengarry)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
