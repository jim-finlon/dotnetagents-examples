namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Posts a bell event to an external channel (Slack, Discord, generic HTTP
/// webhook). Implementations format the bell payload per target shape.
/// Failures are swallowed + logged — a bell drop must not break the contest.
/// </summary>
public interface IBellWebhookPoster
{
    /// <summary>Stable target identifier (e.g. "slack", "discord", "generic-http").</summary>
    string TargetId { get; }

    /// <summary>Whether this poster is configured (URL provided + reachable in theory).</summary>
    bool IsConfigured { get; }

    /// <summary>Post the bell. Best-effort; returns true on 2xx, false otherwise.</summary>
    Task<bool> PostAsync(BellEvent evt, CancellationToken cancellationToken = default);
}
