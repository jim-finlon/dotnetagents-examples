# LICENSE — `samples/sales-arena/agents/calendar/test-audio/`

All audio files in this directory are CC0 1.0 Universal (public-domain dedication)
because they are entirely DNA-synthesized via `MeetingAudioSynth` (pure
`System.IO` + `Math`, no third-party samples or models). See `_synth/README.md`
for regeneration. There is no human speech in this fixture; the integration
test in `SalesArena.Meeting.Tests/LiveMeetingCaptureTests.cs` uses a stubbed
`IVoiceTranscriptionService` that returns canned text, so the audio file only
needs to be a valid WAV.
