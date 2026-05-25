namespace SalesArena.Communications.Inbound.Tests;

internal static class InboundTestHost
{
    internal static string FixtureRoot
    {
        get
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "test-fixtures"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "agents", "communications", "test-fixtures"),
            };

            foreach (var c in candidates)
            {
                var resolved = Path.GetFullPath(c);
                if (Directory.Exists(resolved))
                {
                    return resolved;
                }
            }

            throw new DirectoryNotFoundException(
                $"could not locate communications test-fixtures; checked: {string.Join(", ", candidates)}");
        }
    }

    internal static InboundPipeline BuildPipeline()
    {
        var root = FixtureRoot;
        var adapters = new IInboundChannelAdapter[]
        {
            new LocalEmailInboundAdapter(Path.Combine(root, "email-inbox.json")),
            new LocalSmsInboundAdapter(Path.Combine(root, "sms-inbox.json")),
            new LocalWebFormInboundAdapter(Path.Combine(root, "webform-inbox.json")),
            new LocalChatInboundAdapter(Path.Combine(root, "chat-inbox.json")),
        };
        var router = new InboundMessageRouter(adapters);
        var classifier = new InboundClassifier(new StubInboundClassificationModel());
        var correlator = new CrmCorrelator(CrmProspectIndexLoader.LoadFromJson(Path.Combine(root, "crm-prospects.json")));
        return new InboundPipeline(router, classifier, correlator);
    }
}
