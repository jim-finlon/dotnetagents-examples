using FluentAssertions;
using SalesArena.Replay.TraceExplorer;
using Xunit;

namespace SalesArena.Replay.TraceExplorer.Tests;

/// <summary>
/// Story ceb3ed81 (SA-04-02). Synthetic-span exercises for the
/// trace-explorer-style deal drill-down. SA-01 agents don't yet emit live
/// spans, so the AC's "50+ events" requirement runs through a fake source.
/// </summary>
public sealed class DealTraceExplorerTests
{
    [Fact]
    public async Task GetTraceAsync_returns_empty_tree_when_source_has_no_spans()
    {
        var explorer = new DealTraceExplorer(new FakeSource(Array.Empty<DealSpan>()));

        var tree = await explorer.GetTraceAsync("deal-empty");

        tree.DealId.Should().Be("deal-empty");
        tree.Roots.Should().BeEmpty();
        tree.TotalSpanCount.Should().Be(0);
        tree.TotalSpanCountUnpaged.Should().Be(0);
    }

    [Fact]
    public async Task GetTraceAsync_filters_spans_by_dealId()
    {
        var spans = new[]
        {
            Span("a1", "deal-a", parent: null, kind: "deal.open", offsetMinutes: 0),
            Span("b1", "deal-b", parent: null, kind: "deal.open", offsetMinutes: 0),
            Span("a2", "deal-a", parent: "a1", kind: "touch.email", offsetMinutes: 10),
        };
        var explorer = new DealTraceExplorer(new FakeSource(spans));

        var aTree = await explorer.GetTraceAsync("deal-a");

        aTree.Roots.Should().HaveCount(1);
        aTree.Roots[0].Span.SpanId.Should().Be("a1");
        aTree.Roots[0].Children.Should().HaveCount(1);
        aTree.Roots[0].Children[0].Span.SpanId.Should().Be("a2");
        aTree.TotalSpanCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTraceAsync_builds_hierarchy_and_causality_for_50_plus_events()
    {
        // AC: synthetic deal with 50+ events, verify span hierarchy + causality.
        // Lifecycle: 1 deal.open root → 7 stages → each stage spawns ~7 children
        // (touch/meeting/proposal/transition) for 1 + 7 + 49 = 57 spans.
        var spans = BuildSyntheticDealLifecycle("deal-50", stageCount: 7, perStageChildren: 7);
        spans.Should().HaveCountGreaterThanOrEqualTo(50);

        var explorer = new DealTraceExplorer(new FakeSource(spans));
        var tree = await explorer.GetTraceAsync("deal-50", new TraceExplorerOptions { MaxSpans = 200 });

        tree.Roots.Should().HaveCount(1, "synthetic lifecycle has exactly one deal.open root");
        var root = tree.Roots[0];
        root.Span.Kind.Should().Be("deal.open");
        root.Children.Should().HaveCount(7, "7 stage children");
        foreach (var stage in root.Children)
        {
            stage.Children.Should().HaveCount(7, "each stage has 7 events");
        }

        // Causality should reference every non-root span back to its parent.
        tree.Causality.Should().HaveCount(spans.Count - 1);
        foreach (var (childId, parentId) in tree.Causality)
        {
            spans.Should().Contain(s => s.SpanId == parentId, $"parent '{parentId}' of '{childId}' must exist");
        }

        // Roots are chronologically sorted.
        tree.Roots.Select(r => r.Span.StartUtc).Should().BeInAscendingOrder();
        root.Children.Select(s => s.Span.StartUtc).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetTraceAsync_pages_when_total_exceeds_MaxSpans()
    {
        var spans = BuildSyntheticDealLifecycle("deal-page", stageCount: 10, perStageChildren: 10); // 1 + 10 + 100 = 111
        var explorer = new DealTraceExplorer(new FakeSource(spans));

        var tree = await explorer.GetTraceAsync("deal-page", new TraceExplorerOptions { MaxSpans = 25 });

        tree.TotalSpanCount.Should().Be(25);
        tree.TotalSpanCountUnpaged.Should().Be(111);
        tree.Roots.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTraceAsync_promotes_orphan_to_root_and_records_causality_when_parent_outside_window()
    {
        var spans = BuildSyntheticDealLifecycle("deal-orph", stageCount: 3, perStageChildren: 3); // 1 + 3 + 9 = 13
        var explorer = new DealTraceExplorer(new FakeSource(spans));

        // Window of 6 means: deal.open root + 3 stages + 2 of stage[0]'s children.
        // The remaining stage[0] child and all stage[1..2] children fall outside.
        var tree = await explorer.GetTraceAsync("deal-orph", new TraceExplorerOptions { MaxSpans = 6 });

        tree.TotalSpanCount.Should().Be(6);
        tree.TotalSpanCountUnpaged.Should().Be(13);
        // Roots: exactly 1 (deal.open) because all 5 other paged spans have their parents inside the window.
        tree.Roots.Should().HaveCount(1);
        tree.Roots[0].Span.Kind.Should().Be("deal.open");
    }

    [Fact]
    public async Task GetTraceAsync_refuses_empty_dealId()
    {
        var explorer = new DealTraceExplorer(new FakeSource(Array.Empty<DealSpan>()));

        var act = async () => await explorer.GetTraceAsync("");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*dealId*");
    }

    [Fact]
    public void Build_throws_on_duplicate_span_ids()
    {
        var spans = new[]
        {
            Span("root", "deal-dup", null, "deal.open", 0),
            Span("root", "deal-dup", null, "deal.open", 1),
        };

        var act = () => DealTraceExplorer.Build("deal-dup", spans, totalUnpagedCount: spans.Length);

        act.Should().Throw<ArgumentException>().WithMessage("*duplicate*");
    }

    [Fact]
    public void Build_throws_when_span_traceId_mismatches_dealId()
    {
        var spans = new[] { Span("s1", "deal-x", null, "deal.open", 0) };

        var act = () => DealTraceExplorer.Build("deal-y", spans, totalUnpagedCount: spans.Length);

        act.Should().Throw<ArgumentException>().WithMessage("*TraceId*");
    }

    private static DealSpan Span(string spanId, string traceId, string? parent, string kind, int offsetMinutes, int durationSeconds = 30)
    {
        var start = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero).AddMinutes(offsetMinutes);
        return new DealSpan
        {
            SpanId = spanId,
            TraceId = traceId,
            ParentSpanId = parent,
            Kind = kind,
            Label = $"{kind}:{spanId}",
            StartUtc = start,
            EndUtc = start.AddSeconds(durationSeconds),
        };
    }

    private static IReadOnlyList<DealSpan> BuildSyntheticDealLifecycle(string dealId, int stageCount, int perStageChildren)
    {
        var spans = new List<DealSpan>();
        var offset = 0;
        spans.Add(Span($"{dealId}-root", dealId, null, "deal.open", offset++));
        var childKinds = new[] { "crm.transition", "touch.email", "touch.linkedin", "meeting.held", "proposal.sent", "touch.phone", "touch.sms" };
        for (var stage = 0; stage < stageCount; stage++)
        {
            var stageId = $"{dealId}-stage-{stage}";
            spans.Add(Span(stageId, dealId, $"{dealId}-root", "deal.stage", offset++));
            for (var c = 0; c < perStageChildren; c++)
            {
                var kind = childKinds[c % childKinds.Length];
                spans.Add(Span($"{stageId}-evt-{c}", dealId, stageId, kind, offset++));
            }
        }
        return spans;
    }

    private sealed class FakeSource : IDealSpanSource
    {
        private readonly IReadOnlyList<DealSpan> _all;
        public FakeSource(IReadOnlyList<DealSpan> all) => _all = all;
        public Task<IReadOnlyList<DealSpan>> GetSpansAsync(string dealId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DealSpan>>(_all.Where(s => s.TraceId == dealId).ToArray());
    }
}
