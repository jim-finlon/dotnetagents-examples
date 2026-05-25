namespace SalesArena.Manager.Web.Models;

public sealed class PersonaFloorCardModel
{
    public required string PersonaId { get; init; }
    public required string DisplayName { get; init; }
    public required string AvatarGlyph { get; init; }
    public FloorTier Tier { get; init; } = FloorTier.SteakKnives;
    public FloorActivity Activity { get; init; } = FloorActivity.Idle;
    public int TouchesToday { get; init; }
    public int RepliesToday { get; init; }
    public int MeetingsToday { get; init; }
    public int DealsToday { get; init; }
    public IReadOnlyList<string> TickerLines { get; init; } = [];
    public bool PulseDealClosed { get; init; }

    public PersonaFloorCardModel With(
        FloorTier? tier = null,
        FloorActivity? activity = null,
        int? touchesToday = null,
        int? repliesToday = null,
        int? meetingsToday = null,
        int? dealsToday = null,
        IReadOnlyList<string>? tickerLines = null,
        bool? pulseDealClosed = null) =>
        new()
        {
            PersonaId = PersonaId,
            DisplayName = DisplayName,
            AvatarGlyph = AvatarGlyph,
            Tier = tier ?? Tier,
            Activity = activity ?? Activity,
            TouchesToday = touchesToday ?? TouchesToday,
            RepliesToday = repliesToday ?? RepliesToday,
            MeetingsToday = meetingsToday ?? MeetingsToday,
            DealsToday = dealsToday ?? DealsToday,
            TickerLines = tickerLines ?? TickerLines,
            PulseDealClosed = pulseDealClosed ?? PulseDealClosed,
        };
}
