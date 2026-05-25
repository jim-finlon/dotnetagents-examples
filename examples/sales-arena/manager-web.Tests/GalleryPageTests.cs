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

public sealed class GalleryPageTests : TestContext
{
    public GalleryPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<ICommunityPersonaGalleryCatalog>(new StubGalleryCatalog());
        Services.AddSingleton<GalleryFavoritesStore>();
        Services.AddSingleton<CoachOptions>();
        Services.AddSingleton<IPromptOverlayStore, InMemoryPromptOverlayStore>();
        Services.AddSingleton<ICoachModeService, CoachModeService>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Gallery_renders_cards_and_filters()
    {
        var cut = RenderComponent<Gallery>();
        cut.WaitForState(() => cut.FindAll("[data-testid='gallery-card']").Count >= 2);

        cut.Find("[data-testid='gallery-tag-filters']");
        cut.Find("[data-testid='gallery-elo-filters']");
    }

    [Fact]
    public void Gallery_filter_by_tag_narrows_cards()
    {
        var cut = RenderComponent<Gallery>();
        cut.WaitForState(() => cut.FindAll("[data-testid='gallery-card']").Count >= 2);

        var before = cut.FindAll("[data-testid='gallery-card']").Count;
        var tagChip = cut.FindAll("[data-testid='gallery-tag-chip']").First(e =>
            e.TextContent.Contains("technical", StringComparison.OrdinalIgnoreCase));
        tagChip.Click();
        cut.WaitForState(() => cut.FindAll("[data-testid='gallery-card']").Count < before);
    }

    [Fact]
    public void Gallery_card_click_navigates_to_detail()
    {
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        var cut = RenderComponent<Gallery>();
        cut.WaitForState(() => cut.FindAll("[data-testid='gallery-card']").Count > 0);

        cut.Find(".sa-gallery__card-body").Click();
        Assert.Contains("/gallery/engineer", nav.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gallery_challenge_shows_notice()
    {
        var cut = RenderComponent<Gallery>();
        cut.WaitForState(() => cut.FindAll("[data-testid='challenge-button']").Count > 0);

        cut.Find("[data-testid='challenge-button']").Click();
        cut.WaitForState(() => cut.Find("[data-testid='challenge-notice']") is not null);
    }

    internal sealed class StubGalleryCatalog : ICommunityPersonaGalleryCatalog
    {
        public string CommunityRootPath => ".";

        public Task<CommunityPersonaGalleryIndex> LoadIndexAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CommunityPersonaCard> cards =
            [
                Card("engineer", ["technical-depth", "roi-math"]),
                Card("hardballer", ["aggressive", "roi-math"]),
            ];
            return Task.FromResult(new CommunityPersonaGalleryIndex(cards, ["technical-depth", "roi-math", "aggressive"]));
        }

        public Task<CommunityPersonaDetail?> GetDetailAsync(string slug, CancellationToken cancellationToken = default)
        {
            CommunityPersonaDetail? detail = slug == "engineer"
                ? new(
                    Card("engineer", ["technical-depth"]),
                    "Bio body",
                    "Prompt body",
                    [new("Demo contest", "Won", DateTimeOffset.UtcNow)])
                : null;
            return Task.FromResult(detail);
        }

        public string? QueueChallenge(string slug) => slug;

        private static CommunityPersonaCard Card(string slug, string[] tags) =>
            new(slug, slug, "dna-community-seed", "Excerpt", tags, 12, 4, 20, 14.5, 1320, true, $"/community-personas/{slug}/avatar.svg");
    }

    internal sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "manager")],
                ManagerIdentityDefaults.Scheme);
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
