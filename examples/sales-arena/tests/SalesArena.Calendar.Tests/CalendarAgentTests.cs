using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace SalesArena.Calendar.Tests;

public class CalendarAgentTests
{
    private static readonly DateTimeOffset Day = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static AvailabilityRequest Window(
        int startHour, int endHour,
        TimeSpan? duration = null,
        int? optionCount = null,
        TimeSpan? gap = null,
        int? wdStart = null,
        int? wdEnd = null) =>
        new(
            WindowStartUtc: Day.AddHours(startHour),
            WindowEndUtc: Day.AddHours(endHour),
            MeetingDuration: duration ?? TimeSpan.FromMinutes(30),
            OptionCount: optionCount ?? 3,
            MinGapBetweenOptions: gap,
            WorkdayStartHourUtc: wdStart,
            WorkdayEndHourUtc: wdEnd);

    private static CalendarEvent Busy(int startHour, int endHour, string summary = "Busy")
        => new(Guid.NewGuid().ToString("N"), summary, Day.AddHours(startHour), Day.AddHours(endHour));

    [Fact]
    public void EmptyCalendar_ReturnsRequestedOptionCount()
    {
        var sut = new CalendarAgent();
        var result = sut.ProposeMeetingTimes(Window(9, 17), Array.Empty<CalendarEvent>());

        result.Options.Should().HaveCount(3);
        result.Options[0].Rank.Should().Be(1);
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void OptionsAreStepAligned_15MinGrid()
    {
        var sut = new CalendarAgent();
        var req = Window(9, 17) with { WindowStartUtc = Day.AddHours(9).AddMinutes(7) };

        var result = sut.ProposeMeetingTimes(req, Array.Empty<CalendarEvent>());

        result.Options[0].Slot.StartUtc.Minute.Should().BeOneOf(0, 15, 30, 45);
    }

    [Fact]
    public void BusyOverlap_IsSkipped()
    {
        var sut = new CalendarAgent();
        var busy = new[] { Busy(9, 10, "Existing") };

        var result = sut.ProposeMeetingTimes(Window(9, 17, optionCount: 1), busy);

        result.Options.Should().ContainSingle();
        result.Options[0].Slot.StartUtc.Should().BeOnOrAfter(Day.AddHours(10));
    }

    [Fact]
    public void MultipleBusyBlocks_OptionsLandInGaps()
    {
        var sut = new CalendarAgent();
        var busy = new[] { Busy(9, 10), Busy(11, 12), Busy(13, 14) };

        var result = sut.ProposeMeetingTimes(Window(9, 17, optionCount: 3), busy);

        result.Options.Should().HaveCount(3);
        foreach (var opt in result.Options)
            foreach (var b in busy)
                opt.Slot.Overlaps(new TimeSlot(b.StartUtc, b.EndUtc)).Should().BeFalse();
    }

    [Fact]
    public void MinGapBetweenOptions_IsRespected()
    {
        var sut = new CalendarAgent();
        var req = Window(9, 17, optionCount: 3, gap: TimeSpan.FromHours(1));

        var result = sut.ProposeMeetingTimes(req, Array.Empty<CalendarEvent>());

        for (int i = 1; i < result.Options.Count; i++)
        {
            var delta = result.Options[i].Slot.StartUtc - result.Options[i - 1].Slot.EndUtc;
            delta.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(1));
        }
    }

    [Fact]
    public void WorkdayWindow_ClampsOptions()
    {
        var sut = new CalendarAgent();
        var req = Window(0, 24, optionCount: 6, wdStart: 9, wdEnd: 17);

        var result = sut.ProposeMeetingTimes(req, Array.Empty<CalendarEvent>());

        foreach (var opt in result.Options)
            opt.Slot.StartUtc.UtcDateTime.Hour.Should().BeGreaterThanOrEqualTo(9).And.BeLessThan(17);
    }

    [Fact]
    public void Diagnostic_EmittedWhenFewerOptionsThanRequested()
    {
        var sut = new CalendarAgent();
        var busy = new[] { Busy(9, 16, "All-day workshop") };

        var result = sut.ProposeMeetingTimes(Window(9, 17, optionCount: 3), busy);

        result.Options.Count.Should().BeLessThan(3);
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics[0].Should().Contain("Found");
    }

    [Fact]
    public void InvalidWindow_Throws()
    {
        var sut = new CalendarAgent();
        var bad = Window(9, 9);
        var act = () => sut.ProposeMeetingTimes(bad, Array.Empty<CalendarEvent>());
        act.Should().Throw<ArgumentException>().WithMessage("*WindowEndUtc*");
    }

    [Fact]
    public void NonPositiveDuration_Throws()
    {
        var sut = new CalendarAgent();
        var bad = Window(9, 17, duration: TimeSpan.Zero);
        var act = () => sut.ProposeMeetingTimes(bad, Array.Empty<CalendarEvent>());
        act.Should().Throw<ArgumentException>().WithMessage("*MeetingDuration*");
    }
}
