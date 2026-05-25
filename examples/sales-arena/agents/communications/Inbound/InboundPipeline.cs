namespace SalesArena.Communications.Inbound;

/// <summary>
/// End-to-end ingest → classify → CRM correlate for demo hosts.
/// </summary>
public sealed class InboundPipeline(
    InboundMessageRouter router,
    InboundClassifier classifier,
    CrmCorrelator correlator)
{
    private readonly InboundMessageRouter _router = router ?? throw new ArgumentNullException(nameof(router));
    private readonly InboundClassifier _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
    private readonly CrmCorrelator _correlator = correlator ?? throw new ArgumentNullException(nameof(correlator));

    public async Task<IReadOnlyList<RoutedInboundMessage>> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _router.IngestAsync(cancellationToken).ConfigureAwait(false);
        var routed = new List<RoutedInboundMessage>(messages.Count);
        foreach (var message in messages)
        {
            var classification = await _classifier.ClassifyAsync(message, cancellationToken).ConfigureAwait(false);
            var correlation = _correlator.Correlate(message);
            routed.Add(new RoutedInboundMessage(message, classification, correlation));
        }

        return routed;
    }
}
