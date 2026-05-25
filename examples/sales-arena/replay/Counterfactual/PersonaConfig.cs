namespace SalesArena.Replay.Counterfactual;

/// <summary>
/// The mutable surface a counterfactual mutation can swap. Minimal
/// projection of the full persona pack — only the fields the simulator
/// actually consumes when producing a deterministic <see cref="ContestOutcome"/>.
/// </summary>
public sealed record PersonaConfig(
    string Persona,
    string OutreachTemplatesRef,
    string ModelTier,
    string CadenceRef)
{
    public PersonaConfig WithOutreachTemplatesRef(string newRef) => this with { OutreachTemplatesRef = newRef };
    public PersonaConfig WithModelTier(string newTier) => this with { ModelTier = newTier };
    public PersonaConfig WithCadenceRef(string newRef) => this with { CadenceRef = newRef };
}
