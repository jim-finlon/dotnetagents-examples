namespace SalesArena.Replay;

/// <summary>
/// Generates a narrative Markdown replay report for one contest. Reads the
/// SA-02-03 Ledger + the SA-02-04 Leaderboard, assembles the 5 canonical
/// sections, and surfaces the most newsworthy moments as
/// <see cref="ReplayHighlight"/>s for the narrative rewriter (SA-04-04).
/// </summary>
public interface IReplayGenerator
{
    Task<ReplayReport> GenerateAsync(ReplayOptions options, CancellationToken cancellationToken = default);

    /// <summary>Convenience helper: generate and write the Markdown to disk.</summary>
    Task<ReplayReport> ExportToFileAsync(
        ReplayOptions options,
        string outputPath,
        CancellationToken cancellationToken = default);
}
