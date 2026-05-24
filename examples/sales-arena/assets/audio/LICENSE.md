# Audio Cues — License

All audio files in this folder are licensed under **CC0 1.0 Universal**
(public-domain dedication) and authored by the DNA project. The
synthesizer that produced every `.wav` lives at
[`_synth/AudioCueSynth`](_synth/AudioCueSynth) — fully DNA-original
.NET code with zero third-party dependencies. Output is bit-exact
across runs (fixed seed + closed-form math).

| File | Author | Source | Used by |
| --- | --- | --- | --- |
| `bell.wav` | DNA project | `_synth/AudioCueSynth` (E6 + harmonics, exponential decay) | SA-02-06 Narrator on close events |
| `drumroll.wav` | DNA project | `_synth/AudioCueSynth` (filtered noise + rising envelope + snare-hit transient) | SA-02-06 Narrator on glengarry-drip events |
| `sad-trombone.wav` | DNA project | `_synth/AudioCueSynth` (Bb3 -> A3 -> Ab3 -> G3 sawtooth descent) | SA-02-06 Narrator on third-place wrap |
| `cold-open.wav` | DNA project | `_synth/AudioCueSynth` (A minor triad brass swell, slow attack/release) | SA-06-02 demo cold-open beat |

## CC0 attestation

The DNA project (acting through its operators + agent contributors)
hereby dedicates the four `.wav` files above and the synthesis source
at `_synth/AudioCueSynth/Program.cs` to the public domain under
[CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/).
No samples are derived from third-party recordings; no melodic
sequence reproduces a copyrighted work; the descending "sad trombone"
gesture is a public-domain musical figure with long-standing prior art.

## Regeneration

```bash
dotnet run --project samples/sales-arena/assets/audio/_synth/AudioCueSynth
```

The generator writes back into this folder; re-runs are bit-exact.

## Reviewer checks (all currently green)

- [x] Each file generated from a DNA-licensed synthesis tool
  (`_synth/AudioCueSynth/Program.cs`).
- [x] No vocal samples that could be identifiable to a specific person.
- [x] No melodic samples from copyrighted recordings.
- [x] File <= 500 KB; total audio folder <= 3 MB (verified by
  `SalesArena.AudioCues.Tests`).
- [x] Spot-checked in a stock browser at 30% volume — no clipping,
  no perceived DC offset, no distortion in the synthesized output.
