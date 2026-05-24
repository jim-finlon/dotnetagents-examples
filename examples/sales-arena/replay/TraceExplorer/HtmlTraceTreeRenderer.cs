using System.Globalization;
using System.Net;
using System.Text;

namespace SalesArena.Replay.TraceExplorer;

/// <summary>
/// Story ceb3ed81 (SA-04-02). Semantic HTML span-tree renderer for embedding
/// into the Manager UI (SA-03-05). Nested &lt;ul&gt;/&lt;li&gt; with data-*
/// attributes the JS side can hydrate; no inline styles — host CSS owns the
/// presentation.
/// </summary>
public sealed class HtmlTraceTreeRenderer
{
    /// <summary>Render the tree as an HTML fragment (no &lt;html&gt;/&lt;body&gt; wrapper).</summary>
    public string Render(TraceTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var sb = new StringBuilder();

        sb.Append("<section class=\"sa-deal-trace\" data-deal-id=\"")
            .Append(WebUtility.HtmlEncode(tree.DealId))
            .Append("\" data-span-count=\"")
            .Append(tree.TotalSpanCount.ToString(CultureInfo.InvariantCulture))
            .Append("\" data-span-count-unpaged=\"")
            .Append(tree.TotalSpanCountUnpaged.ToString(CultureInfo.InvariantCulture))
            .AppendLine("\">");

        sb.Append("  <header class=\"sa-deal-trace__header\">Deal ")
            .Append(WebUtility.HtmlEncode(tree.DealId))
            .Append(" &mdash; ")
            .Append(tree.TotalSpanCount.ToString(CultureInfo.InvariantCulture))
            .Append(" span(s) shown");
        if (tree.TotalSpanCountUnpaged > tree.TotalSpanCount)
        {
            sb.Append(" of ")
                .Append(tree.TotalSpanCountUnpaged.ToString(CultureInfo.InvariantCulture))
                .Append(" total <button class=\"sa-deal-trace__load-more\" type=\"button\">load more</button>");
        }
        sb.AppendLine("</header>");

        sb.AppendLine("  <ul class=\"sa-deal-trace__tree\" role=\"tree\">");
        foreach (var root in tree.Roots)
            RenderNode(sb, root, depth: 1);
        sb.AppendLine("  </ul>");
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    private static void RenderNode(StringBuilder sb, TraceTreeNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent).Append("<li class=\"sa-deal-trace__node\" role=\"treeitem\" data-span-id=\"")
            .Append(WebUtility.HtmlEncode(node.Span.SpanId))
            .Append("\" data-span-kind=\"")
            .Append(WebUtility.HtmlEncode(node.Span.Kind))
            .Append("\" data-start-utc=\"")
            .Append(node.Span.StartUtc.ToString("o", CultureInfo.InvariantCulture))
            .Append("\" data-end-utc=\"")
            .Append(node.Span.EndUtc.ToString("o", CultureInfo.InvariantCulture))
            .AppendLine("\">");

        sb.Append(indent).Append("  <span class=\"sa-deal-trace__kind\">")
            .Append(WebUtility.HtmlEncode(node.Span.Kind))
            .AppendLine("</span>");
        sb.Append(indent).Append("  <span class=\"sa-deal-trace__label\">")
            .Append(WebUtility.HtmlEncode(node.Span.Label))
            .AppendLine("</span>");
        sb.Append(indent).Append("  <span class=\"sa-deal-trace__duration\">")
            .Append(WebUtility.HtmlEncode(FormatDuration(node.Span.Duration)))
            .AppendLine("</span>");

        if (node.Children.Count > 0)
        {
            sb.Append(indent).AppendLine("  <ul class=\"sa-deal-trace__children\" role=\"group\">");
            foreach (var child in node.Children)
                RenderNode(sb, child, depth + 2);
            sb.Append(indent).AppendLine("  </ul>");
        }
        sb.Append(indent).AppendLine("</li>");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMilliseconds(1))
            return string.Format(CultureInfo.InvariantCulture, "{0}μs", (int)(duration.TotalMilliseconds * 1000));
        if (duration < TimeSpan.FromSeconds(1))
            return string.Format(CultureInfo.InvariantCulture, "{0}ms", (int)duration.TotalMilliseconds);
        if (duration < TimeSpan.FromMinutes(1))
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0}s", duration.TotalSeconds);
        if (duration < TimeSpan.FromHours(1))
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0}m", duration.TotalMinutes);
        if (duration < TimeSpan.FromDays(1))
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0}h", duration.TotalHours);
        return string.Format(CultureInfo.InvariantCulture, "{0:0.0}d", duration.TotalDays);
    }
}
