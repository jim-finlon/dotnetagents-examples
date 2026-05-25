using DotNetAgents.PreviewConfirm;
using SalesArena.Communications.Outbound;
using SalesArena.OutreachTemplates;

namespace SalesArena.Communications.Outbound.Tests;

internal static class OutboundTestHost
{
    internal static string PersonasRoot => FindPersonasRoot();

    private static string FindPersonasRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "personas"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "personas"),
        };

        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (Directory.Exists(Path.Combine(resolved, "roma", "outreach")))
            {
                return resolved;
            }
        }

        throw new DirectoryNotFoundException(
            $"could not locate personas root; checked: {string.Join(", ", candidates)}");
    }

    internal static ProspectContext SampleProspect => new(
        FirstName: "Alex",
        Company: "Acme Labs",
        Industry: "SaaS",
        RecentTopic: "pipeline visibility");

    internal static OutboundSendService BuildSendService(
        out AbPromotionTracker tracker,
        out PreviewConfirmGate previewGate,
        out SendRateLimiter rateLimiter,
        FakeTimeProvider? time = null,
        bool operatorPreviewDisabled = false)
    {
        time ??= new FakeTimeProvider(new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero));
        tracker = new AbPromotionTracker();
        var loader = new OutreachTemplateLoader(PersonasRoot);
        var drafter = new OutboundDrafter(loader, tracker);
        var coordinator = new PreviewConfirmCoordinator(new InMemoryPreviewConfirmSessionStore(), TimeSpan.FromMinutes(30));
        previewGate = new PreviewConfirmGate(coordinator, operatorPreviewDisabled: operatorPreviewDisabled);
        rateLimiter = new SendRateLimiter(timeProvider: time);
        return new OutboundSendService(drafter, tracker, previewGate, rateLimiter);
    }
}
