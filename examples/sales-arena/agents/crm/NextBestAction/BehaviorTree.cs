using System;
using System.Collections.Generic;

namespace SalesArena.Crm.NextBestAction;

public enum NodeStatus { Success, Failure }

public sealed record NodeResult(NodeStatus Status, NbaActionType? Action, string Reason);

public interface INbaNode
{
    string Name { get; }
    NodeResult Evaluate(CrmContext context, IList<string> trace);
}

/// <summary>Selector returns the first child that yields Success.</summary>
public sealed class Selector : INbaNode
{
    public string Name { get; }
    private readonly IReadOnlyList<INbaNode> _children;

    public Selector(string name, params INbaNode[] children)
    {
        Name = name;
        _children = children;
    }

    public NodeResult Evaluate(CrmContext context, IList<string> trace)
    {
        trace.Add($"Selector({Name}) enter");
        foreach (var child in _children)
        {
            var result = child.Evaluate(context, trace);
            if (result.Status == NodeStatus.Success)
            {
                trace.Add($"Selector({Name}) success via {child.Name}");
                return result;
            }
        }
        trace.Add($"Selector({Name}) failure");
        return new NodeResult(NodeStatus.Failure, null, $"No selector branch matched for {Name}.");
    }
}

/// <summary>Conditioned action: when the predicate is true, return Success with the configured action.</summary>
public sealed class Condition : INbaNode
{
    public string Name { get; }
    private readonly Func<CrmContext, bool> _predicate;
    private readonly NbaActionType _action;
    private readonly string _reason;

    public Condition(string name, Func<CrmContext, bool> predicate, NbaActionType action, string reason)
    {
        Name = name;
        _predicate = predicate;
        _action = action;
        _reason = reason;
    }

    public NodeResult Evaluate(CrmContext context, IList<string> trace)
    {
        if (_predicate(context))
        {
            trace.Add($"Condition({Name}) → {_action}");
            return new NodeResult(NodeStatus.Success, _action, _reason);
        }
        trace.Add($"Condition({Name}) skipped");
        return new NodeResult(NodeStatus.Failure, null, _reason);
    }
}

/// <summary>Always succeeds with the given action; use as the default leaf.</summary>
public sealed class FallbackAction : INbaNode
{
    public string Name { get; }
    private readonly NbaActionType _action;
    private readonly string _reason;

    public FallbackAction(string name, NbaActionType action, string reason)
    {
        Name = name;
        _action = action;
        _reason = reason;
    }

    public NodeResult Evaluate(CrmContext context, IList<string> trace)
    {
        trace.Add($"Fallback({Name}) → {_action}");
        return new NodeResult(NodeStatus.Success, _action, _reason);
    }
}
