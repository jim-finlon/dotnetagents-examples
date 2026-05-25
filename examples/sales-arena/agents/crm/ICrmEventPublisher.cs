namespace SalesArena.Crm;

/// <summary>
/// Publishes <see cref="CrmStageChangedEvent"/>s to in-proc subscribers.
///
/// <para>The Arena's event spine is intentionally lightweight at the agent
/// level — subscribers attach via the <see cref="StageChanged"/> event and
/// react synchronously. Downstream durability (the Arena Ledger) lives in
/// SA-02-03; this interface just emits, it does not persist.</para>
/// </summary>
public interface ICrmEventPublisher
{
    /// <summary>Raised after a CRM transition has been applied + persisted to the activity log.</summary>
    event EventHandler<CrmStageChangedEvent>? StageChanged;

    /// <summary>Publish a stage-change event to all subscribers.</summary>
    Task PublishAsync(CrmStageChangedEvent evt, CancellationToken cancellationToken = default);
}
