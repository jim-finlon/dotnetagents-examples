using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Training.Diary;

/// <summary>
/// Generates the per-persona diary entry for one contest day. Pulls events
/// from the ledger, asks the configured <see cref="IDiaryWriter"/> for the
/// body, applies the hallucination guard, and (optionally) persists via the
/// supplied <see cref="IDiaryStore"/>.
/// </summary>
public interface IDiaryGenerator
{
    /// <summary>
    /// Generate one diary entry for (persona, day). Returns the entry; when a
    /// store is configured, also persists it. Throws
    /// <see cref="DiaryGuardException"/> when the writer's output fails the
    /// hallucination guard.
    /// </summary>
    Task<DiaryEntry> GenerateAsync(
        DiaryGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Inputs for <see cref="IDiaryGenerator.GenerateAsync"/>.</summary>
/// <param name="ContestId">Contest scope.</param>
/// <param name="Persona">Persona writing the entry.</param>
/// <param name="Day">1-based day index.</param>
/// <param name="DayStartUtc">Inclusive lower bound for the day's events.</param>
/// <param name="DayEndUtc">Exclusive upper bound for the day's events.</param>
/// <param name="LeaderboardPosition">End-of-day leaderboard position.</param>
/// <param name="TotalPositions">Total personas in the contest.</param>
public sealed record DiaryGenerationRequest(
    string ContestId,
    string Persona,
    int Day,
    DateTimeOffset DayStartUtc,
    DateTimeOffset DayEndUtc,
    int LeaderboardPosition,
    int TotalPositions);

/// <summary>Thrown when the hallucination guard refuses the writer's output.</summary>
public sealed class DiaryGuardException : InvalidOperationException
{
    public DiaryGuardException(string message, GuardResult result) : base(message)
    {
        Result = result;
    }

    public GuardResult Result { get; }
}
