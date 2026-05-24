#!/usr/bin/env python3
"""Generate SA-05-05 outreach templates (72 files). Idempotent: skips existing non-placeholder files."""

from __future__ import annotations

import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PERSONAS = ["roma", "levene", "moss", "aaronow", "williamson", "mitch-and-murray"]
CHANNELS = ["email", "sms", "linkedin", "chat"]
VARIANTS = [
    ("variant-1", "Lead with a question that invites a one-line reply."),
    ("variant-2", "Lead with anonymized social proof from a peer segment."),
    ("variant-3", "Lead with time-bounded urgency without fabricated pricing."),
]

SIGN = {
    "roma": "Roma",
    "levene": "Levene",
    "moss": "Moss",
    "aaronow": "Aaronow",
    "williamson": "Williamson",
    "mitch-and-murray": "Mitch & Murray",
}

WORD_TARGET = {"email": 95, "sms": 35, "linkedin": 55, "chat": 45}
SUBS = "prospect.first_name, prospect.company, prospect.industry, prospect.recent_topic"


def narrator_body(channel: str) -> str:
    return (
        f"({channel} — narrator persona — not sent)\n\n"
        "Mitch & Murray does not send outbound touches. "
        "This file satisfies the outreach template contract only.\n"
    )


def body_for(persona: str, channel: str, variant: str, hypothesis: str) -> str:
    if persona == "mitch-and-murray":
        return narrator_body(channel)

    first = "{{prospect.first_name}}"
    company = "{{prospect.company}}"
    industry = "{{prospect.industry}}"

    if channel == "email":
        if variant == "variant-1":
            return (
                f"Subject: Quick question for {company}\n\n"
                f"{first}, what would change this quarter if {company} could answer "
                f"one pipeline question in under five minutes?\n\n— {SIGN[persona]}"
            )
        if variant == "variant-2":
            return (
                f"Subject: Peers in {industry}\n\n"
                f"{first}, teams similar to {company} cut forecast prep time after "
                f"standardizing one weekly review ritual. Want the one-page summary?\n\n— {SIGN[persona]}"
            )
        return (
            f"Subject: Before your next team sync\n\n"
            f"{first}, we have two onboarding slots left this month for {industry} operators. "
            f"Reply \"hold\" if you want a 15-minute walkthrough.\n\n— {SIGN[persona]}"
        )

    if channel == "sms":
        if variant == "variant-1":
            return f"{first} — quick question about {company}'s pipeline rhythm. Worth a one-line reply? — {SIGN[persona]}"
        if variant == "variant-2":
            return f"{first}, a peer in {industry} shared a simple forecast ritual. Want the bullet version? — {SIGN[persona]}"
        return f"{first}, holding two demo slots this week for {industry} teams. Reply HOLD if interested. — {SIGN[persona]}"

    if channel == "linkedin":
        if variant == "variant-1":
            return f"Hi {first} — curious how {company} runs weekly pipeline reviews. Open to a short exchange? — {SIGN[persona]}"
        if variant == "variant-2":
            return f"{first}, operators in {industry} trimmed forecast prep with one shared dashboard. Happy to share notes. — {SIGN[persona]}"
        return f"{first}, closing two intro calls this week for {industry} leaders. Ping me if timing works. — {SIGN[persona]}"

    # chat
    if variant == "variant-1":
        return f"Hey {first} — what's the one metric {company} checks before green-lighting a deal? — {SIGN[persona]}"
    if variant == "variant-2":
        return f"{first}, saw a team in {industry} unblock reps with a lighter handoff checklist. Want it? — {SIGN[persona]}"
    return f"{first}, we can walk through a 10-min demo Thu/Fri if {company} is exploring forecast tooling. — {SIGN[persona]}"


def frontmatter(persona: str, channel: str, variant: str, hypothesis: str) -> str:
    narrator = persona == "mitch-and-murray"
    lines = [
        "---",
        f'hypothesis: "{hypothesis}"',
        f"variant_id: {channel}-{variant.split('-')[1]}",
        f"word_count_target: {WORD_TARGET[channel]}",
        f"substitutions: {SUBS}",
    ]
    if narrator:
        lines.append("narrator_only: true")
    lines.append("---")
    return "\n".join(lines)


def should_skip(path: Path) -> bool:
    if not path.exists():
        return False
    text = path.read_text(encoding="utf-8")
    if "word_count_target:" in text and len(text) > 400:
        return True  # preserve polished long-form variant-1 emails
    return False


def main() -> None:
    created = 0
    skipped = 0
    for persona in PERSONAS:
        for channel in CHANNELS:
            for variant, hypo in VARIANTS:
                path = ROOT / persona / "outreach" / channel / f"{variant}.md"
                path.parent.mkdir(parents=True, exist_ok=True)
                if should_skip(path):
                    skipped += 1
                    continue
                content = frontmatter(persona, channel, variant, hypo) + "\n" + body_for(persona, channel, variant, hypo) + "\n"
                path.write_text(content, encoding="utf-8", newline="\n")
                created += 1
    print(f"created={created} skipped={skipped}")


if __name__ == "__main__":
    main()
