using System.Globalization;
using System.Text;

namespace SalesArena.Replay.TraceExplorer;

/// <summary>
/// Story ceb3ed81 (SA-04-02). ASCII span-tree renderer with indent + duration,
/// suitable for terminal output and SDLC story evidence pastes.
/// </summary>
public sealed class TextTraceTreeRenderer
{
    /// <summary>Render the tree as a multi-line ASCII string.</summary>
    public string Render(TraceTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var sb = new StringBuilder();
        sb.Append("Deal ").Append(tree.DealId).Append(" — ")
            .Append(tree.TotalSpanCount).Append(" span(s) shown");
        if (tree.TotalSpanCountUnpaged > tree.TotalSpanCount)
        {
            sb.Append(" of ").Append(tree.TotalSpanCountUnpaged).Append(" total (load more)");
        }
        sb.AppendLine();

        for (var i = 0; i < tree.Roots.Count; i++)
        {
            var isLast = i == tree.Roots.Count - 1;
            RenderNode(sb, tree.Roots[i], prefix: "", isLast: isLast);
        }
        return sb.ToString();
    }

    private static void RenderNode(StringBuilder sb, TraceTreeNode node, string prefix, bool isLast)
    {
        var connector = isLast ? "└─ " : "├─ ";
        sb.Append(prefix).Append(connector)
            .Append('[').Append(node.Span.Kind).Append("] ")
            .Append(node.Span.Label)
            .Append(" (").Append(FormatDuration(node.Span.Duration)).Append(')')
            .AppendLine();

        var childPrefix = prefix + (isLast ? "   " : "│  ");
        for (var i = 0; i < node.Children.Count; i++)
        {
            var lastChild = i == node.Children.Count - 1;
            RenderNode(sb, node.Children[i], childPrefix, lastChild);
        }
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
