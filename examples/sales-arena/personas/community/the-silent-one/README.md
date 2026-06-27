# The Silent One — Quiet John Mulcahy

Glengarry's seventh closer, and the worked example from
[`examples/sales-arena/README.md`](../../../../../examples/sales-arena/README.md).

A deliberately under-talking closer who lets the prospect fill the silence.
The pack's edge is what John does not say: one short question per turn,
no voicemails, no "circling back" follow-ups, no four-paragraph cold
emails. Phone and Zoom only. Phone replaces the email after the second
touch when the prospect has gone silent.

## What is in this pack

- `persona.yaml` — tags, model tier, file refs.
- `system-prompt.md` — five-section character sheet (identity, philosophy,
  voice, decision posture, refusals).
- `bio.md` — 400-word backstory describing how Mulcahy came to the
  silent-treatment school of closing.
- `cadence.yaml` — six touches per day, 50/30/20 phone/email/LinkedIn,
  voicemails disabled.
- `outreach/email/variant-{1..5}.md` — five outreach hypotheses,
  including the two-sentence cold-open from the tutorial (variant 1).
- `proposals/starter.md` — short proposal that puts the price inside the
  doc, not on the cover email.
- `avatar.svg` — quiet ellipsis on a dark muted gradient.

## How the fixture tests this pack

`SalesArena.Personas.Tests.TheSilentOnePackTests` exercises the same
shape of assertions used for the other three seeded community packs
(influencer / engineer / hardballer) plus the persona-fork-tutorial
distinctness checks against Roma:

- The pack directory exists and ships every required file.
- The bio is at least 300 words (the SA-07-06 acceptance).
- The pack round-trips through the `ZipPersonaPackFormat` codec
  (the same SA-07-01 codec the other community packs round-trip
  through).
- The system prompt is at least the persona-pack 200-word floor.
- The cadence channel mix sums to 1.0, and voicemails are disabled.
- The system prompt is at least 0.30 in cosine distance from each of
  the three other seeded community packs (influencer / engineer /
  hardballer), so the worked example does not silently converge on
  one of the existing voices.

The fixture is the durable guard against the
`SALES-ARENA-FORK-YOUR-PERSONA.md` tutorial drifting as the persona-pack
convention evolves. If the tutorial changes the worked example's
`persona.yaml`, `cadence.yaml`, or outreach variant 1, the fixture
fails until the pack catches up.
