using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Services.ContestSettings;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class SettingsPageTests : TestContext
{
    private readonly TestContestLifecycleHost _lifecycle = new();

    public SettingsPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<IContestLifecycleHost>(_lifecycle);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Settings_renders_contest_form_controls()
    {
        var cut = RenderComponent<Settings>();

        Assert.Contains("Contest settings", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid='persona-checkboxes'] input"));
        Assert.NotEmpty(cut.FindAll("[data-testid='rule-toggles'] input"));
        cut.Find("#prize-tier");
        cut.Find("#scoring-metric");
    }

    [Fact]
    public void Settings_validation_fails_when_contest_name_cleared()
    {
        var cut = RenderComponent<Settings>();
        var nameInput = cut.Find("#contest-name");
        nameInput.Change("");

        var submit = cut.Find("[data-testid='start-contest-button']");
        cut.InvokeAsync(() =>
        {
            submit.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        cut.WaitForState(() => cut.Markup.Contains("Contest name is required", StringComparison.Ordinal));
        Assert.Equal(ContestRunState.Idle, _lifecycle.State);
    }

    [Fact]
    public void Settings_validation_fails_when_no_persona_selected()
    {
        var cut = RenderComponent<Settings>();

        for (var i = 0; i < 6; i++)
        {
            var index = i;
            cut.InvokeAsync(() =>
            {
                var checkboxes = cut.FindAll("[data-testid='persona-checkboxes'] input");
                checkboxes[index].Change(false);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }

        var submit = cut.Find("[data-testid='start-contest-button']");
        cut.InvokeAsync(() =>
        {
            submit.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        cut.WaitForState(() =>
            cut.Markup.Contains(ContestSettingsValidation.PersonaRequiredMessage, StringComparison.Ordinal));
        Assert.Equal(ContestRunState.Idle, _lifecycle.State);
    }

    [Fact]
    public void Settings_start_contest_happy_path_init_then_active()
    {
        var cut = RenderComponent<Settings>();

        var submit = cut.Find("[data-testid='start-contest-button']");
        cut.InvokeAsync(() =>
        {
            submit.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        cut.WaitForState(() => _lifecycle.State == ContestRunState.Active);
        Assert.Contains("Contest started", _lifecycle.LastStatusMessage, StringComparison.Ordinal);
        cut.WaitForState(() => cut.Find("[data-testid='settings-feedback']").TextContent.Contains(
            "Contest started",
            StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_blocks_submit_when_contest_already_active()
    {
        _lifecycle.State = ContestRunState.Active;
        _lifecycle.LastStatusMessage = "Already running";

        var cut = RenderComponent<Settings>();

        cut.Find("[data-testid='active-contest-banner']");
        var submit = cut.Find("[data-testid='start-contest-button']");
        Assert.True(submit.HasAttribute("disabled"));

        cut.InvokeAsync(() =>
        {
            submit.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        Assert.Equal(ContestRunState.Active, _lifecycle.State);
        Assert.DoesNotContain(cut.Markup, "Contest started (demo host", StringComparison.Ordinal);
    }

    private sealed class TestContestLifecycleHost : IContestLifecycleHost
    {
        public ContestRunState State { get; set; } = ContestRunState.Idle;

        public string? LastStatusMessage { get; set; }

        public Task<ContestLifecycleResult> InitAsync(
            ContestSettingsDraft draft,
            CancellationToken cancellationToken = default)
        {
            if (State is ContestRunState.Active or ContestRunState.Paused)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked("Active contest."));
            }

            if (draft.EnabledPersonas.Count == 0)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked(
                    ContestSettingsValidation.PersonaRequiredMessage));
            }

            State = ContestRunState.Initialized;
            LastStatusMessage = $"Initialized {draft.ContestName}";
            return Task.FromResult(ContestLifecycleResult.Ok(LastStatusMessage));
        }

        public Task<ContestLifecycleResult> StartAsync(CancellationToken cancellationToken = default)
        {
            if (State != ContestRunState.Initialized)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked("Not initialized."));
            }

            State = ContestRunState.Active;
            LastStatusMessage = "Contest started (demo host — wire SA-02-05 for ledger-backed lifecycle).";
            return Task.FromResult(ContestLifecycleResult.Ok(LastStatusMessage));
        }
    }
}
