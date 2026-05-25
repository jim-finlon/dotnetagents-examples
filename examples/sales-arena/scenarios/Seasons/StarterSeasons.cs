namespace SalesArena.Seasons;

/// <summary>
/// The three starter season presets. Each is theme-distinct without
/// referencing copyrighted material — names are descriptive of the
/// gameplay shape, not direct quotes.
/// </summary>
public static class StarterSeasons
{
    public const string GlengarryId = "glengarry-classic";
    public const string WolfId = "wolf-high-volume";
    public const string OfficeSpaceId = "office-space-relaxed";

    /// <summary>Revenue-weighted, big-ticket closer's season. Long deals carry the leaderboard.</summary>
    public static Season Glengarry(DateTimeOffset startedAtUtc) => new(
        Id: GlengarryId,
        Name: "Glengarry Classic",
        Weights: new SeasonScoringWeights(
            RevenueWeight: 1.5,
            DealsWeight: 0.7,
            ConversionWeight: 0.5,
            BurnoutPenaltyPerHourOverThreshold: 0.0,
            BurnoutPenaltyThreshold: TimeSpan.FromHours(99)),
        PersonaBuffs: new[]
        {
            new PersonaBuff("roma", 50.0),
            new PersonaBuff("levene", 25.0),
        },
        Duration: TimeSpan.FromDays(28),
        StartedAtUtc: startedAtUtc,
        Description: "Revenue-weighted. Big tickets, long pitches. Closers win on contract value, not call volume.");

    /// <summary>Deals-count-weighted, boiler-room high-volume season.</summary>
    public static Season WolfOfHighVolume(DateTimeOffset startedAtUtc) => new(
        Id: WolfId,
        Name: "Wolf of High-Volume",
        Weights: new SeasonScoringWeights(
            RevenueWeight: 0.6,
            DealsWeight: 1.8,
            ConversionWeight: 1.0,
            BurnoutPenaltyPerHourOverThreshold: 0.0,
            BurnoutPenaltyThreshold: TimeSpan.FromHours(99)),
        PersonaBuffs: new[]
        {
            new PersonaBuff("levene", 50.0),
            new PersonaBuff("aaronow", 30.0),
        },
        Duration: TimeSpan.FromDays(14),
        StartedAtUtc: startedAtUtc,
        Description: "Deals-count-weighted. Volume wins; smiling-and-dialing through the rolodex outranks the marquee close.");

    /// <summary>
    /// Relaxed-cadence season — penalizes excessive grinding above a humane work-time
    /// threshold. Built to keep personas (and the humans they teach) sustainable.
    /// </summary>
    public static Season OfficeSpaceRelaxed(DateTimeOffset startedAtUtc) => new(
        Id: OfficeSpaceId,
        Name: "Office-Space Relaxed",
        Weights: new SeasonScoringWeights(
            RevenueWeight: 1.0,
            DealsWeight: 1.0,
            ConversionWeight: 1.2,
            BurnoutPenaltyPerHourOverThreshold: 15.0, // ELO penalty per hour worked beyond threshold
            BurnoutPenaltyThreshold: TimeSpan.FromHours(8)),
        PersonaBuffs: new[]
        {
            new PersonaBuff("moss", 40.0),
            new PersonaBuff("aaronow", 25.0),
        },
        Duration: TimeSpan.FromDays(21),
        StartedAtUtc: startedAtUtc,
        Description: "Anti-burnout. Conversion-quality rewarded; pushing past the daily cap penalizes ELO. Quality of close beats grinding.");

    public static IReadOnlyList<Season> AllAt(DateTimeOffset startedAtUtc) => new[]
    {
        Glengarry(startedAtUtc),
        WolfOfHighVolume(startedAtUtc),
        OfficeSpaceRelaxed(startedAtUtc),
    };
}
