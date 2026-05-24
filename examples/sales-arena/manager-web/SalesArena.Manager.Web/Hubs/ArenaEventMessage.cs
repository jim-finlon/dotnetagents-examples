using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Manager.Web.Hubs;

public sealed record ArenaEventMessage(
    long Id,
    string ContestId,
    string Kind,
    DateTimeOffset OccurredAtUtc,
    string? LeadId,
    string? Persona,
    string PayloadJson)
{
    public static ArenaEventMessage From(ArenaEvent evt) =>
        new(
            evt.Id,
            evt.ContestId,
            evt.Kind,
            evt.OccurredAtUtc,
            evt.LeadId,
            evt.Persona,
            evt.PayloadJson);
}
