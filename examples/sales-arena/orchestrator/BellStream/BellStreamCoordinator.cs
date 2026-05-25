namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Subscribes to the bell bus, applies the rate limiter, and fans bells to
/// every configured webhook poster in parallel. The orchestrator wires one
/// of these per contest; spectators see live bells regardless of how many
/// posters are configured.
///
/// <para>Webhook posts are best-effort — a single failed poster doesn't
/// block the others, and the overall handler never throws on the publisher's
/// thread (errors are swallowed via the posters' own catch). This keeps the
/// game loop running even when the network is hostile.</para>
/// </summary>
public sealed class BellStreamCoordinator : IAsyncDisposable
{
    private readonly IBellEventBus _bus;
    private readonly IReadOnlyList<IBellWebhookPoster> _posters;
    private readonly BellRateLimiter _rateLimiter;
    private bool _disposed;

    public BellStreamCoordinator(
        IBellEventBus bus,
        IEnumerable<IBellWebhookPoster> posters,
        BellRateLimiter rateLimiter)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _posters = (posters ?? throw new ArgumentNullException(nameof(posters))).ToList();
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

        _bus.BellRang += OnBellRang;
    }

    /// <summary>True for bells that were dropped by the rate-limit gate. Useful for tests + diagnostics.</summary>
    public int RateLimitedDropCount { get; private set; }

    /// <summary>Count of bells that were dispatched to at least one configured poster.</summary>
    public int DispatchedCount { get; private set; }

    private async void OnBellRang(object? sender, BellEvent evt)
    {
        // Fire-and-forget by design — the publisher's thread is the Arena's tick loop.
        // Never let an exception escape (the handler is `async void`).
        try
        {
            if (!_rateLimiter.TryAcquire())
            {
                RateLimitedDropCount++;
                return;
            }

            await DispatchAsync(evt, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Swallow — the bell is theatre, not a hard correctness gate.
        }
    }

    /// <summary>
    /// Public dispatch helper — same routing as the event handler, but with
    /// a caller-visible Task. Tests use this to assert post-conditions.
    /// </summary>
    public async Task<int> DispatchAsync(BellEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var configured = _posters.Where(p => p.IsConfigured).ToList();
        if (configured.Count == 0) return 0;

        var posts = configured.Select(p => p.PostAsync(evt, cancellationToken)).ToList();
        var results = await Task.WhenAll(posts).ConfigureAwait(false);
        var successCount = results.Count(ok => ok);
        if (successCount > 0) DispatchedCount++;
        return successCount;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _bus.BellRang -= OnBellRang;
        return ValueTask.CompletedTask;
    }
}
