# Meeting Agent (text-only slice)

Deterministic, offline Meeting Agent surface from the Sales Arena flagship plan
([SALES-ARENA-FLAGSHIP-PLAN.md §4.2](../../../../../examples/sales-arena/README.md)).

This slice (story aa→`ac7584aa-77fb-4f1f-9b5e-ef33dbe7e344`) covers the text-only half of parent
SA-01-05 (`5554a795`). The two heavy lifts in the parent — synthetic
copyright-clean demo audio + live `DotNetAgents.Voice.Transcription` integration
— stay under SA-01-05 for a separate follow-up child (SA-01-05b).

## Surface

- `IMeetingBriefer` / `MeetingBriefer` — pre-meeting brief assembly via
  injected providers (`ICompanyNewsProvider`, `IMutualConnectionProvider`,
  `ICrmHistoryProvider`, `IKnowledgeTalkingPointProvider`). Empty-default
  implementations are included for offline tests.
- `IPostMeetingSummarizer` / `PostMeetingSummarizer` — heuristic transcript→summary
  with a pluggable `ISummaryExtractionStrategy` so a later LLM strategy can be
  dropped in without changing callers.
- Records: `ProspectId`, `Persona`, `Brief`, `MeetingTranscript`,
  `TranscriptTurn`, `MeetingSummary`, `Decision`, `ActionItem`, `Objection`,
  `NextStep`, `SentimentShift`, `CrmStageChangeEvent`.
- `ICrmEventPublisher` (stub) — real wiring lives in the SA-01-01 CRM Agent;
  this slice ships a `RecordingCrmEventPublisher` test recorder.

## Deferred to follow-up (SA-01-05b)

- `LiveMeetingCapture` wrapping `DotNetAgents.Voice.Transcription` with topic
  tagging + decision detection.
- Synthetic, copyright-clean demo audio under `agents/calendar/test-audio/`.
- Audio→transcript→summary integration test using the demo fixture.
- LLM-backed `ISummaryExtractionStrategy` once `DotNetAgents` exposes the
  preferred local Whisper / LLM bridge for offline scoring.
- Live CRM publish wired through `SA-01-01` `CrmAgent` once it exposes the
  publisher.
