using FluentAssertions;
using SalesArena.Replay.Narrative;
using Xunit;

namespace SalesArena.Replay.Narrative.Tests;

/// <summary>
/// Story d7bcad55 (SA-04-04). Stub-LLM-driven tests for the narrative rewriter:
/// citation density, hallucination guard, refusal paths.
/// </summary>
public sealed class NarrativeRewriterTests
{
    [Fact]
    public async Task Refuses_empty_report()
    {
        var rewriter = new NarrativeRewriter(new StubLlm(_ => "ignored"));
        var result = await rewriter.RewriteAsync("", new[] { Evt("evt-1") });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("MISSING_REPORT");
    }

    [Fact]
    public async Task Refuses_empty_ledger()
    {
        var rewriter = new NarrativeRewriter(new StubLlm(_ => "Hour 7 [evt-1]"));
        var result = await rewriter.RewriteAsync("# Report", Array.Empty<LedgerEvent>());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("MISSING_LEDGER");
    }

    [Fact]
    public async Task Refuses_empty_LLM_response()
    {
        var rewriter = new NarrativeRewriter(new StubLlm(_ => "   "));
        var result = await rewriter.RewriteAsync("# Report", new[] { Evt("evt-1") });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("EMPTY_LLM_RESPONSE");
    }

    [Fact]
    public async Task Happy_path_meets_citation_density_one_per_paragraph()
    {
        // Stub returns 3 paragraphs each carrying a citation to an existing event.
        var prose = """
            Hour 1: Roma opened the contest with a steady cold-call cadence [evt-1].

            Hour 4: Levene flooded inboxes with 47 emails [evt-2] before lunch hit.

            Hour 7: Moss reverted to phone-first follow-ups on the warm three [evt-3].
            """;
        var ledger = new[] { Evt("evt-1", "Roma"), Evt("evt-2", "Levene"), Evt("evt-3", "Moss") };
        var rewriter = new NarrativeRewriter(new StubLlm(_ => prose));

        var result = await rewriter.RewriteAsync("# Report", ledger);

        result.Success.Should().BeTrue();
        result.Report.Should().NotBeNull();
        result.Report!.Citations.Should().HaveCount(3);
        result.Report.Citations.Select(c => c.EventId).Should().BeEquivalentTo(new[] { "evt-1", "evt-2", "evt-3" });
        // Citation density: at least one per paragraph.
        var paragraphCount = HallucinationGuard.SplitParagraphs(result.Report.Prose).Count;
        result.Report.Citations.Count.Should().BeGreaterOrEqualTo(paragraphCount);
    }

    [Fact]
    public async Task Hallucination_guard_fires_when_LLM_cites_unknown_event_id()
    {
        var prose = "Hour 1: Roma opened the contest with grace [evt-99-NOT-IN-LEDGER].";
        var ledger = new[] { Evt("evt-1", "Roma") };
        var rewriter = new NarrativeRewriter(new StubLlm(_ => prose));

        var result = await rewriter.RewriteAsync("# Report", ledger);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("HALLUCINATION_GUARD_FAILED");
        result.HallucinationFindings.Should().Contain(f => f.Contains("UNKNOWN_EVENT_ID"));
        result.HallucinationFindings.Should().Contain(f => f.Contains("evt-99-NOT-IN-LEDGER"));
    }

    [Fact]
    public async Task Hallucination_guard_fires_when_paragraph_has_no_citation()
    {
        var prose = """
            Hour 1: Roma opened the contest with a steady cold-call cadence [evt-1].

            This paragraph invents drama without citing anything.
            """;
        var ledger = new[] { Evt("evt-1", "Roma") };
        var rewriter = new NarrativeRewriter(new StubLlm(_ => prose));

        var result = await rewriter.RewriteAsync("# Report", ledger);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("HALLUCINATION_GUARD_FAILED");
        result.HallucinationFindings.Should().Contain(f => f.Contains("NO_CITATION"));
    }

    [Fact]
    public async Task Hallucination_guard_fires_when_LLM_invents_a_persona_name()
    {
        var prose = "Hour 5: Khaleesi swooped in with a four-paragraph cold email [evt-1].";
        var ledger = new[] { Evt("evt-1", "Roma") };
        var rewriter = new NarrativeRewriter(new StubLlm(_ => prose));

        var result = await rewriter.RewriteAsync("# Report", ledger);

        result.Success.Should().BeFalse();
        result.HallucinationFindings.Should().Contain(f => f.Contains("UNKNOWN_PERSONA") && f.Contains("Khaleesi"));
    }

    [Fact]
    public async Task Guard_allows_baseline_persona_names_even_when_not_in_ledger_subset()
    {
        // Ledger only has Roma's event; prose mentions Moss (one of the SA-05-01 baseline names).
        // Guard should not flag baseline personas — they're operator-known canonical entities.
        var prose = """
            Hour 1: Roma scored the first reply with a one-sentence cold open [evt-1].

            Hour 7: Moss countered with phone-first follow-ups [evt-1].
            """;
        var ledger = new[] { Evt("evt-1", "Roma") };
        var rewriter = new NarrativeRewriter(new StubLlm(_ => prose));

        var result = await rewriter.RewriteAsync("# Report", ledger);

        result.Success.Should().BeTrue($"Moss is a baseline persona; got findings: {string.Join("; ", result.HallucinationFindings)}");
    }

    [Fact]
    public async Task Result_passes_prompt_and_report_and_ledger_into_LLM_adapter()
    {
        string? capturedPrompt = null;
        string? capturedReport = null;
        IReadOnlyList<LedgerEvent>? capturedLedger = null;
        var rewriter = new NarrativeRewriter(new CapturingLlm(
            (prompt, report, ledger) =>
            {
                capturedPrompt = prompt;
                capturedReport = report;
                capturedLedger = ledger;
                return "Hour 1 [evt-1]";
            }));

        await rewriter.RewriteAsync("# Structured Report Markdown", new[] { Evt("evt-1") });

        capturedPrompt.Should().Contain("citation");
        capturedReport.Should().Be("# Structured Report Markdown");
        capturedLedger.Should().HaveCount(1);
    }

    [Fact]
    public void Citation_regex_matches_event_id_shapes_from_the_prompt_contract()
    {
        var prose = "First [evt-1] then [event-2] then [evt-with.dots_and-dashes].";
        var matches = HallucinationGuard.CitationPattern.Matches(prose);

        matches.Should().HaveCount(3);
        matches[0].Groups[1].Value.Should().Be("evt-1");
        matches[1].Groups[1].Value.Should().Be("event-2");
        matches[2].Groups[1].Value.Should().Be("evt-with.dots_and-dashes");
    }

    private static LedgerEvent Evt(string id, string? persona = null) => new(
        EventId: id,
        OccurredAtUtc: new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
        Kind: "touch.email",
        Summary: $"event {id}",
        Persona: persona);

    private sealed class StubLlm : INarrativeLlmAdapter
    {
        private readonly Func<IReadOnlyList<LedgerEvent>, string> _fn;
        public StubLlm(Func<IReadOnlyList<LedgerEvent>, string> fn) => _fn = fn;
        public Task<string> RewriteAsync(string prompt, string reportMarkdown, IReadOnlyList<LedgerEvent> ledger, CancellationToken cancellationToken = default)
            => Task.FromResult(_fn(ledger));
    }

    private sealed class CapturingLlm : INarrativeLlmAdapter
    {
        private readonly Func<string, string, IReadOnlyList<LedgerEvent>, string> _fn;
        public CapturingLlm(Func<string, string, IReadOnlyList<LedgerEvent>, string> fn) => _fn = fn;
        public Task<string> RewriteAsync(string prompt, string reportMarkdown, IReadOnlyList<LedgerEvent> ledger, CancellationToken cancellationToken = default)
            => Task.FromResult(_fn(prompt, reportMarkdown, ledger));
    }
}
