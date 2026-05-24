using System;
using System.Collections.Generic;

namespace SalesArena.Calendar;

public sealed record TimeSlot(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public TimeSpan Duration => EndUtc - StartUtc;

    public bool Overlaps(TimeSlot other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;
}

public sealed record CalendarEvent(
    string Uid,
    string Summary,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Organizer = null);

public sealed record AvailabilityRequest(
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    TimeSpan MeetingDuration,
    int OptionCount,
    TimeSpan? MinGapBetweenOptions = null,
    int? WorkdayStartHourUtc = null,
    int? WorkdayEndHourUtc = null);

public sealed record TimeOption(int Rank, TimeSlot Slot, string Reason);

public sealed record ProposalResult(
    IReadOnlyList<TimeOption> Options,
    IReadOnlyList<string> Diagnostics);
