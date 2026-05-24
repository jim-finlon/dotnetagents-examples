using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Orchestration;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

public sealed class ArenaOrchestratorTests
{
    [Fact]
    public async Task TickAsync_ticks_each_pod_agent_and_flushes_activity_to_ledger()
    {
        await using var ledger = new RecordingArenaLedger();
        var tickLog = new List<string>();
        var podManager = new PodManager((podId, persona) => CreatePod(podId, persona, tickLog));
        var crmStream = new RecordingCrmEventStream();
        var outboundQueue = new RecordingOutboundQueue();
        var orchestrator = new ArenaOrchestrator("contest-1", podManager, ledger, crmStream, outboundQueue);

        podManager.SpawnPod("roma");
        podManager.SpawnPod("moss");
        podManager.SpawnPod("levene");

        var activities = await orchestrator.TickAsync();

        podManager.ActivePods.Should().HaveCount(3);
        activities.Should().HaveCount(15);
        tickLog.Should().HaveCount(15);
        tickLog.Should().OnlyContain(entry => entry.EndsWith(":1:1", StringComparison.Ordinal));
        tickLog.Should().OnlyContain(entry => entry.Contains(':', StringComparison.Ordinal));
        ledger.Events.Should().HaveCount(15);
        ledger.Events.Should().OnlyContain(evt =>
            evt.ContestId == "contest-1" &&
            evt.Kind == ArenaEventKinds.TouchSent &&
            (evt.Persona == "roma" || evt.Persona == "moss" || evt.Persona == "levene"));
    }

    [Fact]
    public void Despawn_removes_the_pod_from_future_ticks()
    {
        var podManager = new PodManager((podId, persona) => CreatePod(podId, persona, []));
        var pod = podManager.SpawnPod("roma");

        var removed = podManager.Despawn(pod.PodId);

        removed.Should().BeTrue();
        podManager.ActivePods.Should().BeEmpty();
    }

    private static PersonaPod CreatePod(string podId, string persona, List<string> tickLog) =>
        new(
            podId,
            persona,
            new RecordingAgent("crm", tickLog),
            new RecordingAgent("calendar", tickLog),
            new RecordingAgent("comms", tickLog),
            [
                new RecordingAgent("research", tickLog),
                new RecordingAgent("proposal", tickLog),
            ]);

    private sealed class RecordingAgent : IArenaPodAgent
    {
        private readonly List<string> _tickLog;

        public RecordingAgent(string role, List<string> tickLog)
        {
            Role = role;
            _tickLog = tickLog;
        }

        public string Role { get; }

        public Task<AgentTickResult?> TickAsync(
            PersonaPod pod,
            ArenaOrchestratorContext context,
            CancellationToken cancellationToken = default)
        {
            _tickLog.Add($"{pod.Persona}:{Role}:{context.CrmEvents.Count}:{context.OutboundQueue.Count}");
            return Task.FromResult<AgentTickResult?>(new AgentTickResult(Role, $"tick:{Role}", $"lead-{pod.Persona}"));
        }
    }

    private sealed class RecordingCrmEventStream : ICrmEventStream
    {
        public Task<IReadOnlyList<CrmStreamEvent>> ReadAvailableAsync(
            string contestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrmStreamEvent>>(
            [
                new CrmStreamEvent("crm-1", "roma", "LeadStageChanged", DateTimeOffset.UtcNow),
            ]);
    }

    private sealed class RecordingOutboundQueue : IOutboundQueue
    {
        public Task<IReadOnlyList<OutboundQueueItem>> ReadPendingAsync(
            string contestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OutboundQueueItem>>(
            [
                new OutboundQueueItem("out-1", "roma", "email", "Follow up"),
            ]);
    }

    private sealed class RecordingArenaLedger : IArenaLedger
    {
        public List<ArenaEvent> Events { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<ArenaEvent> AppendAsync(ArenaEvent evt, CancellationToken cancellationToken = default)
        {
            var persisted = evt with { Id = Events.Count + 1 };
            Events.Add(persisted);
            return Task.FromResult(persisted);
        }

        public async Task<IReadOnlyList<ArenaEvent>> AppendManyAsync(
            IEnumerable<ArenaEvent> events,
            CancellationToken cancellationToken = default)
        {
            var persisted = new List<ArenaEvent>();
            foreach (var evt in events)
            {
                persisted.Add(await AppendAsync(evt, cancellationToken).ConfigureAwait(false));
            }

            return persisted;
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
    }
}
