# Sales Arena — Theatrical Asset Pack

The atmosphere that makes a Sales Arena contest feel like a contest. Every
file in this tree is **CC0 / project-owned** — see `LICENSE.md` per
sub-folder for the per-file provenance.

## Contents

| Folder | What it holds | Used by |
| --- | --- | --- |
| `ascii/` | ASCII-art frames for the CLI floor view | SA-04-03 `dna-arena floor` |
| `svg/` | Banner + persona-card frame + tier badges + leaderboard background | SA-03-02 Floor + SA-03-03 Leaderboard |
| `audio/` | Bell, drumroll, sad trombone, cold-open audio cues | SA-02-06 Narrator + SA-08-01 Bell Stream |

The audio sub-folder currently ships a `LICENSE.md` and a `MANIFEST.md`
that document the planned cues, but the `.wav` originals themselves are
deferred to a follow-up story so the synthesis pipeline can be exercised
on a media-capable lane.

## Provenance

- All ASCII art was authored in plain text by the DNA project.
- All SVG components were authored by hand in this repo.
- All planned audio cues are scheduled to be synthesized from DNA-licensed
  generation tooling and stored as CC0 source-of-truth files in
  `assets/audio/`.

No third-party samples, no font embedding, no tracking pixels in SVG, no
external `<image>` or `<script>` references. The assets are entirely
self-contained.

## Total size budget

Per the parent acceptance criteria, the asset pack stays under **5 MB**
total. The ASCII + SVG slice is well under 100 KB; the audio slice will
land with its own size review against the 5 MB cap.
