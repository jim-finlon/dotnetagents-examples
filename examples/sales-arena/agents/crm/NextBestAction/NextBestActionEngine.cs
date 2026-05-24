using System;
using System.Collections.Generic;

namespace SalesArena.Crm.NextBestAction;

public interface INextBestActionEngine
{
    NbaDecision Decide(string personaId, CrmContext context);
}

public sealed class NextBestActionEngine : INextBestActionEngine
{
    private readonly IReadOnlyDictionary<string, IPersonaStrategy> _strategies;

    public NextBestActionEngine() : this(PersonaStrategies.Defaults) { }

    public NextBestActionEngine(IReadOnlyDictionary<string, IPersonaStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    public NbaDecision Decide(string personaId, CrmContext context)
    {
        if (string.IsNullOrWhiteSpace(personaId)) throw new ArgumentException("personaId is required", nameof(personaId));
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (!_strategies.TryGetValue(personaId, out var strategy))
            throw new ArgumentException($"No strategy registered for persona '{personaId}'.", nameof(personaId));

        var trace = new List<string>();
        var tree = strategy.BuildTree();
        var result = tree.Evaluate(context, trace);
        if (result.Action is null)
            throw new InvalidOperationException($"Strategy {personaId} produced no action; this indicates a missing fallback leaf.");

        return new NbaDecision(result.Action.Value, result.Reason, strategy.PersonaId, trace);
    }
}
