using DotNetAgents.Ui.Blazor;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class DraftPageTests : TestContext
{
    private readonly CapturingLedger _ledger = new();

    public DraftPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<IArenaLedger>(_ledger);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Draft_page_renders_order_and_free_agent_pool()
    {
        var cut = RenderComponent<global::SalesArena.Manager.Web.Components.Pages.Draft>(
            parameters => parameters.Add(p => p.ContestId, "contest-1"));

        cut.Find("[data-testid='draft-page']");
        Assert.Contains("Avery", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Engineer", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("On the clock", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Draft_pick_updates_pod_slot_and_appends_ledger_event()
    {
        var cut = RenderComponent<global::SalesArena.Manager.Web.Components.Pages.Draft>(
            parameters => parameters.Add(p => p.ContestId, "contest-1"));

        cut.Find("[data-testid='draft-pick-roma']").Click();
        cut.WaitForState(() => _ledger.Events.Count == 1);

        Assert.Contains("Roma drafted by Avery", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("DraftPickMade", cut.Find("[data-testid='draft-ledger']").TextContent, StringComparison.Ordinal);
        Assert.Equal(ArenaEventKinds.DraftPickMade, _ledger.Events.Single().Kind);
    }

    private sealed class CapturingLedger : IArenaLedger
    {
        public List<ArenaEvent> Events { get; } = [];

        public Task<ArenaEvent> AppendAsync(ArenaEvent evt, CancellationToken cancellationToken = default)
        {
            var inserted = evt with { Id = Events.Count + 1 };
            Events.Add(inserted);
            return Task.FromResult(inserted);
        }

        public Task<IReadOnlyList<ArenaEvent>> AppendManyAsync(
            IEnumerable<ArenaEvent> events,
            CancellationToken cancellationToken = default)
        {
            var inserted = events.Select(evt => evt with { Id = Events.Count + 1 }).ToArray();
            Events.AddRange(inserted);
            return Task.FromResult<IReadOnlyList<ArenaEvent>>(inserted);
        }

        public async IAsyncEnumerable<ArenaEvent> QueryAsync(
            ArenaEventFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var evt in Events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return evt;
                await Task.Yield();
            }
        }

        public Task<long> CountAsync(ArenaEventFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult((long)Events.Count);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
