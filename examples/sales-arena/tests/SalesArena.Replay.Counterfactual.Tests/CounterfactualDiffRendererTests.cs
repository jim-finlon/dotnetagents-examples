using FluentAssertions;
using SalesArena.Replay.Counterfactual;
using Xunit;

namespace SalesArena.Replay.Counterfactual.Tests;

public sealed class CounterfactualDiffRendererTests
{
    private static CounterfactualResult BuildResult(bool noOp)
    {
        var origPersonas = new[]
        {
            new PersonaOutcome("roma", 50, 5, 3, 1, 30000m, 1),
            new PersonaOutcome("levene", 70, 4, 2, 2, 18000m, 2),
        };
        var cfPersonas = noOp
            ? origPersonas
            : new[]
            {
                new PersonaOutcome("roma", 55, 6, 4, 1, 42000m, 1),
                new PersonaOutcome("levene", 65, 3, 1, 3, 12000m, 2),
            };
        var original = new ContestOutcome("c-1", origPersonas);
        var counterfactual = new ContestOutcome("c-1-cf", cfPersonas);
        var diff = CounterfactualRunner.ComputeDiff(original, counterfactual);
        return new CounterfactualResult(
            "c-1",
            new SwapOutreachTemplatesMutation("roma", "levene"),
            original, counterfactual, diff);
    }

    [Fact]
    public void RenderMarkdown_includes_side_by_side_table_with_both_personas()
    {
        var md = CounterfactualDiffRenderer.RenderMarkdown(BuildResult(noOp: false));
        md.Should().Contain("Counterfactual — SwapOutreachTemplates");
        md.Should().Contain("| Persona | Metric | Original | Counterfactual | Δ |");
        md.Should().Contain("| roma | Touches | 50 | 55 | +5 |");
        md.Should().Contain("| levene | Touches | 70 | 65 | -5 |");
    }

    [Fact]
    public void RenderMarkdown_formats_zero_delta_as_zero_not_plus_zero()
    {
        var md = CounterfactualDiffRenderer.RenderMarkdown(BuildResult(noOp: true));
        md.Should().Contain("**No-op:**");
        md.Should().NotContain("+0");
    }

    [Fact]
    public void RenderMarkdown_marks_no_op_results()
    {
        CounterfactualDiffRenderer.RenderMarkdown(BuildResult(noOp: true))
            .Should().Contain("**No-op:**");
        CounterfactualDiffRenderer.RenderMarkdown(BuildResult(noOp: false))
            .Should().NotContain("**No-op:**");
    }

    [Fact]
    public void RenderMarkdown_null_throws()
    {
        Action act = () => CounterfactualDiffRenderer.RenderMarkdown(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
