using System.Security.Claims;
using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Auth;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Services.Coach;
using SalesArena.Manager.Web.Services.Gallery;
using SalesArena.Orchestrator.Coach;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class GalleryDetailPageTests : TestContext
{
    public GalleryDetailPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<ICommunityPersonaGalleryCatalog>(new GalleryPageTests.StubGalleryCatalog());
        Services.AddSingleton<CoachOptions>();
        Services.AddSingleton<IPromptOverlayStore, InMemoryPromptOverlayStore>();
        Services.AddSingleton<ICoachModeService, CoachModeService>();
        Services.AddSingleton<AuthenticationStateProvider>(new GalleryPageTests.TestAuthStateProvider());
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void GalleryDetail_renders_bio_and_prompt_preview()
    {
        var cut = RenderComponent<GalleryDetail>(parameters => parameters
            .Add(p => p.Slug, "engineer"));

        cut.WaitForState(() => cut.Find("[data-testid='gallery-bio']").TextContent.Contains(
            "Bio",
            StringComparison.Ordinal));
        cut.Find("[data-testid='gallery-prompt-preview']");
        cut.Find("[data-testid='gallery-recent-contests']");
    }
}
