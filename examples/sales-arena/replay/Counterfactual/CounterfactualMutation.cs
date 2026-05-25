namespace SalesArena.Replay.Counterfactual;

/// <summary>
/// Closed set of counterfactual mutations. SA-08-06 acceptance pins the
/// surface to exactly three; adding new mutations is a follow-up story.
/// </summary>
public abstract record CounterfactualMutation
{
    public abstract string Kind { get; }

    /// <summary>
    /// Apply this mutation to a persona-config snapshot. Implementations
    /// must be pure — same input → same output, no side-effects, no clock,
    /// no I/O.
    /// </summary>
    public abstract IReadOnlyList<PersonaConfig> Apply(IReadOnlyList<PersonaConfig> original);

    /// <summary>
    /// True when applying this mutation does not change any field on any
    /// persona in <paramref name="original"/>. Used by the no-op zero-delta
    /// invariant.
    /// </summary>
    public bool IsNoOp(IReadOnlyList<PersonaConfig> original)
    {
        var mutated = Apply(original);
        if (mutated.Count != original.Count) return false;
        for (var i = 0; i < original.Count; i++)
        {
            if (!Equals(original[i], mutated[i])) return false;
        }
        return true;
    }
}

/// <summary>
/// Take persona A's outreach templates and apply them to persona B.
/// Persona A is unchanged.
/// </summary>
public sealed record SwapOutreachTemplatesMutation(string FromPersona, string ToPersona) : CounterfactualMutation
{
    public override string Kind => "SwapOutreachTemplates";

    public override IReadOnlyList<PersonaConfig> Apply(IReadOnlyList<PersonaConfig> original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var from = original.SingleOrDefault(p => p.Persona == FromPersona)
            ?? throw new ArgumentException($"FromPersona '{FromPersona}' not in config", nameof(original));
        return original.Select(p => p.Persona == ToPersona
            ? p.WithOutreachTemplatesRef(from.OutreachTemplatesRef)
            : p).ToArray();
    }
}

/// <summary>
/// Change a single persona's model tier (e.g. local-light → frontier-fallback).
/// </summary>
public sealed record SwapModelTierMutation(string Persona, string NewTier) : CounterfactualMutation
{
    public override string Kind => "SwapModelTier";

    public override IReadOnlyList<PersonaConfig> Apply(IReadOnlyList<PersonaConfig> original)
    {
        ArgumentNullException.ThrowIfNull(original);
        return original.Select(p => p.Persona == Persona ? p.WithModelTier(NewTier) : p).ToArray();
    }
}

/// <summary>
/// Swap a persona's cadence to a different cadence-pack reference.
/// </summary>
public sealed record SwapCadenceMutation(string Persona, string NewCadenceRef) : CounterfactualMutation
{
    public override string Kind => "SwapCadence";

    public override IReadOnlyList<PersonaConfig> Apply(IReadOnlyList<PersonaConfig> original)
    {
        ArgumentNullException.ThrowIfNull(original);
        return original.Select(p => p.Persona == Persona ? p.WithCadenceRef(NewCadenceRef) : p).ToArray();
    }
}
