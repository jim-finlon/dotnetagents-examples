using DotNetAgents.Ui.Blazor;
using SalesArena.Manager.Web.Auth;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Services;
using SalesArena.Manager.Web.Services.BossOffice;
using Microsoft.Extensions.FileProviders;
using SalesArena.Manager.Web.Services.ChannelPivot;
using SalesArena.Manager.Web.Services.Coach;
using SalesArena.Manager.Web.Services.Gallery;
using SalesArena.Orchestrator.Coach;
using SalesArena.Manager.Web.Services.ContestSettings;
using SalesArena.Manager.Web.Services.LeadPool;
using SalesArena.Manager.Web.Services.Bullpen;
using SalesArena.Manager.Web.Services.MoneyMap;
using SalesArena.Manager.Web.Services.Pipeline;
using SalesArena.Manager.Web.Services.Replay;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddDotNetAgentsUi();

builder.Services.AddSingleton<IArenaLedger>(sp =>
{
    var connection = builder.Configuration["Arena:LedgerConnectionString"]
        ?? "Data Source=:memory:";
    return new SqliteArenaLedger(connection);
});

builder.Services.AddSingleton<ILeadPoolSnapshotProvider, DemoLeadPoolSnapshotProvider>();
builder.Services.AddSingleton<IContestLifecycleHost, DemoContestLifecycleHost>();
builder.Services.AddSingleton<BossOfficeCostCatalogLoader>();
builder.Services.AddSingleton<DemoBossOfficeMetricsProvider>();
builder.Services.AddSingleton<IBossOfficeMetricsProvider>(sp => sp.GetRequiredService<DemoBossOfficeMetricsProvider>());
builder.Services.AddSingleton<IOperatorDashboardGate, OperatorDashboardGate>();
builder.Services.AddHostedService<BossOfficeMetricsRefreshService>();
builder.Services.AddScoped<ArenaLiveFeed>();
builder.Services.AddScoped<FloorViewState>();
builder.Services.AddScoped<PipelineFunnelState>();
builder.Services.AddScoped<BullpenCamState>();
builder.Services.AddSingleton<RegionCoordinateCatalog>();
builder.Services.AddSingleton<PersonaDisplayColorCatalog>();
builder.Services.AddSingleton<MoneyMapGeoJsonPaths>();
builder.Services.AddScoped<MoneyMapState>();
builder.Services.AddScoped<ILeaderboardEngine, LeaderboardEngine>();
builder.Services.AddScoped<IReplayGenerator, ReplayGenerator>();
builder.Services.AddScoped<IReplayBrowserService, ReplayBrowserService>();
builder.Services.AddSingleton<IChannelPivotService, ChannelPivotService>();
builder.Services.AddSingleton<GalleryFavoritesStore>();
builder.Services.AddSingleton<CommunityPersonaGalleryCatalog>();
builder.Services.AddSingleton<ICommunityPersonaGalleryCatalog>(sp =>
    sp.GetRequiredService<CommunityPersonaGalleryCatalog>());
builder.Services.AddSingleton<CoachOptions>();
builder.Services.AddSingleton<IPromptOverlayStore, InMemoryPromptOverlayStore>();
builder.Services.AddSingleton<ICoachModeService, CoachModeService>();
builder.Services.AddHostedService<ArenaLedgerTailBroadcaster>();
builder.Services.AddHostedService<ArenaDemoBootstrapService>();

builder.Services.AddAuthentication(ManagerIdentityDefaults.Scheme)
    .AddScheme<ManagerIdentityOptions, ManagerIdentityHandler>(
        ManagerIdentityDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(ManagerIdentityDefaults.Scheme)
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

var galleryCatalog = app.Services.GetRequiredService<ICommunityPersonaGalleryCatalog>();
if (Directory.Exists(galleryCatalog.CommunityRootPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(galleryCatalog.CommunityRootPath),
        RequestPath = "/community-personas",
    });
}

app.MapStaticAssets();
app.MapHub<ArenaHub>("/hubs/arena");
app.MapRazorComponents<SalesArena.Manager.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
