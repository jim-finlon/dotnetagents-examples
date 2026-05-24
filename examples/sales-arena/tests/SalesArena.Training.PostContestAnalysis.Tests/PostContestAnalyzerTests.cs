using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SalesArena.Training.PostContestAnalysis;
using Xunit;

namespace SalesArena.Training.PostContestAnalysis.Tests;

public class PostContestAnalyzerTests
{
    private static ContestEventEntry Touch(string persona) => new("c1", persona, "lead-touched");
    private static ContestEventEntry Win(string persona) => new("c1", persona, "deal-won");
    private static ContestEventEntry Loss(string persona) => new("c1", persona, "deal-lost");
    private static ContestEventEntry Obj(string persona, string topic) =>
        new("c1", persona, "objection-raised", topic);

    [Fact]
    public async Task EmptyLedger_EmitsDiagnostic()
    {
        var ledger = new InMemoryContestLedger(System.Array.Empty<ContestEventEntry>());
        var result = await new PostContestAnalyzer().AnalyzeAsync(ledger, "c1");
        result.Suggestions.Should().BeEmpty();
        result.Diagnostics.Should().ContainSingle().Which.Should().Contain("no events");
    }

    [Fact]
    public async Task LowWinRate_NoObjections_SuggestsTightenQualification()
    {
        var events = new List<ContestEventEntry> { Touch("moss") };
        events.AddRange(Enumerable.Repeat(Loss("moss"), 5));
        events.Add(Win("moss"));
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        var s = result.Suggestions.Single(s => s.PersonaId == "moss");
        s.SuggestionKind.Should().Be("tighten-qualification");
    }

    [Fact]
    public async Task LowWinRate_WithPriceDominantObjections_SuggestsCalibrationValue()
    {
        var events = new List<ContestEventEntry> { Touch("moss") };
        events.AddRange(Enumerable.Repeat(Loss("moss"), 5));
        events.AddRange(Enumerable.Repeat(Obj("moss", "price"), 4));
        events.Add(Obj("moss", "timing"));
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        var s = result.Suggestions.Single(s => s.PersonaId == "moss");
        s.SuggestionKind.Should().Be("lead-with-calibration-value");
    }

    [Fact]
    public async Task HighWinRate_SuggestsSoftenAndExport()
    {
        var events = Enumerable.Repeat(Win("roma"), 7)
            .Concat(Enumerable.Repeat(Loss("roma"), 2))
            .ToList();
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        var s = result.Suggestions.Single(s => s.PersonaId == "roma");
        s.SuggestionKind.Should().Be("soften-tone");
    }

    [Fact]
    public async Task PriceObjectionsButNoCloses_StillSuggestsCalibrationValue()
    {
        var events = new List<ContestEventEntry>
        {
            Touch("levene"),
            Obj("levene", "price"), Obj("levene", "price"), Obj("levene", "price"),
            Obj("levene", "timing"),
        };
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        var s = result.Suggestions.Single(s => s.PersonaId == "levene");
        s.SuggestionKind.Should().Be("lead-with-calibration-value");
    }

    [Fact]
    public async Task BelowMinDecisions_EmitsNoChange()
    {
        var events = new List<ContestEventEntry> { Touch("aaronow"), Win("aaronow"), Loss("aaronow") };
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        var s = result.Suggestions.Single(s => s.PersonaId == "aaronow");
        s.SuggestionKind.Should().Be("no-change");
        s.Reason.Should().Contain("below minimum");
    }

    [Fact]
    public async Task MidBandWinRate_NoObjectionDominance_EmitsNoChange()
    {
        var events = Enumerable.Repeat(Win("williamson"), 3)
            .Concat(Enumerable.Repeat(Loss("williamson"), 3))
            .Concat(new[] { Obj("williamson", "timing"), Obj("williamson", "scope") })
            .ToList();
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        var s = result.Suggestions.Single(s => s.PersonaId == "williamson");
        s.SuggestionKind.Should().Be("no-change");
    }

    [Fact]
    public async Task MultiPersona_SuggestionsOrderedOrdinallyByPersonaId()
    {
        var events = new List<ContestEventEntry>
        {
            Win("moss"), Win("moss"), Win("moss"), Win("moss"), Win("moss"), Loss("moss"), Loss("moss"),
            Win("aaronow"), Win("aaronow"), Win("aaronow"), Win("aaronow"), Win("aaronow"), Loss("aaronow"),
            Win("roma"), Win("roma"), Win("roma"), Win("roma"), Win("roma"),
        };
        var result = await new PostContestAnalyzer().AnalyzeAsync(new InMemoryContestLedger(events), "c1");

        result.Suggestions.Select(s => s.PersonaId)
            .Should().Equal(new[] { "aaronow", "moss", "roma" });
    }

    [Fact]
    public async Task BlankContestId_Throws()
    {
        var ledger = new InMemoryContestLedger(System.Array.Empty<ContestEventEntry>());
        Func<Task> act = () => new PostContestAnalyzer().AnalyzeAsync(ledger, "   ");
        await act.Should().ThrowAsync<System.ArgumentException>();
    }

    [Fact]
    public async Task UnknownContestId_EmptyResultWithDiagnostic()
    {
        var ledger = new InMemoryContestLedger(new[] { Touch("moss") });
        var result = await new PostContestAnalyzer().AnalyzeAsync(ledger, "other-contest");
        result.Suggestions.Should().BeEmpty();
        result.Diagnostics.Should().NotBeEmpty();
    }
}
