# Audio cue synthesizer

DNA-authored one-off generator for the four SA-06-04 audio cues
(`bell.wav`, `drumroll.wav`, `sad-trombone.wav`, `cold-open.wav`).
Pure-.NET — no third-party dependencies.

## Regenerate

```bash
dotnet run --project samples/sales-arena/assets/audio/_synth/AudioCueSynth
```

The program writes the four `.wav` files to
`samples/sales-arena/assets/audio/`. Output is bit-exact across runs
(fixed seed + closed-form math), so re-running and re-committing is
safe — only edit the synthesis parameters in `Program.cs` if you
intend to change the cue.

## License

The generator + the `.wav` outputs are all DNA-original work, released
under CC0 1.0 Universal. See `samples/sales-arena/assets/audio/LICENSE.md`
for the per-file provenance table.

## Why ship the generator alongside the audio

- Provenance: a future maintainer can read the `Program.cs` and see
  exactly how each cue was synthesized (every parameter, every frequency).
- Audit: independent reviewers can re-run the build and verify the
  checked-in `.wav` files match what `Program.cs` produces.
- Regeneration: when a future story changes a cue (e.g., a louder
  bell), the diff is in this folder — not in a black-box binary.
