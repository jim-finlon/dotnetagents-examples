using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Training.Diary;
using Xunit;

namespace SalesArena.Training.Tests;

public sealed class DiaryHallucinationGuardTests
{
    private const string Contest = "diary-test";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Guard_passes_text_with_two_distinct_real_citations()
    {
        var events = new[] { NewEvent(1), NewEvent(2) };
        var body = "Wrote down [evt:1] and [evt:2] as today's signals.";

        var r = DiaryHallucinationGuard.Verify(body, events);

        r.IsOk.Should().BeTrue();
        r.CitedOrOffending.Should().BeEquivalentTo(new[] { "1", "2" });
    }

    [Fact]
    public void Guard_rejects_zero_citation_body()
    {
        var events = new[] { NewEvent(1) };
        var r = DiaryHallucinationGuard.Verify("No specifics here.", events);

        r.IsOk.Should().BeFalse();
        r.Reason.Should().Be("no citations found");
    }

    [Fact]
    public void Guard_rejects_made_up_event_ids()
    {
        var events = new[] { NewEvent(1) };
        var r = DiaryHallucinationGuard.Verify("Cited [evt:1] and [evt:99].", events);

        r.IsOk.Should().BeFalse();
        r.Reason.Should().Be("hallucinated event ids");
        r.CitedOrOffending.Should().BeEquivalentTo(new[] { "99" });
    }

    [Fact]
    public void Guard_rejects_one_citation_when_min_is_two()
    {
        var events = new[] { NewEvent(1), NewEvent(2) };
        var r = DiaryHallucinationGuard.Verify("Wrote [evt:1] but only that one.", events, minCitations: 2);

        r.IsOk.Should().BeFalse();
        r.Reason.Should().Contain("insufficient citations");
    }

    [Fact]
    public void Guard_counts_distinct_citations_only()
    {
        var events = new[] { NewEvent(1) };
        // Same id cited twice → counts as 1 distinct
        var r = DiaryHallucinationGuard.Verify("Wrote [evt:1] and again [evt:1].", events, minCitations: 2);

        r.IsOk.Should().BeFalse();
        r.Reason.Should().Contain("insufficient");
    }

    [Fact]
    public void Guard_allows_none_sentinel_only_when_events_are_empty()
    {
        var noEvents = Array.Empty<ArenaEvent>();
        DiaryHallucinationGuard.Verify("[evt:none] [evt:none]", noEvents, minCitations: 2).IsOk.Should().BeTrue();

        // With real events available, the none sentinel doesn't count toward the min.
        var events = new[] { NewEvent(1) };
        DiaryHallucinationGuard.Verify("[evt:none] but only one [evt:1].", events, minCitations: 2)
            .IsOk.Should().BeFalse();
    }

    private static ArenaEvent NewEvent(long id) => new()
    {
        Id = id,
        ContestId = Contest,
        Kind = ArenaEventKinds.DealClosed,
        OccurredAtUtc = T0,
        LeadId = $"L-{id}",
        Persona = "test",
        PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload($"L-{id}", "test", "Won", 1m, null)),
    };
}
