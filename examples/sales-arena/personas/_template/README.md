# Persona Pack Template

> *"There's a new sheriff in town. Hi, I'm $YOUR_PERSONA_NAME and I close deals."*

This template is the skeleton for authoring a Sales-Arena persona pack. Fork it, name your closer, give them a personality, ship them to the Gallery.

## What's a persona pack?

A persona pack is **how an AI salesperson is configured**. It's a directory (or `.salesman.zip` once SA-07-01 ships) that tells the Arena:

- **Who they are** — system prompt + bio + style
- **How they sell** — cadence, channel posture, A/B variants
- **What they offer** — proposal templates per pricing tier
- **What they look like** — avatar + display name
- **What they know** — links into the shared Knowledge pack

## Files in a persona pack

```
personas/<your-persona>/
├── persona.yaml              # Top-level config (name, author, model-tier hint, tags)
├── system-prompt.md          # The persona's voice + decision posture
├── cadence.yaml              # Touches/day, channel mix, follow-up rules
├── bio.md                    # 300-word character bio (for the Gallery card)
├── avatar.svg                # Display avatar (or .png — keep it small)
├── outreach/                 # Channel-specific outbound templates
│   ├── email/
│   │   ├── variant-1.md      # A/B variant 1 (hypothesis-tagged in frontmatter)
│   │   ├── variant-2.md
│   │   └── variant-3.md
│   ├── sms/                  # Same shape
│   ├── linkedin/
│   └── chat/
├── proposals/                # Persona-aware proposal templates
│   ├── starter.md
│   ├── pro.md
│   └── enterprise.md
├── behavior-tree.yaml        # Next-Best-Action tree (optional — defaults supplied)
└── manifest.json             # File hashes + author signature (SA-07-01)
```

## Quickstart — "Build your closer in 30 minutes"

1. Copy this template:
   ```bash
   cp -r samples/sales-arena/personas/_template samples/sales-arena/personas/your-closer-name
   ```

2. Edit `persona.yaml` — name them, tag them, pick a model tier:
   ```yaml
   name: "Sammy 'Three-Bell' Sullivan"
   author: "your-handle"
   version: "0.1.0"
   model_tier: "local-strong"   # local-light / local-strong / frontier-fallback
   tags: ["hardballer", "phone-first", "no-prisoners"]
   bio_ref: "bio.md"
   ```

3. Write `system-prompt.md` — give them a voice. What's their philosophy? Their tone? Their no-go's?

4. Tune `cadence.yaml` — how often do they touch? Do they prefer phone or email? When do they switch channels?

5. Author at least one `outreach/email/variant-1.md` — a cold email template. Use `{{prospect.company}}`, `{{prospect.role}}`, `{{persona.style}}` tokens.

6. Author at least one `proposals/starter.md` — what does their starter proposal look like?

7. Test:
   ```bash
   dna-arena persona validate samples/sales-arena/personas/your-closer-name
   dna-arena contest start --personas roma,your-closer-name --hours 1 --time-compression 60
   ```

8. Watch the bell ring. Or ring sadly. Either way, you learned something.

## The 30-minute quality bar

- Persona's system prompt is at least 200 words and distinctive (not "you are a friendly sales rep")
- At least one outreach variant per channel they actually use
- At least one proposal template
- Cadence sums to a realistic touch volume (5-50/day depending on style)
- The `bio.md` is fun to read — your persona has a *character*

## When you're ready to share

After SA-07-01 ships, you can:

```bash
dna-arena persona export --name your-closer-name --out your-closer.salesman.zip
```

That `.salesman.zip` is shareable. Drop it into anyone else's Arena and watch your closer go head-to-head.

## The hall of fame (after SA-07 ships)

- ELO leaderboard at `/gallery/leaderboard`
- Tournament brackets at `/gallery/bracket/<id>`
- Themed seasons (Glengarry / Wolf-of-Wall-Street / Office-Space)

## Resources

- [Persona-pack format spec (SA-07-01)](../../../docs/public/SALES-ARENA-PERSONA-PACK-FORMAT.md) *(authored once SA-07-01 ships)*
- [Workshop: Build your closer in an hour (SA-07-05)](../../../docs/public/SALES-ARENA-WORKSHOP.md) *(authored once SA-07-05 ships)*
- [Sales Arena Flagship Plan](../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)

---

*"Always be closing."* — Blake
*"Always be character-distinct."* — DNA persona-pack distinctness test
