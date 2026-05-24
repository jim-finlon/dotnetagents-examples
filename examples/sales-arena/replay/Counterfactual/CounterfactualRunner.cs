namespace SalesArena.Replay.Counterfactual;

public sealed class CounterfactualRunner : ICounterfactualRunner
{
    private readonly IContestSimulator _simulator;

    public CounterfactualRunner(IContestSimulator simulator)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    public CounterfactualResult Run(
        string originalContestId,
        IReadOnlyList<PersonaConfig> originalPersonas,
        CounterfactualMutation mutation,
        CounterfactualRunOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalContestId);
        ArgumentNullException.ThrowIfNull(originalPersonas);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(options);

        var mutated = mutation.Apply(originalPersonas);
        var counterfactualId = originalContestId + options.CounterfactualContestIdSuffix;

        var original = _simulator.Simulate(originalContestId, originalPersonas, options.LeadPoolSize, options.Seed);
        var counterfactual = _simulator.Simulate(counterfactualId, mutated, options.LeadPoolSize, options.Seed);

        var diff = ComputeDiff(original, counterfactual);
        return new CounterfactualResult(originalContestId, mutation, original, counterfactual, diff);
    }

    /// <summary>Pure delta computation. Public so callers can diff outcomes they already have.</summary>
    public static ContestOutcomeDiff ComputeDiff(ContestOutcome original, ContestOutcome counterfactual)
    {
        var byPersonaCf = counterfactual.Personas.ToDictionary(p => p.Persona, StringComparer.Ordinal);
        var deltas = new List<PersonaOutcomeDiff>(original.Personas.Count);
        foreach (var o in original.Personas)
        {
            var c = byPersonaCf.TryGetValue(o.Persona, out var match) ? match : null;
            if (c is null)
            {
                continue;
            }
            deltas.Add(new PersonaOutcomeDiff(
                Persona: o.Persona,
                TouchesDelta: c.TouchesSent - o.TouchesSent,
                MeetingsDelta: c.MeetingsHeld - o.MeetingsHeld,
                DealsWonDelta: c.DealsWon - o.DealsWon,
                DealsLostDelta: c.DealsLost - o.DealsLost,
                RevenueDeltaUsd: c.RevenueUsd - o.RevenueUsd,
                PositionDelta: c.FinalPosition - o.FinalPosition));
        }
        return new ContestOutcomeDiff(original.ContestId, counterfactual.ContestId, deltas);
    }
}
