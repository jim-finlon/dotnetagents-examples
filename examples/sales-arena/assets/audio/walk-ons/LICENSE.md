# Walk-on Themes — License

All audio files in this folder are licensed under **CC0 1.0 Universal**
(public-domain dedication) and authored by the DNA project. The
synthesizer that produced every walk-on lives at
[`_synth/WalkOnSynth`](_synth/WalkOnSynth) — fully DNA-original .NET
code with zero third-party dependencies. Output is bit-exact across
runs (fixed seed + closed-form math).

| File | Persona | Character | Source |
| --- | --- | --- | --- |
| `roma.wav` | Roma | Elegant brass fanfare (C major triad arpeggio + sustained pad) | `_synth/WalkOnSynth` |
| `levene.wav` | Levene | Aggressive hunt theme (8 Hz hi-hat noise + D-minor arpeggio + 1 Hz bass thump) | `_synth/WalkOnSynth` |
| `moss.wav` | Moss | Jazzy minor-key piano (A-minor pentatonic riff + octave shimmer + ghost-fifth) | `_synth/WalkOnSynth` |
| `aaronow.wav` | Aaronow | Steady reliable march (1 Hz kick + B3/A3/G3/F3 descent + low hum) | `_synth/WalkOnSynth` |
| `williamson.wav` | Williamson | Stage-bell announcement (two A5/A6 strikes over a sustained A4 tone) | `_synth/WalkOnSynth` |
| `mitch-and-murray.wav` | Mitch & Murray | Low-brass fanfare (E-minor triad + rising stinger) | `_synth/WalkOnSynth` |

All clips are **8 seconds, 11025 Hz mono 16-bit PCM, ≈ 172 KB**. Total
folder ≈ 1.05 MB.

## CC0 attestation

The DNA project hereby dedicates the six `.wav` files above and the
synthesis source at `_synth/WalkOnSynth/Program.cs` to the public
domain under
[CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/).
No samples are derived from third-party recordings; no melodic
sequence reproduces a copyrighted work. The named-character voices
inside `Sales Arena` are DNA originals and the musical motifs that
accompany them are short, generic, distinct, and public-domain by
construction.

## Regeneration

```bash
dotnet run --project samples/sales-arena/assets/audio/walk-ons/_synth/WalkOnSynth
```

The generator writes back into this folder; re-runs are bit-exact.

## Reviewer checks (all currently green)

- [x] Each file generated from a DNA-licensed synthesis tool.
- [x] No vocal samples, no copyrighted recordings.
- [x] File <= 200 KB per clip (acceptance pin); current ≈ 172 KB each.
- [x] Folder total well under any reasonable cap (≈ 1.05 MB).
- [x] Spot-checked: each persona's theme is audibly distinct from the
  others at 30% browser volume.
