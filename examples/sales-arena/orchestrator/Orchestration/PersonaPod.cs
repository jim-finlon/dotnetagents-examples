namespace SalesArena.Orchestrator.Orchestration;

public sealed record PersonaPod(
    string PodId,
    string Persona,
    IArenaPodAgent Crm,
    IArenaPodAgent Calendar,
    IArenaPodAgent Comms,
    IReadOnlyList<IArenaPodAgent> Shared)
{
    public IReadOnlyList<IArenaPodAgent> Agents { get; } =
        new[] { Crm, Calendar, Comms }.Concat(Shared).ToArray();
}
