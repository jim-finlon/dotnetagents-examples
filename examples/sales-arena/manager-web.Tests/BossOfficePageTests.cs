using System.Security.Claims;
using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SalesArena.Manager.Web.Auth;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Services.BossOffice;
using SalesArena.Manager.Web.Services.ContestSettings;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class BossOfficePageTests : TestContext
{
    public BossOfficePageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());
        Services.AddSingleton<IContestLifecycleHost, DemoContestLifecycleHost>();
        Services.AddSingleton<BossOfficeCostCatalogLoader>();
        Services.AddSingleton<DemoBossOfficeMetricsProvider>();
        Services.AddSingleton<IBossOfficeMetricsProvider>(sp => sp.GetRequiredService<DemoBossOfficeMetricsProvider>());
        Services.AddSingleton<IOperatorDashboardGate, OperatorDashboardGate>();
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void BossOffice_renders_six_widgets_for_operator()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(isOperator: true));

        var cut = RenderComponent<BossOffice>();
        cut.WaitForState(() => cut.Find("[data-testid='boss-office-dashboard']").TextContent.Contains(
            "Boss Office",
            StringComparison.Ordinal));

        Assert.Equal(6, cut.FindAll(".sa-boss-office__card").Count);
        cut.Find("[data-testid='widget-contest-roi']");
        cut.Find("[data-testid='widget-cost-per-touch']");
        cut.Find("[data-testid='widget-model-tier-spend']");
        cut.Find("[data-testid='widget-opportunity-cost']");
        cut.Find("[data-testid='widget-contest-velocity']");
        cut.Find("[data-testid='widget-prospect-saturation']");
        cut.Find("[data-testid='boss-office-as-of']");
    }

    [Fact]
    public void BossOffice_denies_spectator_role()
    {
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(isOperator: false));

        var cut = RenderComponent<BossOffice>();

        cut.Find("[data-testid='boss-office-denied']");
        Assert.DoesNotContain(cut.Markup, "widget-contest-roi", StringComparison.Ordinal);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SalesArena.Manager.Web.Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        private readonly bool _isOperator;

        public TestAuthStateProvider(bool isOperator) => _isOperator = isOperator;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity("test");
            identity.AddClaim(new Claim(ClaimTypes.Name, _isOperator ? "manager" : "spectator"));
            if (_isOperator)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, ManagerIdentityDefaults.OperatorRole));
            }
            else
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, ManagerIdentityDefaults.SpectatorRole));
            }

            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
