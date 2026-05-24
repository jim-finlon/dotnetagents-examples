using SalesArena.Manager.Web.Services.ContestSettings;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class ContestSettingsValidationTests
{
    [Fact]
    public void HasEnabledPersona_returns_false_when_all_unchecked()
    {
        var model = new ContestSettingsFormModel
        {
            PersonaRomano = false,
            PersonaMoss = false,
            PersonaAaronow = false,
            PersonaLevene = false,
            PersonaWilliamson = false,
            PersonaHarris = false,
        };

        Assert.False(ContestSettingsValidation.HasEnabledPersona(model));
        Assert.Empty(model.GetEnabledPersonas());
    }

    [Fact]
    public void ToDraft_includes_selected_personas_and_rules()
    {
        var model = new ContestSettingsFormModel
        {
            ContestName = "Test Run",
            PersonaRomano = true,
            PersonaMoss = true,
            PersonaAaronow = false,
            PersonaLevene = false,
            PersonaWilliamson = false,
            PersonaHarris = false,
            RuleBellOnClose = false,
        };

        var draft = model.ToDraft();

        Assert.Equal("Test Run", draft.ContestName);
        Assert.Equal(["romano", "moss"], draft.EnabledPersonas);
        Assert.False(draft.Rules.BellOnClose);
        Assert.True(draft.Rules.NoDoubleTouch);
    }
}
