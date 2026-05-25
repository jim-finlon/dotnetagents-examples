namespace SalesArena.Training.Diary;

/// <summary>
/// One end-of-day journal entry by one persona. Persisted as Markdown under
/// <c>samples/sales-arena/diary/&lt;contestId&gt;/&lt;persona&gt;/&lt;day&gt;.md</c>
/// (the file-system store does the actual write).
/// </summary>
/// <param name="ContestId">The contest this entry belongs to.</param>
/// <param name="Persona">The persona writing the entry.</param>
/// <param name="Day">1-based day index within the contest.</param>
/// <param name="GeneratedAtUtc">When the diary was generated (UTC).</param>
/// <param name="LeaderboardPosition">The persona's leaderboard position at end-of-day (1-based).</param>
/// <param name="WordCount">Computed body word count, useful for the AC's 150 ± 30 gate.</param>
/// <param name="CitedEventIds">Distinct event ids cited via <c>[evt:id]</c> in the body.</param>
/// <param name="Markdown">The full Markdown body (already includes the frontmatter header).</param>
public sealed record DiaryEntry(
    string ContestId,
    string Persona,
    int Day,
    DateTimeOffset GeneratedAtUtc,
    int LeaderboardPosition,
    int WordCount,
    IReadOnlyList<string> CitedEventIds,
    string Markdown);
