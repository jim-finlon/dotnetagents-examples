namespace SalesArena.Orchestrator.Orchestration;

public sealed record OutboundQueueItem(
    string ItemId,
    string Persona,
    string Channel,
    string Body);
