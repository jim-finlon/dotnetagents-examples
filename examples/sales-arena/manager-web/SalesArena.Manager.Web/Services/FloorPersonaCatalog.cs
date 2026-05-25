using SalesArena.Manager.Web.Models;

namespace SalesArena.Manager.Web.Services;

public static class FloorPersonaCatalog
{
    public static IReadOnlyList<PersonaFloorCardModel> DefaultPods { get; } =
    [
        new()
        {
            PersonaId = "roma",
            DisplayName = "Roma",
            AvatarGlyph = "R",
            Tier = FloorTier.Cadillac,
            Activity = FloorActivity.Waiting,
        },
        new()
        {
            PersonaId = "levene",
            DisplayName = "Levene",
            AvatarGlyph = "L",
            Tier = FloorTier.SteakKnives,
            Activity = FloorActivity.Sending,
        },
        new()
        {
            PersonaId = "moss",
            DisplayName = "Moss",
            AvatarGlyph = "M",
            Tier = FloorTier.YouAreFired,
            Activity = FloorActivity.Researching,
        },
        new()
        {
            PersonaId = "aaronow",
            DisplayName = "Aaronow",
            AvatarGlyph = "A",
            Tier = FloorTier.SteakKnives,
            Activity = FloorActivity.Drafting,
        },
    ];
}
