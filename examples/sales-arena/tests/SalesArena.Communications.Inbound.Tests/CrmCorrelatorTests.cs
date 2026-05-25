using FluentAssertions;
using SalesArena.Communications.Inbound;
using Xunit;

namespace SalesArena.Communications.Inbound.Tests;

public sealed class CrmCorrelatorTests
{
    private static readonly IReadOnlyList<CrmProspectIndexEntry> Index =
        CrmProspectIndexLoader.LoadFromJson(Path.Combine(InboundTestHost.FixtureRoot, "crm-prospects.json"));

    [Fact]
    public void Correlator_matches_email_hit()
    {
        var correlator = new CrmCorrelator(Index);
        var message = new InboundMessage(
            "m1",
            "email",
            "em-1",
            "Interested",
            "Jordan Lee",
            "jordan.lee@acmelabs.example",
            "Acme Labs",
            DateTimeOffset.UtcNow);

        var result = correlator.Correlate(message);
        result.Status.Should().Be(CrmCorrelationStatus.Matched);
        result.LeadId.Should().Be("L-1001");
    }

    [Fact]
    public void Correlator_misses_unknown_sender()
    {
        var correlator = new CrmCorrelator(Index);
        var message = new InboundMessage(
            "m2",
            "sms",
            "sms-9",
            "Hello",
            "Unknown Person",
            "nobody@nowhere.example",
            "Mystery Inc",
            DateTimeOffset.UtcNow);

        correlator.Correlate(message).Status.Should().Be(CrmCorrelationStatus.Miss);
    }

    [Fact]
    public void Correlator_flags_ambiguous_company_only_match()
    {
        var correlator = new CrmCorrelator(
        [
            new CrmProspectIndexEntry("L-A", "A", "One", "SharedCo", null),
            new CrmProspectIndexEntry("L-B", "B", "Two", "SharedCo", null),
        ]);

        var message = new InboundMessage(
            "m3",
            "chat",
            "c-1",
            "Question?",
            "Mystery",
            null,
            "SharedCo",
            DateTimeOffset.UtcNow);

        var result = correlator.Correlate(message);
        result.Status.Should().Be(CrmCorrelationStatus.Ambiguous);
        result.CandidateLeadIds.Should().HaveCount(2);
    }
}
