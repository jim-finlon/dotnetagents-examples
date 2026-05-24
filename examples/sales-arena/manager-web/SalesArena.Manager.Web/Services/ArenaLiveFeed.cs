using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SalesArena.Manager.Web.Hubs;

namespace SalesArena.Manager.Web.Services;

/// <summary>
/// Per-circuit SignalR client for the manager live feed. Pages subscribe to
/// <see cref="EventsChanged"/> to render ledger-backed activity.
/// </summary>
public sealed class ArenaLiveFeed : IAsyncDisposable
{
    private const int MaxBufferedEvents = 200;
    private readonly NavigationManager _navigation;
    private HubConnection? _connection;
    private readonly List<ArenaEventMessage> _events = [];
    private readonly Lock _gate = new();

    public ArenaLiveFeed(NavigationManager navigation) => _navigation = navigation;

    public event Action? EventsChanged;

    public IReadOnlyList<ArenaEventMessage> RecentEvents
    {
        get
        {
            lock (_gate)
            {
                return _events.ToList();
            }
        }
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
        {
            return;
        }

        _connection ??= new HubConnectionBuilder()
            .WithUrl(_navigation.ToAbsoluteUri("/hubs/arena"))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<IReadOnlyList<ArenaEventMessage>>("ReceiveArenaEvent", OnBatchReceived);

        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync().ConfigureAwait(false);
            await _connection.InvokeAsync("JoinManagerFeedAsync").ConfigureAwait(false);
        }
    }

    private void OnBatchReceived(IReadOnlyList<ArenaEventMessage> batch)
    {
        lock (_gate)
        {
            foreach (var evt in batch)
            {
                _events.Add(evt);
            }

            if (_events.Count > MaxBufferedEvents)
            {
                _events.RemoveRange(0, _events.Count - MaxBufferedEvents);
            }
        }

        EventsChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
