# Calendar Agent

Story `68c6374d` (SA-01-04). Deterministic, conflict-aware meeting-time proposal
engine + minimal RFC 5545 ICS reader for the Sales Arena ([SALES-ARENA-FLAGSHIP-PLAN.md §4.1](../../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)).

## Surface

- `ICalendarAgent` + `CalendarAgent` — proposes up to N meeting time options
  inside a window, skipping busy-event overlaps and (optionally) clamping to a
  workday-hour window in UTC. 15-minute step alignment for stable output.
- `IcsAdapter.Parse(string | TextReader)` — minimal ICS reader supporting
  `VEVENT` with `UID` / `SUMMARY` / `ORGANIZER` / `DTSTART` / `DTEND` in UTC
  (`YYYYMMDDTHHMMSSZ`). Sufficient for demo calendars; not a complete RFC 5545
  implementation.
- Records: `TimeSlot` (with `Overlaps`), `CalendarEvent`, `AvailabilityRequest`,
  `TimeOption`, `ProposalResult`.

## Companion `Meeting/` subdir

The earlier `samples/sales-arena/agents/calendar/Meeting/` slice (story
`ac7584aa`) ships the text-only pre-meeting brief assembler + post-meeting
summarizer. This story adds the scheduling half of the Calendar Agent.

## Deferred to follow-up

- Time-zone aware workday windows via NodaTime (current implementation works in
  UTC only).
- RFC 5545 `RRULE` / `VTIMEZONE` / floating-time parsing.
- Calendar write (CalDAV) — read-only adapter in this slice.
- Per-attendee free/busy aggregation.
