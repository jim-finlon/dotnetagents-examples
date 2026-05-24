using DotNetAgents.Agents.BehaviorTrees;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Orchestrator.Orchestration;

public sealed class ArenaOrchestrator : IAsyncDisposable
{
    private readonly IArenaLedger _ledger;
    private readonly ICrmEventStream _crmEventStream;
    private readonly IOutboundQueue _outboundQueue;
    private readonly ArenaOrchestratorOptions _options;
    private readonly SemaphoreSlim _tickGate;

    public ArenaOrchestrator(
        string contestId,
        IPodManager podManager,
        IArenaLedger ledger,
        ICrmEventStream? crmEventStream = null,
        IOutboundQueue? outboundQueue = null,
        SupervisorBehaviorTree? supervisor = null,
        ArenaOrchestratorOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);
        ContestId = contestId;
        PodManager = podManager ?? throw new ArgumentNullException(nameof(podManager));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _crmEventStream = crmEventStream ?? EmptyCrmEventStream.Instance;
        _outboundQueue = outboundQueue ?? EmptyOutboundQueue.Instance;
        Supervisor = supervisor ?? new SupervisorBehaviorTree();
        _options = options ?? new ArenaOrchestratorOptions();

        if (_options.MaxConcurrentPodTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxConcurrentPodTicks must be greater than zero.");
        }

        _tickGate = new SemaphoreSlim(1, 1);
    }

    public string ContestId { get; }

    public IPodManager PodManager { get; }

    public SupervisorBehaviorTree Supervisor { get; }

    public TimeSpan TickInterval => _options.TickInterval;

    public async Task<IReadOnlyList<PersonaActivity>> TickAsync(CancellationToken cancellationToken = default)
    {
        await _tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var context = new ArenaOrchestratorContext
            {
                ContestId = ContestId,
                Pods = PodManager.ActivePods,
                CrmEvents = await _crmEventStream.ReadAvailableAsync(ContestId, cancellationToken).ConfigureAwait(false),
                OutboundQueue = await _outboundQueue.ReadPendingAsync(ContestId, cancellationToken).ConfigureAwait(false),
            };

            var result = await Supervisor.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.Status is not BehaviorTreeNodeStatus.Success)
            {
                return Array.Empty<PersonaActivity>();
            }

            if (context.Activities.Count > 0)
            {
                var events = context.Activities.Select(ToArenaEvent).ToArray();
                await _ledger.AppendManyAsync(events, cancellationToken).ConfigureAwait(false);
            }

            return context.Activities;
        }
        finally
        {
            _tickGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _tickGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _tickGate.Release();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _tickGate.Dispose();
    }

    private ArenaEvent ToArenaEvent(PersonaActivity activity) =>
        new()
        {
            ContestId = ContestId,
            Kind = ArenaEventKinds.TouchSent,
            OccurredAtUtc = activity.OccurredAtUtc,
            LeadId = activity.LeadId,
            Persona = activity.Persona,
            PayloadJson = ArenaEvent.SerializePayload(activity),
        };
}
