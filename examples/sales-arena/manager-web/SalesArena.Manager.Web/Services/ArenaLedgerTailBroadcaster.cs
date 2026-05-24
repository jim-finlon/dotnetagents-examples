using Microsoft.AspNetCore.SignalR;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Manager.Web.Services;

/// <summary>
/// Tails the append-only ledger and pushes new <see cref="ArenaEvent"/> rows to SignalR
/// clients, throttled to ~5 fps to avoid UI backpressure.
/// </summary>
public sealed class ArenaLedgerTailBroadcaster : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IArenaLedger _ledger;
    private readonly IHubContext<ArenaHub> _hub;
    private readonly ILogger<ArenaLedgerTailBroadcaster> _logger;
    private long _lastEventId;
    private DateTimeOffset? _lastOccurredUtc;

    public ArenaLedgerTailBroadcaster(
        IArenaLedger ledger,
        IHubContext<ArenaHub> hub,
        ILogger<ArenaLedgerTailBroadcaster> logger)
    {
        _ledger = ledger;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastNewEventsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Arena ledger tail broadcast failed; retrying.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task BroadcastNewEventsAsync(CancellationToken cancellationToken)
    {
        var filter = new ArenaEventFilter(FromUtc: _lastOccurredUtc);
        var batch = new List<ArenaEventMessage>();

        await foreach (var evt in _ledger.QueryAsync(filter, cancellationToken).ConfigureAwait(false))
        {
            if (evt.Id <= _lastEventId)
            {
                continue;
            }

            batch.Add(ArenaEventMessage.From(evt));
            _lastEventId = evt.Id;
            _lastOccurredUtc = evt.OccurredAtUtc;
        }

        if (batch.Count == 0)
        {
            return;
        }

        await _hub.Clients
            .Group(ArenaHub.ManagerGroup)
            .SendAsync("ReceiveArenaEvent", batch, cancellationToken)
            .ConfigureAwait(false);
    }
}
