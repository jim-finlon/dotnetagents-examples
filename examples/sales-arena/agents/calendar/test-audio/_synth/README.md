# MeetingAudioSynth

DNA-authored synthesizer for the SA-01-05 Meeting Agent integration test fixture.
Produces a CC0 WAV file at `samples/sales-arena/agents/calendar/test-audio/meeting-demo.wav`.

## Regenerate

```bash
dotnet run --project samples/sales-arena/agents/calendar/test-audio/_synth/MeetingAudioSynth
```

The output is a deterministic sequence of four short sine tones (A4, C5, E5, G5)
separated by silence. There is no speech and no third-party content; the bytes
are CC0 because they are entirely DNA-synthesized via `System.IO` + `Math`.

The integration test in `SalesArena.Meeting.Tests/LiveMeetingCaptureTests.cs`
asserts the file exists and parses as a valid WAV; the transcription content
itself is provided by a stub `IVoiceTranscriptionService` so the test does not
depend on a running Whisper model.
