using SalesArena.Replay.Counterfactual;

namespace SalesArena.BakeOff;

public sealed class VendorBakeOffEngine : IVendorBakeOffEngine
{
    private readonly IContestSimulator _simulator;
    private readonly TimeProvider _timeProvider;

    public VendorBakeOffEngine(IContestSimulator simulator, TimeProvider? timeProvider = null)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public BakeOffResult Run(
        ProductProfile productA,
        ProductProfile productB,
        IReadOnlyList<PersonaConfig> personas,
        int leadPoolSize,
        int seed,
        DateTimeOffset? completedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(productA);
        ArgumentNullException.ThrowIfNull(productB);
        ArgumentNullException.ThrowIfNull(personas);

        if (string.IsNullOrWhiteSpace(productA.Name) || string.IsNullOrWhiteSpace(productB.Name))
        {
            throw new BakeOffException(BakeOffErrorCode.EmptyProductName, "both products must have a non-empty name");
        }
        if (string.Equals(productA.Name, productB.Name, StringComparison.Ordinal))
        {
            throw new BakeOffException(BakeOffErrorCode.SameProductBakeOff,
                "bake-off requires two distinct products; got the same Name twice");
        }
        if (string.IsNullOrWhiteSpace(productA.IdealCustomerProfile) || string.IsNullOrWhiteSpace(productB.IdealCustomerProfile))
        {
            throw new BakeOffException(BakeOffErrorCode.EmptyIdealCustomerProfile,
                "both products must declare an IdealCustomerProfile so the simulator can differentiate outcomes");
        }
        if (personas.Count == 0)
        {
            throw new BakeOffException(BakeOffErrorCode.EmptyPersonaList, "bake-off requires at least one persona");
        }

        var personasForA = personas.Select(p => p with { OutreachTemplatesRef = ComposeTemplatesRef(p.OutreachTemplatesRef, productA) }).ToArray();
        var personasForB = personas.Select(p => p with { OutreachTemplatesRef = ComposeTemplatesRef(p.OutreachTemplatesRef, productB) }).ToArray();

        var bakeOffId = Guid.NewGuid().ToString("N");
        var outcomeA = _simulator.Simulate($"{bakeOffId}-A-{productA.Name}", personasForA, leadPoolSize, seed);
        var outcomeB = _simulator.Simulate($"{bakeOffId}-B-{productB.Name}", personasForB, leadPoolSize, seed);

        var byPersonaA = outcomeA.Personas.ToDictionary(p => p.Persona, StringComparer.Ordinal);
        var byPersonaB = outcomeB.Personas.ToDictionary(p => p.Persona, StringComparer.Ordinal);

        var perPersona = new List<PersonaBakeOffResult>(personas.Count);
        foreach (var config in personas)
        {
            if (!byPersonaA.TryGetValue(config.Persona, out var withA) ||
                !byPersonaB.TryGetValue(config.Persona, out var withB))
            {
                continue;
            }
            string? preferred = null;
            if (withA.RevenueUsd > withB.RevenueUsd) preferred = productA.Name;
            else if (withB.RevenueUsd > withA.RevenueUsd) preferred = productB.Name;
            perPersona.Add(new PersonaBakeOffResult(
                Persona: config.Persona,
                WithProductA: withA,
                WithProductB: withB,
                PreferredProduct: preferred,
                RevenueDeltaUsd: withA.RevenueUsd - withB.RevenueUsd));
        }

        var aggregate = Aggregate(perPersona, productA, productB);

        return new BakeOffResult(
            BakeOffId: bakeOffId,
            ProductA: productA,
            ProductB: productB,
            Seed: seed,
            LeadPoolSize: leadPoolSize,
            CompletedAtUtc: completedAtUtc ?? _timeProvider.GetUtcNow(),
            PerPersona: perPersona,
            Aggregate: aggregate);
    }

    /// <summary>Encode product identity into a templates-ref so the deterministic simulator differentiates the two runs.</summary>
    public static string ComposeTemplatesRef(string original, ProductProfile product)
    {
        // Encode the product identity into the templates ref so the
        // simulator's deterministic hash differentiates the two runs even
        // when callers pass the same persona config for both products.
        return $"{original}|product:{product.Name}|tier:{product.PricingTier}|icp:{product.IdealCustomerProfile}";
    }

    private static BakeOffAggregate Aggregate(IReadOnlyList<PersonaBakeOffResult> perPersona, ProductProfile a, ProductProfile b)
    {
        var preferA = perPersona.Count(p => p.PreferredProduct == a.Name);
        var preferB = perPersona.Count(p => p.PreferredProduct == b.Name);
        var tied = perPersona.Count(p => p.PreferredProduct is null);
        var totalDelta = perPersona.Sum(p => p.RevenueDeltaUsd);

        string? verdict = null;
        if (perPersona.Count > 0)
        {
            if (preferA > preferB) verdict = a.Name;
            else if (preferB > preferA) verdict = b.Name;
            // null verdict = tied at the aggregate level.
        }
        return new BakeOffAggregate(preferA, preferB, tied, totalDelta, verdict);
    }
}
