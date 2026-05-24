using System;
using FluentAssertions;
using Xunit;

namespace SalesArena.Calendar.Tests;

public class IcsAdapterTests
{
    private const string Sample = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//mitchmurray//demo//EN
        BEGIN:VEVENT
        UID:event-1@mitchmurray.example
        SUMMARY:Greybridge intro call
        ORGANIZER:moss@mitchmurray.example
        DTSTART:20260601T140000Z
        DTEND:20260601T143000Z
        END:VEVENT
        BEGIN:VEVENT
        UID:event-2@mitchmurray.example
        SUMMARY:Stratham follow-up
        DTSTART:20260602T150000Z
        DTEND:20260602T160000Z
        END:VEVENT
        END:VCALENDAR
        """;

    [Fact]
    public void Parse_ReturnsAllVEvents()
    {
        var events = IcsAdapter.Parse(Sample);
        events.Should().HaveCount(2);
        events[0].Uid.Should().Be("event-1@mitchmurray.example");
        events[0].StartUtc.Should().Be(new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero));
        events[0].EndUtc.Should().Be(new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero));
        events[1].Organizer.Should().BeNull();
    }

    [Fact]
    public void Parse_MissingFields_Throws()
    {
        var bad = "BEGIN:VEVENT\nUID:x\nEND:VEVENT\n";
        var act = () => IcsAdapter.Parse(bad);
        act.Should().Throw<FormatException>().WithMessage("*UID/SUMMARY/DTSTART/DTEND*");
    }

    [Fact]
    public void Parse_UnclosedVEvent_Throws()
    {
        var bad = "BEGIN:VEVENT\nUID:x\nSUMMARY:y\nDTSTART:20260601T140000Z\nDTEND:20260601T150000Z\n";
        var act = () => IcsAdapter.Parse(bad);
        act.Should().Throw<FormatException>().WithMessage("*inside a VEVENT*");
    }

    [Fact]
    public void Parse_MalformedDateTime_Throws()
    {
        var bad = """
            BEGIN:VEVENT
            UID:x
            SUMMARY:y
            DTSTART:not-a-date
            DTEND:20260601T150000Z
            END:VEVENT
            """;
        var act = () => IcsAdapter.Parse(bad);
        act.Should().Throw<FormatException>().WithMessage("*not-a-date*");
    }
}
