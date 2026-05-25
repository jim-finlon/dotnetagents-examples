using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class ManagerWebSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ManagerWebSmokeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Floor_route_returns_ok()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });

        var response = await client.GetAsync("/floor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The Floor", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("floor-grid", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Leaderboard_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/leaderboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Leads_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/leads");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Replay_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/replay");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Replay browser", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Settings_contest_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/settings/contest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Contest settings", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start-contest-button", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Boss_office_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/boss-office");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Boss Office", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("widget-contest-roi", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gallery_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/gallery");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Persona gallery", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gallery-page", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coach_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/coach/engineer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Halftime speech", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coach-page", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pivot_route_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/pivot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Channel effectiveness", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pivot-dashboard", html, StringComparison.OrdinalIgnoreCase);
    }
}
