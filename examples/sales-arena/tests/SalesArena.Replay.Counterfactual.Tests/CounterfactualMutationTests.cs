using FluentAssertions;
using SalesArena.Replay.Counterfactual;
using Xunit;

namespace SalesArena.Replay.Counterfactual.Tests;

public sealed class CounterfactualMutationTests
{
    private static readonly IReadOnlyList<PersonaConfig> _base = new[]
    {
        new PersonaConfig("roma", "templates/roma", "local-strong", "cadence/roma"),
        new PersonaConfig("levene", "templates/levene", "local-light", "cadence/levene"),
        new PersonaConfig("moss", "templates/moss", "local-strong", "cadence/moss"),
    };

    [Fact]
    public void SwapOutreachTemplates_copies_from_to_target_persona()
    {
        var m = new SwapOutreachTemplatesMutation("roma", "levene");
        var mutated = m.Apply(_base);

        mutated.Single(p => p.Persona == "levene").OutreachTemplatesRef.Should().Be("templates/roma");
        // From-persona is unchanged.
        mutated.Single(p => p.Persona == "roma").OutreachTemplatesRef.Should().Be("templates/roma");
        // Third persona unchanged.
        mutated.Single(p => p.Persona == "moss").OutreachTemplatesRef.Should().Be("templates/moss");
    }

    [Fact]
    public void SwapOutreachTemplates_unknown_FromPersona_throws()
    {
        var m = new SwapOutreachTemplatesMutation("ghost", "levene");
        Action act = () => m.Apply(_base);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SwapModelTier_updates_only_target_persona()
    {
        var m = new SwapModelTierMutation("roma", "frontier-fallback");
        var mutated = m.Apply(_base);

        mutated.Single(p => p.Persona == "roma").ModelTier.Should().Be("frontier-fallback");
        mutated.Single(p => p.Persona == "levene").ModelTier.Should().Be("local-light");
    }

    [Fact]
    public void SwapCadence_updates_only_target_persona()
    {
        var m = new SwapCadenceMutation("moss", "cadence/sprint-week");
        var mutated = m.Apply(_base);

        mutated.Single(p => p.Persona == "moss").CadenceRef.Should().Be("cadence/sprint-week");
        mutated.Single(p => p.Persona == "roma").CadenceRef.Should().Be("cadence/roma");
    }

    [Fact]
    public void IsNoOp_true_when_mutation_changes_nothing()
    {
        // Swap roma → roma → effectively no-op.
        var sameTemplates = new SwapOutreachTemplatesMutation("roma", "roma");
        sameTemplates.IsNoOp(_base).Should().BeTrue();

        // Swap roma's tier to its current tier.
        var sameTier = new SwapModelTierMutation("roma", "local-strong");
        sameTier.IsNoOp(_base).Should().BeTrue();
    }

    [Fact]
    public void IsNoOp_false_when_mutation_changes_a_field()
    {
        new SwapModelTierMutation("roma", "frontier-fallback").IsNoOp(_base).Should().BeFalse();
        new SwapOutreachTemplatesMutation("roma", "levene").IsNoOp(_base).Should().BeFalse();
    }

    [Fact]
    public void Apply_returns_a_new_list_without_mutating_original()
    {
        var m = new SwapCadenceMutation("roma", "cadence/x");
        var mutated = m.Apply(_base);

        mutated.Should().NotBeSameAs(_base);
        _base.Single(p => p.Persona == "roma").CadenceRef.Should().Be("cadence/roma",
            "the input list must be untouched (record-with semantics)");
    }
}
