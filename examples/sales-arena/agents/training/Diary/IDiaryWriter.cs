using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Training.Diary;

/// <summary>
/// Writes a single in-voice diary body for one persona at end-of-day. The
/// implementer chooses the prose source (deterministic stub, real LLM via
/// DotNetAgents.PromptRuntime, or operator-curated template).
///
/// <para>AC contract: 150 words ± 30, 2+ citations to events in the
/// supplied day events. The generator enforces these gates before
/// admitting the entry.</para>
/// </summary>
public interface IDiaryWriter
{
    Task<string> WriteEntryAsync(
        DiaryDayContext day,
        CancellationToken cancellationToken = default);
}

/// <summary>Inputs the writer receives for one diary entry.</summary>
/// <param name="Persona">Persona name (e.g. "roma").</param>
/// <param name="Day">1-based day index in the contest.</param>
/// <param name="LeaderboardPosition">End-of-day position; influences morale tone.</param>
/// <param name="TotalPositions">Total personas in the contest (denominator for "near bottom" tone).</param>
/// <param name="DealsClosedToday">Wins recorded today.</param>
/// <param name="DealsLostToday">Losses recorded today.</param>
/// <param name="RevenueToday">Revenue closed today (USD).</param>
/// <param name="Events">Today's ledger events for this persona, oldest-first.</param>
public sealed record DiaryDayContext(
    string Persona,
    int Day,
    int LeaderboardPosition,
    int TotalPositions,
    int DealsClosedToday,
    int DealsLostToday,
    decimal RevenueToday,
    IReadOnlyList<ArenaEvent> Events);
