#!/usr/bin/env python3
"""
v2 lead-pack validator — same zero-dependency posture as test-lead-pack.py.
Accepts v1 packs when passed explicitly (delegates to v1 checks) and v2 packs
with renewal fields.

Usage:
    python3 test-lead-pack-v2.py <path-to-leads.json>
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

ID_PATTERN = re.compile(r"^L-[0-9]{4,}$")
CUSTOMER_TIERS = {"starter", "growth", "enterprise"}


def validate_v2_pack(path: Path) -> tuple[bool, list[str], dict]:
    failures: list[str] = []
    try:
        pack = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        return False, [f"json parse error: {exc}"], {}

    version = pack.get("version")
    if version == "v1":
        v1_script = Path(__file__).with_name("test-lead-pack.py")
        proc = subprocess.run(
            [sys.executable, str(v1_script), str(path)],
            capture_output=True,
            text=True,
        )
        if proc.returncode == 0:
            return True, [], {"name": pack.get("name"), "total": len(pack.get("leads", [])), "version": "v1"}
        return False, [proc.stderr.strip() or "v1 validator failed"], {}

    if version != "v2":
        failures.append(f"version must be 'v1' or 'v2', got {version!r}")
        return False, failures, {}

    if pack.get("synthetic") is not True:
        failures.append("synthetic != true")
    leads = pack.get("leads")
    if not isinstance(leads, list) or len(leads) < 1:
        failures.append("leads must be a non-empty array")
        return False, failures, {}

    seen: set[str] = set()
    for i, lead in enumerate(leads):
        ctx = f"leads[{i}]"
        lid = lead.get("id")
        if not isinstance(lid, str) or ID_PATTERN.match(lid) is None:
            failures.append(f"{ctx} id invalid: {lid}")
        elif lid in seen:
            failures.append(f"{ctx} duplicate id: {lid}")
        else:
            seen.add(lid)

        ct = lead.get("customer_tier")
        if ct not in CUSTOMER_TIERS:
            failures.append(f"{ctx} customer_tier invalid: {ct}")

        mrr = lead.get("mrr")
        if not isinstance(mrr, (int, float)) or mrr < 0:
            failures.append(f"{ctx} mrr must be a non-negative number")

        rd = lead.get("renewal_date")
        if not isinstance(rd, str) or len(rd) < 8:
            failures.append(f"{ctx} renewal_date missing or invalid")

        churn = lead.get("churn_risk_score")
        if not isinstance(churn, (int, float)) or churn < 0 or churn > 1:
            failures.append(f"{ctx} churn_risk_score must be 0..1")

        exp = lead.get("expansion_signal")
        if exp is not None:
            if not isinstance(exp, list) or not all(isinstance(x, str) and x for x in exp):
                failures.append(f"{ctx} expansion_signal must be string[]")

    stats = {"name": pack.get("name"), "total": len(leads), "version": "v2"}
    return len(failures) == 0, failures, stats


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: test-lead-pack-v2.py <path-to-leads.json>", file=sys.stderr)
        return 2
    path = Path(argv[1])
    if not path.exists():
        print(f"file not found: {path}", file=sys.stderr)
        return 2

    ok, failures, stats = validate_v2_pack(path)

    if ok:
        print(f"PASS {path}")
        for k, v in stats.items():
            print(f"  {k} = {v}")
        return 0
    print(f"FAIL {path}", file=sys.stderr)
    for f in failures:
        print(f"  - {f}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
