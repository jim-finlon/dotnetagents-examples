using FluentAssertions;
using SalesArena.BakeOff;
using SalesArena.Replay.Counterfactual;
using Xunit;

namespace SalesArena.BakeOff.Tests;

public sealed class VendorBakeOffEngineTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<PersonaConfig> _personas = new[]
    {
        new PersonaConfig("roma", "templates/roma", "local-strong", "cadence/roma"),
        new PersonaConfig("levene", "templates/levene", "local-light", "cadence/levene"),
        new PersonaConfig("moss", "templates/moss", "local-strong", "cadence/moss"),
    };

    private static ProductProfile ProductA => new(
        Name: "ProductA",
        PricingTier: "starter",
        Features: new[] { "auth", "audit-log", "sso" },
        IdealCustomerProfile: "20-200 seat SaaS",
        ContainsConfidentialData: false);

    private static ProductProfile ProductB => new(
        Name: "ProductB",
        PricingTier: "enterprise",
        Features: new[] { "auth", "scim", "rbac", "data-residency" },
        IdealCustomerProfile: "Fortune-500 IT",
        ContainsConfidentialData: false);

    [Fact]
    public void Run_returns_per_persona_results_for_every_persona()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var result = engine.Run(ProductA, ProductB, _personas, leadPoolSize: 200, seed: 42, _t0);

        result.PerPersona.Select(p => p.Persona).Should().BeEquivalentTo(new[] { "roma", "levene", "moss" });
        result.ProductA.Name.Should().Be("ProductA");
        result.ProductB.Name.Should().Be("ProductB");
        result.Seed.Should().Be(42);
        result.LeadPoolSize.Should().Be(200);
        result.CompletedAtUtc.Should().Be(_t0);
    }

    [Fact]
    public void Run_is_deterministic_same_input_yields_same_per_persona_outcomes()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var first = engine.Run(ProductA, ProductB, _personas, 200, 42, _t0);
        var second = engine.Run(ProductA, ProductB, _personas, 200, 42, _t0);

        // BakeOffId is random per call (Guid.NewGuid) — exclude it from the comparison.
        first.PerPersona.Should().BeEquivalentTo(second.PerPersona);
        first.Aggregate.Should().BeEquivalentTo(second.Aggregate);
    }

    [Fact]
    public void Run_actually_differentiates_products_at_least_one_persona_has_a_winner()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var result = engine.Run(ProductA, ProductB, _personas, 200, 42, _t0);

        result.PerPersona.Should().Contain(p => p.PreferredProduct != null,
            "encoding product identity into the templates ref should differentiate outcomes");
    }

    [Fact]
    public void Run_same_product_for_A_and_B_throws_SameProductBakeOff()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        Action act = () => engine.Run(ProductA, ProductA, _personas, 200, 42, _t0);
        act.Should().Throw<BakeOffException>().Which.Code.Should().Be(BakeOffErrorCode.SameProductBakeOff);
    }

    [Fact]
    public void Run_empty_product_name_throws()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var empty = ProductA with { Name = "" };
        Action act = () => engine.Run(empty, ProductB, _personas, 200, 42, _t0);
        act.Should().Throw<BakeOffException>().Which.Code.Should().Be(BakeOffErrorCode.EmptyProductName);
    }

    [Fact]
    public void Run_empty_icp_throws()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var noIcp = ProductA with { IdealCustomerProfile = "" };
        Action act = () => engine.Run(noIcp, ProductB, _personas, 200, 42, _t0);
        act.Should().Throw<BakeOffException>().Which.Code.Should().Be(BakeOffErrorCode.EmptyIdealCustomerProfile);
    }

    [Fact]
    public void Run_empty_persona_list_throws()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        Action act = () => engine.Run(ProductA, ProductB, Array.Empty<PersonaConfig>(), 200, 42, _t0);
        act.Should().Throw<BakeOffException>().Which.Code.Should().Be(BakeOffErrorCode.EmptyPersonaList);
    }

    [Fact]
    public void Run_aggregate_verdict_picks_higher_count_or_null_on_tie()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var result = engine.Run(ProductA, ProductB, _personas, 200, 42, _t0);

        var totalAccountedFor = result.Aggregate.PersonasPreferringA
            + result.Aggregate.PersonasPreferringB
            + result.Aggregate.PersonasTied;
        totalAccountedFor.Should().Be(_personas.Count);

        if (result.Aggregate.PersonasPreferringA > result.Aggregate.PersonasPreferringB)
        {
            result.Aggregate.OverallVerdict.Should().Be(ProductA.Name);
        }
        else if (result.Aggregate.PersonasPreferringB > result.Aggregate.PersonasPreferringA)
        {
            result.Aggregate.OverallVerdict.Should().Be(ProductB.Name);
        }
        else
        {
            result.Aggregate.OverallVerdict.Should().BeNull();
        }
    }

    [Fact]
    public void ContainsConfidentialData_propagates_from_either_product()
    {
        var engine = new VendorBakeOffEngine(new DeterministicHashSimulator());
        var aConfidential = ProductA with { ContainsConfidentialData = true };
        var result = engine.Run(aConfidential, ProductB, _personas, 200, 42, _t0);
        result.ContainsConfidentialData.Should().BeTrue();

        var bConfidential = ProductB with { ContainsConfidentialData = true };
        result = engine.Run(ProductA, bConfidential, _personas, 200, 42, _t0);
        result.ContainsConfidentialData.Should().BeTrue();

        result = engine.Run(ProductA, ProductB, _personas, 200, 42, _t0);
        result.ContainsConfidentialData.Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_null_simulator()
    {
        Action act = () => _ = new VendorBakeOffEngine(simulator: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComposeTemplatesRef_encodes_product_identity()
    {
        var encoded = VendorBakeOffEngine.ComposeTemplatesRef("templates/roma", ProductA);
        encoded.Should().Contain("templates/roma");
        encoded.Should().Contain("product:ProductA");
        encoded.Should().Contain("tier:starter");
        encoded.Should().Contain("icp:20-200 seat SaaS");
    }
}
