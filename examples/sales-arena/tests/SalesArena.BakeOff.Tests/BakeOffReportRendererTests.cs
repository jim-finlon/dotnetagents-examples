using FluentAssertions;
using SalesArena.BakeOff;
using SalesArena.Replay.Counterfactual;
using Xunit;

namespace SalesArena.BakeOff.Tests;

public sealed class BakeOffReportRendererTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static BakeOffResult Build(bool confidential = false)
    {
        var perPersona = new[]
        {
            new PersonaBakeOffResult(
                Persona: "roma",
                WithProductA: new PersonaOutcome("roma", 50, 5, 3, 1, 30000m, 1),
                WithProductB: new PersonaOutcome("roma", 40, 4, 2, 2, 18000m, 2),
                PreferredProduct: "ProductA",
                RevenueDeltaUsd: 12000m),
            new PersonaBakeOffResult(
                Persona: "levene",
                WithProductA: new PersonaOutcome("levene", 70, 4, 1, 3, 12000m, 3),
                WithProductB: new PersonaOutcome("levene", 65, 5, 2, 1, 28000m, 1),
                PreferredProduct: "ProductB",
                RevenueDeltaUsd: -16000m),
        };

        var aggregate = new BakeOffAggregate(
            PersonasPreferringA: 1,
            PersonasPreferringB: 1,
            PersonasTied: 0,
            TotalRevenueDeltaUsd: -4000m,
            OverallVerdict: null);

        return new BakeOffResult(
            BakeOffId: "abc123",
            ProductA: new ProductProfile("ProductA", "starter", new[] { "auth" }, "smb saas", ContainsConfidentialData: confidential),
            ProductB: new ProductProfile("ProductB", "enterprise", new[] { "scim" }, "fortune-500"),
            Seed: 42,
            LeadPoolSize: 200,
            CompletedAtUtc: _t0,
            PerPersona: perPersona,
            Aggregate: aggregate);
    }

    [Fact]
    public void RenderMarkdown_includes_disclaimer_header()
    {
        var md = BakeOffReportRenderer.RenderMarkdown(Build());
        md.Should().Contain("Simulation-only");
        md.Should().Contain("real evaluation");
    }

    [Fact]
    public void RenderMarkdown_includes_per_persona_table_rows()
    {
        var md = BakeOffReportRenderer.RenderMarkdown(Build());
        md.Should().Contain("| roma |");
        md.Should().Contain("30000");
        md.Should().Contain("+12000");
        md.Should().Contain("ProductA");
        md.Should().Contain("| levene |");
        md.Should().Contain("-16000");
        md.Should().Contain("ProductB");
    }

    [Fact]
    public void RenderMarkdown_shows_tied_aggregate_when_verdict_is_null()
    {
        var md = BakeOffReportRenderer.RenderMarkdown(Build());
        md.Should().Contain("tied at the aggregate level");
    }

    [Fact]
    public void RenderMarkdown_includes_confidential_badge_when_either_product_flagged()
    {
        var md = BakeOffReportRenderer.RenderMarkdown(Build(confidential: true));
        md.Should().Contain("Confidential data");
        md.Should().Contain("Do not share");
    }

    [Fact]
    public void RenderMarkdown_omits_confidential_badge_when_neither_flagged()
    {
        var md = BakeOffReportRenderer.RenderMarkdown(Build(confidential: false));
        md.Should().NotContain("Confidential data");
    }

    [Fact]
    public void RenderMarkdown_null_throws()
    {
        Action act = () => BakeOffReportRenderer.RenderMarkdown(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
