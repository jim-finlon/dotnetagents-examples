# Audio Cues — Manifest (pending synthesis)

Planned audio cues for the Sales Arena. The `.wav` originals will land
on a follow-up lane once the synthesis pipeline has been exercised on a
media-capable runner.

## `bell.wav`

- **When it plays.** Every close event (deal signed by any persona).
- **Length target.** 2.5 seconds.
- **Character.** A clear single-strike trading-floor bell with about
  1.5 seconds of natural decay. Treat it as the signal moment of every
  contest.
- **Notes for synthesis.** Strike at 80% velocity; let the harmonics
  ring out rather than fading abruptly; loudness-normalize so the bell
  is audible at 30% browser-volume.

## `drumroll.wav`

- **When it plays.** Every "glengarry drip" event — the moment a
  premium lead is awarded to the top-of-board persona.
- **Length target.** 4 seconds.
- **Character.** A short ascending drumroll, ending in a single snare
  hit at the four-second mark. The snare hit cues the manager UI
  banner animation.
- **Notes for synthesis.** Tempo ~140 bpm; no cymbal; resolve the roll
  on the beat.

## `sad-trombone.wav`

- **When it plays.** End-of-contest "you're fired" beat for the
  third-place persona. Used by SA-02-06 narrator wrap. Deliberately
  comic-relief; the wrap beat in the demo script depends on this cue
  landing exactly once at contest end.
- **Length target.** 2 seconds.
- **Character.** Standard "wah-wah-waaah" three-note descending
  trombone. Public-domain in spirit; the synthesis must not sample
  any specific copyrighted recording.
- **Notes for synthesis.** Three notes, descending; the third note
  holds for ~0.8 seconds.

## `cold-open.wav`

- **When it plays.** Beat 1 of the 12-minute live demo
  ([SALES-ARENA-DEMO-SCRIPT.md](../../../../examples/sales-arena/README.md)).
- **Length target.** 5 seconds.
- **Character.** A sparse, low-key sting: single muted brass note
  swelling under the narrator's opening line, fading to silence as the
  presenter turns toward the laptop.
- **Notes for synthesis.** Mono is fine; this is a voice-over bed.

## Reviewer gate when audio lands

Before the four `.wav` files are added to this folder, the reviewer must:

1. Confirm CC0 provenance for every file (see `LICENSE.md` reviewer
   checks).
2. Loudness-normalize against a `-16 LUFS` target.
3. Verify file size totals ≤ 3 MB (the asset pack as a whole stays
   ≤ 5 MB per the parent acceptance criteria).
4. Append each landed file to the `LICENSE.md` table with its
   synthesis-tool reference.
