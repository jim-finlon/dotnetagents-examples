using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesArena.Calendar;

public interface ICalendarAgent
{
    ProposalResult ProposeMeetingTimes(
        AvailabilityRequest request,
        IReadOnlyList<CalendarEvent> busyEvents);
}

/// <summary>
/// Deterministic, conflict-aware meeting-time proposal engine. Walks the window in
/// 15-minute steps, skips any candidate that overlaps a busy event, applies
/// workday-hour filtering (UTC), and emits up to <c>request.OptionCount</c> options
/// (default 3) spaced by <c>MinGapBetweenOptions</c> when supplied.
/// </summary>
public sealed class CalendarAgent : ICalendarAgent
{
    private static readonly TimeSpan StepSize = TimeSpan.FromMinutes(15);

    public ProposalResult ProposeMeetingTimes(
        AvailabilityRequest request,
        IReadOnlyList<CalendarEvent> busyEvents)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (busyEvents is null) throw new ArgumentNullException(nameof(busyEvents));
        if (request.WindowEndUtc <= request.WindowStartUtc)
            throw new ArgumentException("WindowEndUtc must be after WindowStartUtc.", nameof(request));
        if (request.MeetingDuration <= TimeSpan.Zero)
            throw new ArgumentException("MeetingDuration must be > 0.", nameof(request));
        if (request.OptionCount <= 0)
            throw new ArgumentException("OptionCount must be > 0.", nameof(request));

        var diagnostics = new List<string>();
        var busy = busyEvents
            .OrderBy(e => e.StartUtc)
            .ToArray();

        var options = new List<TimeOption>();
        var minGap = request.MinGapBetweenOptions ?? TimeSpan.Zero;
        DateTimeOffset? lastAcceptedEnd = null;

        for (var candidate = AlignToStep(request.WindowStartUtc);
             candidate + request.MeetingDuration <= request.WindowEndUtc;
             candidate += StepSize)
        {
            var slot = new TimeSlot(candidate, candidate + request.MeetingDuration);

            if (!WithinWorkday(slot, request))
                continue;

            if (lastAcceptedEnd is not null && candidate < lastAcceptedEnd.Value + minGap)
                continue;

            var conflict = FirstOverlap(slot, busy);
            if (conflict is not null)
            {
                // Jump candidate forward to just past the conflict to avoid linear retries.
                candidate = AlignToStep(conflict.EndUtc) - StepSize;
                continue;
            }

            options.Add(new TimeOption(options.Count + 1, slot, BuildReason(slot, busy)));
            lastAcceptedEnd = slot.EndUtc;

            if (options.Count >= request.OptionCount) break;
        }

        if (options.Count < request.OptionCount)
            diagnostics.Add($"Found {options.Count} of {request.OptionCount} requested options inside the window.");

        return new ProposalResult(options, diagnostics);
    }

    private static DateTimeOffset AlignToStep(DateTimeOffset dt)
    {
        var ticks = dt.UtcTicks;
        var step = StepSize.Ticks;
        var aligned = ((ticks + step - 1) / step) * step;
        return new DateTimeOffset(aligned, TimeSpan.Zero);
    }

    private static bool WithinWorkday(TimeSlot slot, AvailabilityRequest request)
    {
        if (request.WorkdayStartHourUtc is null && request.WorkdayEndHourUtc is null) return true;
        var startHour = request.WorkdayStartHourUtc ?? 0;
        var endHour = request.WorkdayEndHourUtc ?? 24;
        var hour = slot.StartUtc.UtcDateTime.Hour;
        var endHourOfSlot = slot.EndUtc.UtcDateTime.Hour == 0 ? 24 : slot.EndUtc.UtcDateTime.Hour;
        return hour >= startHour && endHourOfSlot <= endHour;
    }

    private static CalendarEvent? FirstOverlap(TimeSlot slot, IReadOnlyList<CalendarEvent> busy)
    {
        foreach (var evt in busy)
        {
            if (evt.EndUtc <= slot.StartUtc) continue;
            if (evt.StartUtc >= slot.EndUtc) break;
            return evt;
        }
        return null;
    }

    private static string BuildReason(TimeSlot slot, IReadOnlyList<CalendarEvent> busy)
    {
        var nearest = busy.FirstOrDefault(b => b.StartUtc >= slot.EndUtc);
        if (nearest is null) return "Open window; no conflicts in remainder.";
        var gap = nearest.StartUtc - slot.EndUtc;
        return $"Open; {gap.TotalMinutes:0} min before next event \"{nearest.Summary}\".";
    }
}
