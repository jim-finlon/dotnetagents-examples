#!/usr/bin/env python3
"""
Lead-pack invariant checker — runs the safety + schema-sanity gates without
requiring the `jsonschema` library.

This is the regression test SA-05-02's AC asks for. It's deliberately a
zero-dependency Python script so it can run in any CI environment without
pip-installing anything. After SA-04-03 ships, the same checks live behind
`dna-arena leads validate`.

Exits 0 on pass, 2 on failure. Prints a structured report.

Usage:
    python3 test-lead-pack.py samples/sales-arena/lead-packs/glengarry-v1/leads.json
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ID_PATTERN = re.compile(r"^L-\d{4,}$")
DOMAIN_PATTERN = re.compile(r"^[a-z0-9.-]+\.(example|test|invalid)$")
PHONE_PATTERN = re.compile(r"^555-\d{4}$")
EMAIL_DOMAIN_PATTERN = re.compile(r"^[A-Za-z0-9._%+-]+@[a-z0-9.-]+\.(example|test|invalid)$")

ALLOWED_TIERS = {"glengarry", "cold"}
ALLOWED_INDUSTRIES = {
    "pharma", "fintech", "manufacturing", "healthcare", "ecommerce",
    "education", "hospitality", "agritech", "saas", "logistics",
    "media", "construction", "energy", "nonprofit", "other",
}
ALLOWED_SIZES = {"smb", "mid-market", "enterprise"}
ALLOWED_SIGNAL_KINDS = {
    "news", "hiring", "content_engagement", "mutual_connection",
    "press_release", "leadership_change", "funding",
    "product_launch", "expansion", "compliance_event",
}
ALLOWED_VERSIONS = {"v1", "v2"}
ALLOWED_CUSTOMER_TIERS = {"starter", "professional", "enterprise", "strategic"}
DATE_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}$")


def check(condition: bool, msg: str, failures: list[str]) -> None:
    if not condition:
        failures.append(msg)


def validate_pack(path: Path) -> tuple[bool, list[str], dict]:
    failures: list[str] = []
    try:
        pack = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        return False, [f"json parse error: {exc}"], {}

    version = pack.get("version")
    check(version in ALLOWED_VERSIONS, f"version must be v1 or v2, got: {version!r}", failures)
    check(pack.get("synthetic") is True, "synthetic != true (only synthetic packs allowed here)", failures)
    check(isinstance(pack.get("name"), str) and len(pack["name"]) > 0, "name missing or empty", failures)
    check(isinstance(pack.get("description"), str), "description missing", failures)

    leads = pack.get("leads")
    check(isinstance(leads, list) and len(leads) >= 1, "leads must be a non-empty array", failures)
    if not isinstance(leads, list):
        return len(failures) == 0, failures, {}

    seen_ids: set[str] = set()
    glengarry_count = 0
    cold_count = 0

    for i, lead in enumerate(leads):
        ctx = f"leads[{i}]"
        lid = lead.get("id")
        check(isinstance(lid, str) and ID_PATTERN.match(lid) is not None, f"{ctx} id pattern failed: {lid}", failures)
        if lid is not None:
            check(lid not in seen_ids, f"{ctx} duplicate id: {lid}", failures)
            seen_ids.add(lid)

        tier = lead.get("tier")
        check(tier in ALLOWED_TIERS, f"{ctx} tier invalid: {tier}", failures)
        if tier == "glengarry":
            glengarry_count += 1
        elif tier == "cold":
            cold_count += 1

        company = lead.get("company") or {}
        check(isinstance(company.get("name"), str) and len(company["name"]) > 0, f"{ctx} company.name missing", failures)

        ind = company.get("industry")
        if ind is not None:
            check(ind in ALLOWED_INDUSTRIES, f"{ctx} industry invalid: {ind}", failures)

        size = company.get("size")
        if size is not None:
            check(size in ALLOWED_SIZES, f"{ctx} size invalid: {size}", failures)

        dom = company.get("domain")
        if dom is not None:
            check(DOMAIN_PATTERN.match(dom) is not None, f"{ctx} domain must use .example/.test/.invalid: {dom}", failures)

        mrr = company.get("mrr")
        if mrr is not None:
            check(isinstance(mrr, (int, float)) and mrr >= 0, f"{ctx} mrr must be a non-negative number: {mrr}", failures)

        ctier = lead.get("customer_tier")
        if ctier is not None:
            check(ctier in ALLOWED_CUSTOMER_TIERS, f"{ctx} customer_tier invalid: {ctier}", failures)

        renewal = lead.get("renewal_date")
        if renewal is not None:
            check(isinstance(renewal, str) and DATE_PATTERN.match(renewal) is not None, f"{ctx} renewal_date must be YYYY-MM-DD: {renewal}", failures)

        churn = lead.get("churn_risk_score")
        if churn is not None:
            check(isinstance(churn, int) and 0 <= churn <= 100, f"{ctx} churn_risk_score must be 0-100: {churn}", failures)

        expansion = lead.get("expansion_signal")
        if expansion is not None:
            check(isinstance(expansion, list), f"{ctx} expansion_signal must be an array", failures)
            for j, item in enumerate(expansion):
                check(isinstance(item, str) and len(item) > 0, f"{ctx}.expansion_signal[{j}] must be a non-empty string", failures)

        # Glengarry-specific gates
        if tier == "glengarry":
            check("contact" in lead, f"{ctx} glengarry lead must have contact", failures)
            sigs = lead.get("signals")
            check(isinstance(sigs, list) and len(sigs) >= 1, f"{ctx} glengarry lead must have >=1 signal", failures)

        contact = lead.get("contact") or {}
        if "email" in contact:
            check(EMAIL_DOMAIN_PATTERN.match(contact["email"]) is not None, f"{ctx} email must use .example/.test/.invalid: {contact['email']}", failures)
        if "phone" in contact:
            check(PHONE_PATTERN.match(contact["phone"]) is not None, f"{ctx} phone must use 555-NNNN: {contact['phone']}", failures)

        sigs = lead.get("signals") or []
        for j, sig in enumerate(sigs):
            sctx = f"{ctx}.signals[{j}]"
            sk = sig.get("kind")
            check(sk in ALLOWED_SIGNAL_KINDS, f"{sctx} kind invalid: {sk}", failures)
            check(isinstance(sig.get("summary"), str) and len(sig["summary"]) > 0, f"{sctx} summary missing", failures)

    stats = {
        "name": pack.get("name"),
        "version": version,
        "total": len(leads),
        "glengarry": glengarry_count,
        "cold": cold_count,
    }
    return len(failures) == 0, failures, stats


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: test-lead-pack.py <path-to-leads.json>", file=sys.stderr)
        return 2

    path = Path(argv[1])
    if not path.exists():
        print(f"file not found: {path}", file=sys.stderr)
        return 2

    ok, failures, stats = validate_pack(path)
    if ok:
        print(f"PASS {path}")
        print(f"  name      = {stats.get('name')}")
        print(f"  version   = {stats.get('version')}")
        print(f"  total     = {stats.get('total')}")
        print(f"  glengarry = {stats.get('glengarry')}")
        print(f"  cold      = {stats.get('cold')}")
        return 0

    print(f"FAIL {path}", file=sys.stderr)
    for f in failures:
        print(f"  - {f}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
