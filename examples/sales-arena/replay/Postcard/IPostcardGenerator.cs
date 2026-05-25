using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Replay.Postcard;

/// <summary>
/// Renders a vintage-style SVG postcard celebrating the winner of a contest.
/// Pure function — same inputs always produce the same SVG bytes.
/// </summary>
public interface IPostcardGenerator
{
    /// <summary>
    /// Generate an SVG postcard. Returns the UTF-8-encoded SVG document body.
    /// </summary>
    /// <param name="leaderboard">The final leaderboard whose Cadillac persona is the postcard subject.</param>
    /// <param name="options">Visual style + display metadata.</param>
    string Generate(SalesArena.Orchestrator.Leaderboard.Leaderboard leaderboard, PostcardOptions? options = null);
}
