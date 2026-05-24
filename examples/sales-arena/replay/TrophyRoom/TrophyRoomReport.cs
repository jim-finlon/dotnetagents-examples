namespace SalesArena.Replay.TrophyRoom;

/// <summary>Output of <see cref="ITrophyRoomBuilder.BuildAsync"/>.</summary>
public sealed record TrophyRoomReport(
    DateTimeOffset GeneratedAtUtc,
    int TotalContests,
    IReadOnlyList<TrophyEntry> Trophies,
    IReadOnlyList<PersonaBaseballCard> BaseballCards,
    string Markdown);

/// <summary>One contest's trophy row.</summary>
public sealed record TrophyEntry(
    string ContestId,
    string? ContestDisplayName,
    DateTimeOffset ClosedAtUtc,
    string WinnerPersona,
    decimal WinnerRevenueUsd,
    int WinnerDealsWon);
