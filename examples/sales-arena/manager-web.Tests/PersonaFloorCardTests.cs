using Bunit;
using SalesArena.Manager.Web.Components.Floor;
using SalesArena.Manager.Web.Models;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class PersonaFloorCardTests : TestContext
{
    [Fact]
    public void Persona_card_renders_stats_ticker_and_tier_class()
    {
        var model = new PersonaFloorCardModel
        {
            PersonaId = "roma",
            DisplayName = "Roma",
            AvatarGlyph = "R",
            Tier = FloorTier.Cadillac,
            Activity = FloorActivity.Sending,
            TouchesToday = 3,
            RepliesToday = 1,
            MeetingsToday = 2,
            DealsToday = 1,
            TickerLines = ["10:15 · Deal closed"],
        };

        var cut = RenderComponent<PersonaFloorCard>(p => p.Add(x => x.Model, model));

        Assert.Contains("Roma", cut.Markup);
        Assert.Contains("Touches", cut.Markup);
        Assert.Contains("Deal closed", cut.Markup);
        Assert.Contains("floor-persona--cadillac", cut.Markup);
    }

    [Fact]
    public void Persona_card_reflects_fired_tier_color_class()
    {
        var model = new PersonaFloorCardModel
        {
            PersonaId = "moss",
            DisplayName = "Moss",
            AvatarGlyph = "M",
            Tier = FloorTier.YouAreFired,
        };

        var cut = RenderComponent<PersonaFloorCard>(p => p.Add(x => x.Model, model));

        Assert.Contains("floor-persona--fired", cut.Markup);
        Assert.Contains("You're Fired", cut.Markup);
    }
}
