# Persona Pack — Workshop Notebook

> Companion notebook for the
> [Sales Arena Build-Your-Closer Workshop](../../../../examples/sales-arena/README.md).
> Copy this file into your persona directory and check off the boxes as
> you go.

This notebook is the same eight-block sequence as the workshop guide,
formatted as a working checklist. The full prose, walkthrough examples,
and distinctness-check rationale live in the workshop guide; treat this
file as the per-persona log you actually edit.

---

## Persona under construction

- **Name:** _____
- **Author handle:** _____
- **Date:** _____
- **Workshop start time:** _____
- **Workshop end time (target T + 60 min):** _____

---

## Block 1 — Casting (5 min)

- [ ] Picked a name.
- [ ] Picked an archetype in one phrase: _____
- [ ] Picked 3 tags: _____ / _____ / _____
- [ ] Wrote 3 no-go's: _____ / _____ / _____
- [ ] Copied the template:
      `cp -r examples/sales-arena/personas/_template examples/sales-arena/personas/<your-closer>`.
- [ ] Edited `persona.yaml` with name, author, version, model_tier, tags.

**Distinctness check.** Read the name and tags out loud. Could they
describe any rep at any company? If yes, add a hook.

---

## Block 2 — Voice (10 min)

Edit `system-prompt.md`. Each section gets 2 minutes.

- [ ] **Identity** — one sentence: _____
- [ ] **Philosophy** — two sentences.
- [ ] **Tone** — 3-4 adjectives: _____
- [ ] **Tactics they reach for first** — 3-4 bullets.
- [ ] **No-go's** — explicit refusal list.

**Distinctness check.** Show the prompt to someone who doesn't know
your persona. Ask them to describe the person. Generic answer → rewrite.

---

## Block 3 — Cadence (5 min)

Edit `cadence.yaml`.

- [ ] `touches_per_day`: _____
- [ ] `channel_mix` sums to 1.0.
- [ ] `follow_up.max_attempts`: _____
- [ ] `follow_up.break_glass_after_days`: _____
- [ ] `quiet_hours_local`: _____ → _____

**Distinctness check.** Channel mix totals 1.0 on purpose, not by
matchmaker normalization.

---

## Block 4 — Outreach (10 min)

At minimum: one variant per active channel.

- [ ] `outreach/email/variant-1.md` written.
- [ ] Outreach variants for the other active channels written:
  - [ ] `outreach/phone/script-1.md` (if phone is active)
  - [ ] `outreach/linkedin/variant-1.md` (if linkedin is active)
  - [ ] `outreach/sms/variant-1.md` (if sms is active)
  - [ ] `outreach/chat/variant-1.md` (if chat is active)
- [ ] Each variant has a `hypothesis:` line in the frontmatter.

**Distinctness check.** The first subject line / opening sentence is
unmistakably your closer.

---

## Block 5 — Proposals (10 min)

Pick the tier your closer is most opinionated about.

- [ ] One of: `proposals/starter.md` / `proposals/pro.md` / `proposals/enterprise.md`
- [ ] Proposal answers: problem in 1 sentence, solution in 3, price + bundle, next step, signature move.
- [ ] Signature move is postcard-worthy.

**Distinctness check.** Cover the title, read sections 1 and 2 — the
buyer's problem is specific.

---

## Block 6 — Bio + avatar (5 min)

- [ ] `bio.md` written. ~300 words.
- [ ] First sentence is a hook.
- [ ] Last sentence is a catchphrase.
- [ ] `avatar.svg` (or `.png`) in place — placeholder is fine.

**Distinctness check.** Read the bio aloud and smile.

---

## Block 7 — Validate + sign (5 min)

```bash
dna-arena persona validate examples/sales-arena/personas/<your-closer>
dna-arena persona export --name <your-closer> --sign --out <your-closer>.salesman.zip
```

- [ ] Validator passes.
- [ ] Export + sign succeeds.
- [ ] `manifest.json` shows your signature.

**Distinctness check.**
`dna-arena persona summary <your-closer>` matches what's in your head.

---

## Block 8 — Bell (10 min)

```bash
dna-arena contest start \
  --personas roma,<your-closer> \
  --lead-pack examples/sales-arena/lead-packs/synthetic-200.json \
  --hours 1 --time-compression 60
```

- [ ] Contest started.
- [ ] Leaderboard observed live at `http://localhost:5005/leaderboard`.
- [ ] Contest finished.

After:

```bash
dna-arena replay summary --contest-id <id>
```

- [ ] Win-rate noted: _____
- [ ] Revenue noted: _____
- [ ] Top 5 money turns reviewed.
- [ ] At least one money turn is something only your closer would have
      said.

**Distinctness check.** The top money turn is recognizably yours.

---

## Stretch goals (only after bell)

- [ ] Second outreach variant per channel.
- [ ] Second proposal tier.
- [ ] Tuned cadence based on contest log.
- [ ] `behavior-tree.yaml` override for one specific objection.
- [ ] Re-ran the contest against a different persona.

---

## Workshop telemetry

- **Total minutes spent:** _____
- **Block where I lost the most time:** _____
- **What unblocked me:** _____
- **What I'd cut to ship in 60 minutes next time:** _____

Open an issue tagged `sa-07-05-workshop` if your run exceeded 60
minutes; the workshop guide owes you a tightening.

---

## Quality bar — final checklist

Ship-ready when every box is checked:

- [ ] System prompt ≥ 200 words and the Block-2 distinctness check
      passed.
- [ ] One outreach variant per active channel with hypothesis
      frontmatter.
- [ ] One proposal with a signature move.
- [ ] `bio.md` is fun to read aloud.
- [ ] Validator passes; pack is signed.
- [ ] A head-to-head replay exists and the top money turn is yours.

When all six boxes are ticked, your closer is Gallery-ready. Bell.

---

## Related docs

- [Workshop guide (SA-07-05)](../../../../examples/sales-arena/README.md)
  — the long-form version of this notebook.
- [Persona template README](README.md) — the 30-minute quickstart
  version.
- [Sales Arena Flagship Plan §SA-07](../../../../examples/sales-arena/README.md)
  — the parent plan.
