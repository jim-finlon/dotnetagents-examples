namespace SalesArena.Manager.Web.Services.Pipeline;

public sealed record PipelineStageSnapshot(
    string Stage,
    int Count,
    int RecentTransitions,
    IReadOnlyList<PipelinePersonaMarker> Personas);

public sealed record PipelinePersonaMarker(string Persona, string Stage);

public sealed record PipelineRevenueParticle(
    Guid Id,
    decimal ValueUsd,
    DateTimeOffset CreatedAtUtc);
