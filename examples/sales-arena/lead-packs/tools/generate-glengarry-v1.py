#!/usr/bin/env python3
"""
Generates the Glengarry v1 lead pack — 20 glengarry-tier premium leads
(hand-curated for character) plus 180 cold-tier leads (deterministically
generated from name/industry tables).

Why hand-curate the premium ones? Because the 20 Glengarry leads are the
ones the Arena's *drama* hangs on. They need names that pop in a leaderboard
narration: "Roma just closed Yatzee Pharmaceutical for $48K." Generic names
kill the theatre.

The 180 cold leads can be deterministic — they're volume fodder. Levene will
fire 47 of them off before lunch and most will bounce.

Run:
    python3 samples/sales-arena/lead-packs/tools/generate-glengarry-v1.py

Output: samples/sales-arena/lead-packs/glengarry-v1/leads.json
"""

from __future__ import annotations

import json
import random
import sys
import unicodedata
from pathlib import Path


def _ascii_slug(value: str) -> str:
    """ASCII-only slug for email-local-parts; strips diacritics + lowercases."""
    decomposed = unicodedata.normalize("NFKD", value)
    stripped = "".join(c for c in decomposed if not unicodedata.combining(c))
    return "".join(c for c in stripped.lower() if c.isascii() and (c.isalnum() or c in "._-"))


# ---------------------------------------------------------------------------
# The 20 hand-curated glengarry-tier premium leads.
#
# Distinctive company names + named contacts + 2-3 intel signals each.
# Every entry has narrative hooks the personas can ride.
# ---------------------------------------------------------------------------
GLENGARRY_LEADS: list[dict] = [
    {
        "id": "L-0001",
        "tier": "glengarry",
        "company": {
            "name": "Yatzee Pharmaceutical",
            "industry": "pharma",
            "size": "enterprise",
            "region": "us-northeast",
            "domain": "yatzee-pharma.example",
            "headcount": 4200,
        },
        "contact": {
            "firstName": "Olivia",
            "lastName": "Pendergast",
            "role": "VP Clinical Analytics",
            "email": "o.pendergast@yatzee-pharma.example",
            "phone": "555-0142",
        },
        "signals": [
            {"kind": "leadership_change", "summary": "New CFO appointed last quarter; analytics modernization listed in their first earnings call.", "timestampUtc": "2026-04-12T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Three downloads of clinical-trial-analytics whitepaper in the last 14 days.", "timestampUtc": "2026-05-09T00:00:00Z"},
            {"kind": "hiring", "summary": "Posted 4 data-platform engineer roles on their careers page.", "timestampUtc": "2026-05-02T00:00:00Z"},
        ],
        "notes": "The Cadillac lead. Every persona wants this one.",
    },
    {
        "id": "L-0002",
        "tier": "glengarry",
        "company": {
            "name": "Bluebottle Distribution",
            "industry": "logistics",
            "size": "mid-market",
            "region": "us-midwest",
            "domain": "bluebottle-dist.example",
            "headcount": 880,
        },
        "contact": {
            "firstName": "Marcus",
            "lastName": "Vance",
            "role": "Director of Operations Analytics",
            "email": "m.vance@bluebottle-dist.example",
            "phone": "555-0234",
        },
        "signals": [
            {"kind": "expansion", "summary": "Opened third regional distribution center in March.", "timestampUtc": "2026-03-21T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Watched the 'route-optimization ROI' webinar in full last Tuesday.", "timestampUtc": "2026-05-13T00:00:00Z"},
        ],
        "notes": "Williamson's perfect lead — they need scheduling clarity right now.",
    },
    {
        "id": "L-0003",
        "tier": "glengarry",
        "company": {
            "name": "Riverbend Robotics",
            "industry": "manufacturing",
            "size": "mid-market",
            "region": "us-pacific-northwest",
            "domain": "riverbend-robotics.example",
            "headcount": 540,
        },
        "contact": {
            "firstName": "Pria",
            "lastName": "Sundaram",
            "role": "Head of Production Intelligence",
            "email": "p.sundaram@riverbend-robotics.example",
            "phone": "555-0317",
        },
        "signals": [
            {"kind": "funding", "summary": "Closed $40M Series C, named 'predictive maintenance' as a top spending category.", "timestampUtc": "2026-04-28T00:00:00Z"},
            {"kind": "press_release", "summary": "Announced partnership with a national auto-OEM (no name disclosed).", "timestampUtc": "2026-05-05T00:00:00Z"},
            {"kind": "mutual_connection", "summary": "Connected to Yatzee Pharmaceutical's CFO via a Q1 industry panel.", "timestampUtc": "2026-02-18T00:00:00Z"},
        ],
        "notes": "Moss bait: they'll have technical objections about edge-deployment, and Moss has the counter.",
    },
    {
        "id": "L-0004",
        "tier": "glengarry",
        "company": {
            "name": "Northpoint Hospitality Group",
            "industry": "hospitality",
            "size": "enterprise",
            "region": "us-northeast",
            "domain": "northpoint-hospitality.example",
            "headcount": 3100,
        },
        "contact": {
            "firstName": "Henrik",
            "lastName": "Olafsson",
            "role": "SVP Guest Experience Data",
            "email": "h.olafsson@northpoint-hospitality.example",
            "phone": "555-0418",
        },
        "signals": [
            {"kind": "news", "summary": "Featured in a Hospitality Tech feature on 'next-gen loyalty programs.'", "timestampUtc": "2026-04-30T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Six page views on the 'customer-lifetime-value' case study in one week.", "timestampUtc": "2026-05-11T00:00:00Z"},
        ],
        "notes": "Aaronow lead — patient, relationship-first close.",
    },
    {
        "id": "L-0005",
        "tier": "glengarry",
        "company": {
            "name": "Saltmarsh Financial Services",
            "industry": "fintech",
            "size": "mid-market",
            "region": "us-southeast",
            "domain": "saltmarsh-fin.example",
            "headcount": 1200,
        },
        "contact": {
            "firstName": "Jacinta",
            "lastName": "Reyes-Bell",
            "role": "Director of Risk Analytics",
            "email": "j.reyes-bell@saltmarsh-fin.example",
            "phone": "555-0509",
        },
        "signals": [
            {"kind": "compliance_event", "summary": "Recently completed a SOC 2 audit; mentioned analytics-tooling-gaps in the public summary.", "timestampUtc": "2026-04-08T00:00:00Z"},
            {"kind": "hiring", "summary": "Two posted roles for senior risk-analytics modelers.", "timestampUtc": "2026-05-14T00:00:00Z"},
            {"kind": "mutual_connection", "summary": "Board member overlaps with Bluebottle Distribution.", "timestampUtc": "2026-01-15T00:00:00Z"},
        ],
        "notes": "Compliance angle is the hook. Roma will lead with the audit story.",
    },
    {
        "id": "L-0006",
        "tier": "glengarry",
        "company": {
            "name": "Cinderpath Education Networks",
            "industry": "education",
            "size": "mid-market",
            "region": "us-southwest",
            "domain": "cinderpath-edu.example",
            "headcount": 640,
        },
        "contact": {
            "firstName": "Tomas",
            "lastName": "Whitaker",
            "role": "VP Institutional Analytics",
            "email": "t.whitaker@cinderpath-edu.example",
            "phone": "555-0612",
        },
        "signals": [
            {"kind": "expansion", "summary": "Acquired a smaller online-education provider in February.", "timestampUtc": "2026-02-22T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Downloaded the 'student-outcome predictive modeling' ebook twice.", "timestampUtc": "2026-05-08T00:00:00Z"},
        ],
        "notes": "Post-acquisition data-consolidation pain. Levene angle: speed-to-quick-win.",
    },
    {
        "id": "L-0007",
        "tier": "glengarry",
        "company": {
            "name": "Quillstone Publishing",
            "industry": "media",
            "size": "smb",
            "region": "us-northeast",
            "domain": "quillstone-pub.example",
            "headcount": 180,
        },
        "contact": {
            "firstName": "Beatrice",
            "lastName": "Marwick",
            "role": "Chief Revenue Officer",
            "email": "b.marwick@quillstone-pub.example",
            "phone": "555-0714",
        },
        "signals": [
            {"kind": "leadership_change", "summary": "Beatrice was promoted from VP to CRO in March; first 90-day plan emphasized 'data-driven subscriber growth.'", "timestampUtc": "2026-03-04T00:00:00Z"},
            {"kind": "press_release", "summary": "Launched a new digital-subscription tier last week.", "timestampUtc": "2026-05-12T00:00:00Z"},
        ],
        "notes": "CRO with a public 90-day mandate. The fastest close on the board if you reach her in week 4.",
    },
    {
        "id": "L-0008",
        "tier": "glengarry",
        "company": {
            "name": "Mossberg Agritech",
            "industry": "agritech",
            "size": "mid-market",
            "region": "us-midwest",
            "domain": "mossberg-agri.example",
            "headcount": 920,
        },
        "contact": {
            "firstName": "Dane",
            "lastName": "Korhonen",
            "role": "Head of Field Operations Data",
            "email": "d.korhonen@mossberg-agri.example",
            "phone": "555-0823",
        },
        "signals": [
            {"kind": "product_launch", "summary": "Released a new IoT-soil-sensor platform in April.", "timestampUtc": "2026-04-17T00:00:00Z"},
            {"kind": "hiring", "summary": "Posted a 'sensor-data ML engineer' role last week.", "timestampUtc": "2026-05-10T00:00:00Z"},
        ],
        "notes": "Edge-compute story. Moss closes this one in his sleep if he can get on a call.",
    },
    {
        "id": "L-0009",
        "tier": "glengarry",
        "company": {
            "name": "Halberd & Lark Construction",
            "industry": "construction",
            "size": "mid-market",
            "region": "us-mountain",
            "domain": "halberd-lark.example",
            "headcount": 760,
        },
        "contact": {
            "firstName": "Imogen",
            "lastName": "Tachibana",
            "role": "Director of Project Analytics",
            "email": "i.tachibana@halberd-lark.example",
            "phone": "555-0915",
        },
        "signals": [
            {"kind": "news", "summary": "Won a major regional infrastructure contract; ribbon-cutting next month.", "timestampUtc": "2026-05-01T00:00:00Z"},
            {"kind": "expansion", "summary": "Hiring across 4 new project offices in adjacent states.", "timestampUtc": "2026-04-25T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Engaged on the 'construction-cost-overrun reduction' LinkedIn post.", "timestampUtc": "2026-05-06T00:00:00Z"},
        ],
        "notes": "Roma's signature lead — multiple signals, complex stakeholder map, long sales cycle, beautiful close.",
    },
    {
        "id": "L-0010",
        "tier": "glengarry",
        "company": {
            "name": "Brimworth Energy",
            "industry": "energy",
            "size": "enterprise",
            "region": "us-southcentral",
            "domain": "brimworth-energy.example",
            "headcount": 5400,
        },
        "contact": {
            "firstName": "Solange",
            "lastName": "Akintola",
            "role": "VP Grid Intelligence",
            "email": "s.akintola@brimworth-energy.example",
            "phone": "555-1018",
        },
        "signals": [
            {"kind": "compliance_event", "summary": "Pending regulatory grid-reliability filing in Q3.", "timestampUtc": "2026-04-19T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Two engagements on the 'predictive-outage analytics' content piece.", "timestampUtc": "2026-05-13T00:00:00Z"},
        ],
        "notes": "Enterprise, multi-quarter cycle. Aaronow's marathon. Pays huge.",
    },
    {
        "id": "L-0011",
        "tier": "glengarry",
        "company": {
            "name": "Carbide Healthcare Networks",
            "industry": "healthcare",
            "size": "enterprise",
            "region": "us-mid-atlantic",
            "domain": "carbide-health.example",
            "headcount": 8900,
        },
        "contact": {
            "firstName": "Renée",
            "lastName": "DuBoulay",
            "role": "Chief Data Officer",
            "email": "r.duboulay@carbide-health.example",
            "phone": "555-1124",
        },
        "signals": [
            {"kind": "leadership_change", "summary": "Renée joined 90 days ago from a peer health system.", "timestampUtc": "2026-02-17T00:00:00Z"},
            {"kind": "press_release", "summary": "Announced a value-based-care initiative spanning 12 hospitals.", "timestampUtc": "2026-04-30T00:00:00Z"},
            {"kind": "hiring", "summary": "5 senior data-engineering roles posted in the last 30 days.", "timestampUtc": "2026-05-09T00:00:00Z"},
        ],
        "notes": "New CDO + public mandate + hiring spree. Whoever closes this gets the Cadillac.",
    },
    {
        "id": "L-0012",
        "tier": "glengarry",
        "company": {
            "name": "Foxglove eCommerce Holdings",
            "industry": "ecommerce",
            "size": "mid-market",
            "region": "us-pacific-coast",
            "domain": "foxglove-ec.example",
            "headcount": 1100,
        },
        "contact": {
            "firstName": "Sven",
            "lastName": "Magnusson",
            "role": "Director of Customer Intelligence",
            "email": "s.magnusson@foxglove-ec.example",
            "phone": "555-1209",
        },
        "signals": [
            {"kind": "content_engagement", "summary": "Watched 3 of our 5-part 'merch-margin analytics' video series.", "timestampUtc": "2026-05-07T00:00:00Z"},
            {"kind": "mutual_connection", "summary": "Sven and Marcus from Bluebottle Distribution presented at the same regional logistics summit.", "timestampUtc": "2026-03-09T00:00:00Z"},
        ],
        "notes": "Williamson lead — Sven is a meeting-taker by reputation.",
    },
    {
        "id": "L-0013",
        "tier": "glengarry",
        "company": {
            "name": "Greyfield SaaS Holdings",
            "industry": "saas",
            "size": "smb",
            "region": "us-southeast",
            "domain": "greyfield-saas.example",
            "headcount": 240,
        },
        "contact": {
            "firstName": "Kalindi",
            "lastName": "Patel-Rourke",
            "role": "Co-founder & COO",
            "email": "k.patel-rourke@greyfield-saas.example",
            "phone": "555-1311",
        },
        "signals": [
            {"kind": "funding", "summary": "Closed $12M Series A six weeks ago.", "timestampUtc": "2026-04-04T00:00:00Z"},
            {"kind": "hiring", "summary": "Hiring a head of growth + 3 SDRs simultaneously.", "timestampUtc": "2026-05-11T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Re-shared our 'go-to-market analytics' post on LinkedIn.", "timestampUtc": "2026-05-14T00:00:00Z"},
        ],
        "notes": "Founder-led, fast cycle. Levene loves these — speed wins.",
    },
    {
        "id": "L-0014",
        "tier": "glengarry",
        "company": {
            "name": "Pinewell Nonprofit Alliance",
            "industry": "nonprofit",
            "size": "mid-market",
            "region": "us-northeast",
            "domain": "pinewell-np.example",
            "headcount": 410,
        },
        "contact": {
            "firstName": "Aaron",
            "lastName": "Yiannopoulos",
            "role": "Director of Donor Analytics",
            "email": "a.yiannopoulos@pinewell-np.example",
            "phone": "555-1407",
        },
        "signals": [
            {"kind": "news", "summary": "Featured in a national philanthropy publication for innovative donor-retention work.", "timestampUtc": "2026-04-22T00:00:00Z"},
            {"kind": "press_release", "summary": "Announced a $50M endowment matching campaign.", "timestampUtc": "2026-05-02T00:00:00Z"},
        ],
        "notes": "Roma's heart-warmer. Mission-aligned narrative closes it.",
    },
    {
        "id": "L-0015",
        "tier": "glengarry",
        "company": {
            "name": "Tideglass Marketing Group",
            "industry": "media",
            "size": "smb",
            "region": "us-pacific-coast",
            "domain": "tideglass-mkt.example",
            "headcount": 95,
        },
        "contact": {
            "firstName": "Lavinia",
            "lastName": "Beaumont",
            "role": "Head of Client Insights",
            "email": "l.beaumont@tideglass-mkt.example",
            "phone": "555-1502",
        },
        "signals": [
            {"kind": "expansion", "summary": "Recently won a Fortune 500 retail account.", "timestampUtc": "2026-04-10T00:00:00Z"},
            {"kind": "hiring", "summary": "Posted for a 'data scientist + creative strategist hybrid' role — unusual signal.", "timestampUtc": "2026-05-08T00:00:00Z"},
        ],
        "notes": "Williamson + Moss combo — book the meeting, then defuse the agency-data skepticism.",
    },
    {
        "id": "L-0016",
        "tier": "glengarry",
        "company": {
            "name": "Hollowhill Manufacturing",
            "industry": "manufacturing",
            "size": "enterprise",
            "region": "us-midwest",
            "domain": "hollowhill-mfg.example",
            "headcount": 6800,
        },
        "contact": {
            "firstName": "Bartholomew",
            "lastName": "Crewe",
            "role": "VP Operations Excellence",
            "email": "b.crewe@hollowhill-mfg.example",
            "phone": "555-1613",
        },
        "signals": [
            {"kind": "press_release", "summary": "Announced a $200M plant-modernization program in March.", "timestampUtc": "2026-03-08T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Engaged with the 'OEE benchmarking' content twice this month.", "timestampUtc": "2026-05-10T00:00:00Z"},
            {"kind": "hiring", "summary": "Hiring a director of 'industrial-AI strategy' — a brand-new role.", "timestampUtc": "2026-04-30T00:00:00Z"},
        ],
        "notes": "Massive cycle, massive payday. Aaronow's marathon plus Moss's technical depth.",
    },
    {
        "id": "L-0017",
        "tier": "glengarry",
        "company": {
            "name": "Sablefin Logistics Cooperative",
            "industry": "logistics",
            "size": "mid-market",
            "region": "us-pacific-northwest",
            "domain": "sablefin-logistics.example",
            "headcount": 720,
        },
        "contact": {
            "firstName": "Imani",
            "lastName": "Okonkwo",
            "role": "Director of Network Operations",
            "email": "i.okonkwo@sablefin-logistics.example",
            "phone": "555-1719",
        },
        "signals": [
            {"kind": "expansion", "summary": "Opening a second hub on the West Coast next quarter.", "timestampUtc": "2026-04-26T00:00:00Z"},
            {"kind": "compliance_event", "summary": "Recently flagged for a hours-of-service compliance review.", "timestampUtc": "2026-05-05T00:00:00Z"},
        ],
        "notes": "Compliance pain + expansion = double-hook. Moss leads with the audit angle.",
    },
    {
        "id": "L-0018",
        "tier": "glengarry",
        "company": {
            "name": "Briarstone Construction Partners",
            "industry": "construction",
            "size": "smb",
            "region": "us-southeast",
            "domain": "briarstone-cp.example",
            "headcount": 220,
        },
        "contact": {
            "firstName": "Cleo",
            "lastName": "Manfredi",
            "role": "Operations Director",
            "email": "c.manfredi@briarstone-cp.example",
            "phone": "555-1814",
        },
        "signals": [
            {"kind": "news", "summary": "Featured in the regional business press for fastest-growing GC of 2026 Q1.", "timestampUtc": "2026-04-15T00:00:00Z"},
            {"kind": "content_engagement", "summary": "Watched the 'jobsite-cost-tracking' demo end-to-end.", "timestampUtc": "2026-05-12T00:00:00Z"},
        ],
        "notes": "Levene speed-deal. Small but fast. Easy bell-ringer.",
    },
    {
        "id": "L-0019",
        "tier": "glengarry",
        "company": {
            "name": "Veldspar Bio Solutions",
            "industry": "pharma",
            "size": "mid-market",
            "region": "us-northeast",
            "domain": "veldspar-bio.example",
            "headcount": 480,
        },
        "contact": {
            "firstName": "Anders",
            "lastName": "Lundberg",
            "role": "Director of Computational Biology",
            "email": "a.lundberg@veldspar-bio.example",
            "phone": "555-1916",
        },
        "signals": [
            {"kind": "funding", "summary": "Closed $25M Series B with computational-platform as a stated investment area.", "timestampUtc": "2026-03-29T00:00:00Z"},
            {"kind": "press_release", "summary": "Announced a research partnership with a top-3 research university.", "timestampUtc": "2026-05-04T00:00:00Z"},
            {"kind": "mutual_connection", "summary": "Anders co-authored a paper with someone on Carbide Health's data team.", "timestampUtc": "2026-01-20T00:00:00Z"},
        ],
        "notes": "Roma's longest-cycle prize. PhD-flavored. Worth a year of nurture if he plays it right.",
    },
    {
        "id": "L-0020",
        "tier": "glengarry",
        "company": {
            "name": "Goldenrod Hospitality Ventures",
            "industry": "hospitality",
            "size": "mid-market",
            "region": "us-southwest",
            "domain": "goldenrod-hosp.example",
            "headcount": 1340,
        },
        "contact": {
            "firstName": "Tessa",
            "lastName": "Verstraete-Owens",
            "role": "VP Guest Loyalty Programs",
            "email": "t.verstraete-owens@goldenrod-hosp.example",
            "phone": "555-2003",
        },
        "signals": [
            {"kind": "product_launch", "summary": "Launched a refreshed loyalty app in April; early reviews mixed.", "timestampUtc": "2026-04-09T00:00:00Z"},
            {"kind": "leadership_change", "summary": "New CMO joined in February with a stated 'analytics-first' mandate.", "timestampUtc": "2026-02-12T00:00:00Z"},
        ],
        "notes": "Williamson's specialty: get Tessa on a 30-min discovery within 5 business days.",
    },
]


# ---------------------------------------------------------------------------
# Deterministic cold-lead generation.
# ---------------------------------------------------------------------------
COLD_COMPANY_PREFIXES = [
    "Acorn", "Bracken", "Cinder", "Drift", "Elm", "Fenwick", "Gable", "Harrow",
    "Ironwood", "Juniper", "Kingsley", "Larkspur", "Marsh", "Nettle", "Oxbow",
    "Pine", "Quarry", "Ridge", "Sable", "Thorn", "Underwood", "Vale",
    "Willow", "Yarrow", "Zephyr", "Beacon", "Cobble", "Dell", "Fern",
    "Glen", "Hazel", "Iris", "Jasper", "Knot", "Linden", "Meadow",
    "Nimbus", "Oat", "Plume", "Quill", "Reed", "Sage", "Thicket", "Vesper",
    "Wisp", "Yew", "Brindle", "Calder", "Drumlin", "Embry",
]

COLD_COMPANY_SUFFIXES = [
    "Industries", "Holdings", "Group", "Partners", "Solutions", "Systems",
    "Networks", "Cooperative", "Collective", "Ventures", "Capital",
    "Logistics", "Manufacturing", "Pharma", "Health Services", "Analytics",
    "Robotics", "Energy", "Construction", "Agritech", "SaaS", "Education Group",
    "Media", "Hospitality", "Financial", "Retail Brands", "Distribution",
    "Trading Co.", "Studios", "Labs",
]

COLD_INDUSTRIES = [
    "pharma", "fintech", "manufacturing", "healthcare", "ecommerce",
    "education", "hospitality", "agritech", "saas", "logistics", "media",
    "construction", "energy", "other",
]

COLD_SIZES = ["smb", "mid-market", "enterprise"]
COLD_SIZE_WEIGHTS = [0.55, 0.35, 0.10]

COLD_REGIONS = [
    "us-northeast", "us-southeast", "us-midwest", "us-southwest",
    "us-mountain", "us-pacific-coast", "us-pacific-northwest",
    "us-mid-atlantic", "us-southcentral", "emea-uk", "emea-de",
    "emea-fr", "apac-southeast", "apac-au-nz", "canada-east",
]

COLD_ROLES = [
    "VP Operations", "Director of Analytics", "Head of Data",
    "VP Marketing Analytics", "Director of IT", "Operations Manager",
    "VP Strategy", "Director of Business Intelligence", "COO",
    "VP Customer Success", "Head of Growth", "Director of Procurement",
    "VP Finance", "Director of Insights", "Chief of Staff", "VP Engineering",
    "Head of Platform", "Director of Field Operations", "VP Product",
    "Senior Director of Analytics",
    # ~30% leave role blank by emitting None below
]

COLD_FIRST_NAMES = [
    "Anika", "Benedict", "Cassia", "Dimitri", "Eleanor", "Faisal", "Gemma",
    "Hiroshi", "Iris", "Joaquin", "Kestrel", "Laila", "Mateo", "Niamh",
    "Oluwaseun", "Priya", "Quentin", "Rohan", "Samira", "Tobias",
    "Ursula", "Vikram", "Wren", "Xiaowen", "Yusuf", "Zelda", "Adan",
    "Briana", "Camilo", "Devika", "Esteban", "Fiona", "Gunnar", "Hala",
    "Idris", "Junia", "Kofi", "Linnea", "Magnus", "Nia", "Otto", "Petra",
    "Quincy", "Rashid", "Saoirse", "Tariq", "Una", "Viggo", "Wendell", "Yael",
]

COLD_LAST_NAMES = [
    "Alvarez", "Bjornsson", "Chen", "Devereux", "Esquivel", "Fukuda",
    "Goncalves", "Hannigan", "Ibrahim", "Joubert", "Kazemi", "Lindqvist",
    "Mwangi", "Nakamura", "Oduya", "Petrov", "Quiroga", "Rasheed",
    "Saintclair", "Tanaka", "Underhill", "Vasquez", "Whitford", "Xiang",
    "Yoshida", "Zerbino", "Abboud", "Brennan", "Cisneros", "Dahlgren",
    "Eberhardt", "Fitzwilliam", "Gallego", "Honoré", "Ito", "Jovanovic",
    "Klepper", "Lefebvre", "Marchetti", "Nightingale", "Oyelaran", "Palomar",
    "Quattlebaum", "Reinholt", "Saxena", "Truelove", "Ulysses", "Verlander",
    "Winterbourne", "Yamashita",
]


def deterministic_cold_lead(idx: int, rng: random.Random) -> dict:
    prefix = rng.choice(COLD_COMPANY_PREFIXES)
    suffix = rng.choice(COLD_COMPANY_SUFFIXES)
    name = f"{prefix} {suffix}"

    industry = rng.choice(COLD_INDUSTRIES)
    size = rng.choices(COLD_SIZES, weights=COLD_SIZE_WEIGHTS, k=1)[0]
    region = rng.choice(COLD_REGIONS)

    company = {
        "name": name,
        "industry": industry,
        "size": size,
        "region": region,
    }

    # Roughly 60% of cold leads have a contact name; only ~25% have a role.
    has_contact = rng.random() < 0.60
    has_role = rng.random() < 0.25
    has_email = has_contact and rng.random() < 0.55

    lead: dict = {
        "id": f"L-{idx:04d}",
        "tier": "cold",
        "company": company,
    }

    if has_contact:
        contact: dict = {
            "firstName": rng.choice(COLD_FIRST_NAMES),
            "lastName": rng.choice(COLD_LAST_NAMES),
        }
        if has_role:
            contact["role"] = rng.choice(COLD_ROLES)
        if has_email:
            slug = _ascii_slug(prefix) or "co"
            first_init = _ascii_slug(contact["firstName"])[:1] or "x"
            last_slug = _ascii_slug(contact["lastName"]) or "smith"
            contact["email"] = f"{first_init}.{last_slug}@{slug}-co.example"
        lead["contact"] = contact

    return lead


def main(out_path: Path) -> None:
    rng = random.Random(20260517)  # seeded for determinism

    leads = list(GLENGARRY_LEADS)
    for i in range(21, 201):
        leads.append(deterministic_cold_lead(i, rng))

    pack = {
        "version": "v1",
        "name": "glengarry-v1",
        "description": "DNA Sales Arena flagship lead pack. 200 fully-synthetic B2B prospects — 20 hand-curated glengarry-tier premium leads with full intel + 180 deterministically-generated cold leads with sparse intel. Use this pack to boot the first Arena contest.",
        "synthetic": True,
        "createdAtUtc": "2026-05-18T00:00:00Z",
        "tags": ["b2b", "data-analytics-saas", "flagship", "fictional"],
        "leads": leads,
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8") as fp:
        json.dump(pack, fp, indent=2, ensure_ascii=False)
        fp.write("\n")

    print(f"wrote {len(leads)} leads to {out_path}")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        target = Path(sys.argv[1])
    else:
        # default: alongside this script's parent at glengarry-v1/leads.json
        script_dir = Path(__file__).resolve().parent
        target = script_dir.parent / "glengarry-v1" / "leads.json"
    main(target)
