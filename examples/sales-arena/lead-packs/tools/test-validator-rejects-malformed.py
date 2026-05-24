#!/usr/bin/env python3
"""
Negative-path test: confirms the validator (test-lead-pack.py) refuses
malformed packs. The AC says "Validator refuses malformed packs"; this is the
test that proves it.

For each malformed fixture, validate_pack() must return ok=False and at least
one failure mentioning the right invariant.
"""

from __future__ import annotations

import json
import sys
import tempfile
from pathlib import Path

# Add the sibling module to the import path.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from importlib import util as _util  # noqa: E402

_spec = _util.spec_from_file_location("test_lead_pack", Path(__file__).resolve().parent / "test-lead-pack.py")
_mod = _util.module_from_spec(_spec)  # type: ignore[arg-type]
assert _spec is not None and _spec.loader is not None
_spec.loader.exec_module(_mod)  # type: ignore[union-attr]
validate_pack = _mod.validate_pack  # type: ignore[attr-defined]


FIXTURES: list[tuple[str, dict, str]] = [
    (
        "missing-version",
        {"name": "x", "description": "x", "synthetic": True, "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X"}}]},
        "version",
    ),
    (
        "wrong-version",
        {"version": "v0", "name": "x", "description": "x", "synthetic": True, "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X"}}]},
        "version",
    ),
    (
        "missing-synthetic-flag",
        {"version": "v1", "name": "x", "description": "x", "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X"}}]},
        "synthetic",
    ),
    (
        "real-domain-leak",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X", "domain": "evil-co.com"}}]},
        ".example/.test/.invalid",
    ),
    (
        "real-phone-leak",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X"},
                    "contact": {"firstName": "A", "lastName": "B", "phone": "212-555-0142"}}]},
        "555-NNNN",
    ),
    (
        "glengarry-without-signals",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "L-0001", "tier": "glengarry", "company": {"name": "X"},
                    "contact": {"firstName": "A", "lastName": "B"}}]},
        "signal",
    ),
    (
        "duplicate-ids",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X"}},
                   {"id": "L-0001", "tier": "cold", "company": {"name": "Y"}}]},
        "duplicate id",
    ),
    (
        "id-pattern-violation",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "lead-1", "tier": "cold", "company": {"name": "X"}}]},
        "id pattern",
    ),
    (
        "invalid-industry",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "L-0001", "tier": "cold", "company": {"name": "X", "industry": "narcotics"}}]},
        "industry invalid",
    ),
    (
        "invalid-signal-kind",
        {"version": "v1", "name": "x", "description": "x", "synthetic": True,
         "leads": [{"id": "L-0001", "tier": "glengarry", "company": {"name": "X"},
                    "contact": {"firstName": "A", "lastName": "B"},
                    "signals": [{"kind": "vibe-shift", "summary": "x"}]}]},
        "kind invalid",
    ),
]


def main() -> int:
    fail_count = 0
    pass_count = 0
    for name, payload, expected_keyword in FIXTURES:
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, encoding="utf-8") as fp:
            json.dump(payload, fp)
            tmp = Path(fp.name)

        ok, failures, _ = validate_pack(tmp)
        tmp.unlink(missing_ok=True)

        if ok:
            print(f"FAIL: {name} should have been rejected, but validate_pack returned ok=True")
            fail_count += 1
            continue

        joined = " | ".join(failures)
        if expected_keyword.lower() not in joined.lower():
            print(f"FAIL: {name} rejected, but no failure mentioned '{expected_keyword}'. Failures: {joined}")
            fail_count += 1
            continue

        pass_count += 1

    print(f"NEGATIVE-PATH: {pass_count}/{len(FIXTURES)} fixtures correctly rejected")
    return 0 if fail_count == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
