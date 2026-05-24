using FluentAssertions;
using SalesArena.Replay.Counterfactual;
using Xunit;

namespace SalesArena.Replay.Counterfactual.Tests;

public sealed class CounterfactualRunnerTests
{
    private static readonly IReadOnlyList<PersonaConfig> _personas = new[]
    {
        new PersonaConfig("roma", "templates/roma", "local-strong", "cadence/roma"),
        new PersonaConfig("levene", "templates/levene", "local-light", "cadence/levene"),
        new PersonaConfig("moss", "templates/moss", "local-strong", "cadence/moss"),
    };

    private static readonly CounterfactualRunOptions _opts = new(LeadPoolSize: 200, Seed: 42);

    [Fact]
    public void Run_returns_original_and_counterfactual_outcomes_with_per_persona_diff()
    {
        var runner = new CounterfactualRunner(new DeterministicHashSimulator());
        var mutation = new SwapOutreachTemplatesMutation("roma", "levene");

        var result = runner.Run("contest-A", _personas, mutation, _opts);

        result.OriginalContestId.Should().Be("contest-A");
        result.Counterfactual.ContestId.Should().Be("contest-A-cf");
        result.Original.Personas.Select(p => p.Persona)
            .Should().BeEquivalentTo(new[] { "roma", "levene", "moss" });
        result.Diff.Personas.Should().HaveCount(3);
    }

    [Fact]
    public void Run_is_deterministic_same_input_yields_same_outcome()
    {
        var runner = new CounterfactualRunner(new DeterministicHashSimulator());
        var mutation = new SwapModelTierMutation("levene", "frontier-fallback");

        var first = runner.Run("contest-A", _personas, mutation, _opts);
        var second = runner.Run("contest-A", _personas, mutation, _opts);

        first.Original.Should().BeEquivalentTo(second.Original);
        first.Counterfactual.Should().BeEquivalentTo(second.Counterfactual);
        first.Diff.Should().BeEquivalentTo(second.Diff);
    }

    [Fact]
    public void Run_no_op_mutation_produces_zero_delta_diff()
    {
        var runner = new CounterfactualRunner(new DeterministicHashSimulator());
        // Roma's current tier is local-strong; mutate to local-strong = no-op.
        var mutation = new SwapModelTierMutation("roma", "local-strong");

        var result = runner.Run("contest-A", _personas, mutation, _opts);
        result.Diff.IsZero.Should().BeTrue(
            "a mutation that changes no persona-config field must produce a zero-delta diff");
    }

    [Fact]
    public void Run_seed_change_yields_different_outcome()
    {
        var runner = new CounterfactualRunner(new DeterministicHashSimulator());
        var mutation = new SwapCadenceMutation("moss", "cadence/sprint-week");

        var seedA = runner.Run("contest-A", _personas, mutation, _opts);
        var seedB = runner.Run("contest-A", _personas, mutation, _opts with { Seed = 9999 });

        // Same mutation, different seed — the original-run outcomes themselves
        // should differ, proving the simulator honored the seed.
        seedA.Original.Should().NotBeEquivalentTo(seedB.Original);
    }

    [Fact]
    public void Run_actual_mutation_changes_at_least_one_persona_delta()
    {
        var runner = new CounterfactualRunner(new DeterministicHashSimulator());
        var mutation = new SwapOutreachTemplatesMutation("roma", "levene");

        var result = runner.Run("contest-A", _personas, mutation, _opts);
        result.Diff.IsZero.Should().BeFalse(
            "swapping templates onto a different persona must produce some delta");
        // Only the target (levene) gets the new templates; her hash key changes;
        // her outcome should differ.
        var levene = result.Diff.Personas.Single(d => d.Persona == "levene");
        (levene.TouchesDelta != 0 || levene.RevenueDeltaUsd != 0m).Should().BeTrue();
    }

    [Fact]
    public void Run_null_arguments_throw()
    {
        var runner = new CounterfactualRunner(new DeterministicHashSimulator());
        var mutation = new SwapCadenceMutation("roma", "x");
        Action a = () => runner.Run(null!, _personas, mutation, _opts);
        Action b = () => runner.Run("c", null!, mutation, _opts);
        Action c = () => runner.Run("c", _personas, null!, _opts);
        Action d = () => runner.Run("c", _personas, mutation, null!);
        a.Should().Throw<ArgumentException>();
        b.Should().Throw<ArgumentNullException>();
        c.Should().Throw<ArgumentNullException>();
        d.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_rejects_null_simulator()
    {
        Action act = () => _ = new CounterfactualRunner(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Final_position_in_outcome_is_a_valid_ranking()
    {
        var sim = new DeterministicHashSimulator();
        var outcome = sim.Simulate("c", _personas, leadPoolSize: 100, seed: 42);
        var positions = outcome.Personas.Select(p => p.FinalPosition).OrderBy(x => x).ToArray();
        positions.Should().BeEquivalentTo(new[] { 1, 2, 3 }, opts => opts.WithStrictOrdering());
    }
}
