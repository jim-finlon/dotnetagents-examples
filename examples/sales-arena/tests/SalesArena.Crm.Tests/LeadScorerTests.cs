using FluentAssertions;
using SalesArena.Crm;
using SalesArena.Crm.Scoring;
using Xunit;

namespace SalesArena.Crm.Tests;

public sealed class LeadScorerTests
{
    private static readonly IcpProfile DefaultIcp = new(
        Name: "B2B SaaS mid-market",
        TargetIndustries: ["SaaS", "Software"],
        TargetRegions: ["US"],
        MinHeadcount: 50,
        MaxHeadcount: 5000);

    [Fact]
    public async Task Persona_weights_produce_different_composites_for_same_subscores()
    {
        var scorer = BuildScorer();
        var lead = SampleLead();

        var roma = await scorer.ScoreAsync(lead, DefaultIcp, "roma");
        var levene = await scorer.ScoreAsync(lead, DefaultIcp, "levene");
        var moss = await scorer.ScoreAsync(lead, DefaultIcp, "moss");

        roma.Fit.Should().Be(levene.Fit);
        new[] { roma.Composite, levene.Composite, moss.Composite }.Should().OnlyHaveUniqueItems();
        moss.Composite.Should().BeGreaterThan(levene.Composite);
        roma.Rationale.Should().Contain(r => r.Contains("Composite", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("high", 88)]
    [InlineData("low", 35)]
    public async Task Stub_model_maps_intent_signal(string signal, int expectedIntent)
    {
        var scorer = BuildScorer();
        var lead = SampleLead();
        lead.Metadata["intent_signal"] = signal;

        var score = await scorer.ScoreAsync(lead, DefaultIcp, "roma");
        score.Intent.Should().Be(expectedIntent);
    }

    [Fact]
    public void Weight_catalog_loads_three_persona_yaml_files()
    {
        var catalog = PersonaWeightCatalog.LoadFromDirectory(WeightsDirectory());
        catalog.GetWeights("roma").Fit.Should().Be(0.50);
        catalog.GetWeights("levene").Intent.Should().Be(0.50);
        catalog.GetWeights("moss").Power.Should().Be(0.60);
    }

    private static LocalLlmLeadScorer BuildScorer()
    {
        var rubric = Path.Combine(ScoringDirectory(), "rubric.prompt.md");
        var weights = PersonaWeightCatalog.LoadFromDirectory(WeightsDirectory());
        return new LocalLlmLeadScorer(new StubLeadScoreModel(), weights, rubric);
    }

    private static string ScoringDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Scoring"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "agents", "crm", "Scoring"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agents", "crm", "Scoring"),
        };

        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (File.Exists(Path.Combine(resolved, "rubric.prompt.md")))
            {
                return resolved;
            }
        }

        throw new DirectoryNotFoundException("Could not locate CRM Scoring directory.");
    }

    private static string WeightsDirectory() => Path.Combine(ScoringDirectory(), "weights");

    private static CrmRecord SampleLead() =>
        new()
        {
            LeadId = "L-9001",
            Stage = CrmStages.Lead,
            Persona = "roma",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["industry"] = "SaaS",
                ["intent_signal"] = "medium",
                ["contact_role"] = "Director of Sales",
            },
        };
}
