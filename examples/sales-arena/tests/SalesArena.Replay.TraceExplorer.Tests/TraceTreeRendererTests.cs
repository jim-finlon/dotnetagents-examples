using FluentAssertions;
using SalesArena.Replay.TraceExplorer;
using Xunit;

namespace SalesArena.Replay.TraceExplorer.Tests;

public sealed class TraceTreeRendererTests
{
    [Fact]
    public void Text_renderer_emits_indented_ascii_tree_with_durations()
    {
        var tree = BuildTwoLevelTree("deal-r1");
        var output = new TextTraceTreeRenderer().Render(tree);

        output.Should().Contain("Deal deal-r1");
        output.Should().Contain("├─ [deal.open]");
        output.Should().Contain("│  └─ [touch.email]");
        output.Should().Contain("└─ [deal.stage]");
        output.Should().Contain("30.0s");
    }

    [Fact]
    public void Text_renderer_emits_load_more_marker_when_paged()
    {
        var tree = BuildTwoLevelTree("deal-paged", totalUnpaged: 99);
        var output = new TextTraceTreeRenderer().Render(tree);

        output.Should().Contain("load more");
        output.Should().Contain("of 99 total");
    }

    [Fact]
    public void Html_renderer_emits_semantic_tree_with_data_attributes()
    {
        var tree = BuildTwoLevelTree("deal-html");
        var output = new HtmlTraceTreeRenderer().Render(tree);

        output.Should().Contain("<section class=\"sa-deal-trace\" data-deal-id=\"deal-html\"");
        output.Should().Contain("role=\"tree\"");
        output.Should().Contain("data-span-id=\"r1\"");
        output.Should().Contain("data-span-kind=\"deal.open\"");
        // Nested ul indicates the child node was rendered under its parent.
        output.Should().Contain("<ul class=\"sa-deal-trace__children\"");
    }

    [Fact]
    public void Html_renderer_escapes_dynamic_attribute_values()
    {
        // Cover an injection-style label to ensure no raw '<' makes it into output.
        var hostileSpan = new DealSpan
        {
            SpanId = "evil",
            TraceId = "deal-x",
            ParentSpanId = null,
            Kind = "<script>",
            Label = "alert(1)",
            StartUtc = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 5, 18, 12, 0, 30, TimeSpan.Zero),
        };
        var tree = DealTraceExplorer.Build("deal-x", new[] { hostileSpan }, totalUnpagedCount: 1);

        var output = new HtmlTraceTreeRenderer().Render(tree);

        output.Should().NotContain("<script>");
        output.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Html_renderer_emits_load_more_button_when_paged()
    {
        var tree = BuildTwoLevelTree("deal-html-paged", totalUnpaged: 200);
        var output = new HtmlTraceTreeRenderer().Render(tree);

        output.Should().Contain("class=\"sa-deal-trace__load-more\"");
        output.Should().Contain("data-span-count-unpaged=\"200\"");
    }

    private static TraceTree BuildTwoLevelTree(string dealId, int? totalUnpaged = null)
    {
        var spans = new List<DealSpan>
        {
            new() {
                SpanId = "r1", TraceId = dealId, Kind = "deal.open", Label = "deal opens",
                StartUtc = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 5, 18, 12, 0, 30, TimeSpan.Zero),
            },
            new() {
                SpanId = "r1c1", TraceId = dealId, ParentSpanId = "r1", Kind = "touch.email", Label = "first email",
                StartUtc = new DateTimeOffset(2026, 5, 18, 12, 5, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 5, 18, 12, 5, 30, TimeSpan.Zero),
            },
            new() {
                SpanId = "r2", TraceId = dealId, Kind = "deal.stage", Label = "qualify",
                StartUtc = new DateTimeOffset(2026, 5, 18, 12, 10, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 5, 18, 12, 10, 30, TimeSpan.Zero),
            },
        };
        return DealTraceExplorer.Build(dealId, spans, totalUnpaged ?? spans.Count);
    }
}
