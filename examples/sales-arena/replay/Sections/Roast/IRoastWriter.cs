using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections.Roast;

/// <summary>
/// Writes one roast paragraph (roaster → target). The implementer decides
/// where the prose comes from — deterministic stub (tests, demo-mode
/// offline), real LLM via DotNetAgents.PromptRuntime, or operator-curated
/// templates.
///
/// <para>Every paragraph the writer emits MUST include at least one
/// <c>[evt:id]</c> citation referencing an event from the target-events
/// argument; the engine's hallucination guard refuses output that fails
/// this check.</para>
/// </summary>
public interface IRoastWriter
{
    Task<string> WriteRoastAsync(
        string roaster,
        string target,
        IReadOnlyList<ArenaEvent> targetEvents,
        CancellationToken cancellationToken = default);
}
