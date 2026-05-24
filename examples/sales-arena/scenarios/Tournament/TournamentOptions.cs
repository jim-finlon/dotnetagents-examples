namespace SalesArena.Tournament;

/// <summary>
/// Engine tunables. Defaults are conservative for the public flagship demo;
/// operators raise the cap explicitly when they want a 32-persona bracket.
/// </summary>
public sealed record TournamentOptions
{
    /// <summary>
    /// Hard cap on entrants per bracket. Story acceptance pins 8 as the
    /// default; bigger requires explicit opt-in.
    /// </summary>
    public int MaxPersonas { get; init; } = 8;

    /// <summary>Default leads-per-round budget if the caller doesn't override.</summary>
    public int DefaultLeadsPerRound { get; init; } = 20;

    /// <summary>
    /// If true, top-seed-first byes are assigned (highest-seeded personas
    /// skip round 1). If false, byes go to the lowest seeds. Defaults true
    /// (standard tournament convention).
    /// </summary>
    public bool TopSeedsGetByes { get; init; } = true;

    /// <summary>
    /// ELO baseline used when a persona has no rating in the chosen season
    /// scope. Defaults to <see cref="SalesArena.Seasons.EloCalculator.DefaultStartingRating"/>.
    /// </summary>
    public double DefaultRating { get; init; } = SalesArena.Seasons.EloCalculator.DefaultStartingRating;
}
