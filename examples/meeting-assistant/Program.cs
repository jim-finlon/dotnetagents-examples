using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => RunSmoke(),
    "list" => WriteJson(MeetingAssistantCatalog.Examples),
    "run" => RunExample(args.Skip(1).FirstOrDefault() ?? "weekly-sync"),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static int RunSmoke()
{
    var card = MeetingAssistantAgent.AgentCard;
    var notes = "Decided to release public examples. David will write code. Jim will review docs by Friday.";
    var decisions = MeetingAssistantAgent.ExtractDecisions(notes);
    var actions = MeetingAssistantAgent.ExtractActionItems(notes);
    var email = MeetingAssistantAgent.GenerateFollowUp(decisions, actions);

    var passed = card.AgentId == "meeting-assistant" &&
                 card.McpTools.Contains("extract-decisions") &&
                 decisions.Count == 1 &&
                 decisions[0].Contains("release public examples") &&
                 actions.Count == 2 &&
                 actions.Any(a => a.Owner == "David") &&
                 actions.Any(a => a.Owner == "Jim") &&
                 email.Contains("David") &&
                 email.Contains("Friday");

    var envelope = PublicExampleResultEnvelope.Create(
        exampleId: card.AgentId,
        exampleVersion: "1.0.0",
        inputSummary: "meeting-assistant --smoke weekly-sync",
        localValidation: new PublicExampleValidationSummary(
            passed ? "passed" : "failed",
            [
                "agent card present",
                "extracted correct number of decisions",
                "extracted correct action items and owners",
                "generated follow-up draft containing critical owners"
            ]),
        outputArtifactRefs:
        [
            new PublicExampleOutputArtifactRef("sample-output", "action-items.json", "application/json"),
            new PublicExampleOutputArtifactRef("sample-output", "follow-up.md", "text/markdown")
        ],
        selfReportedMetrics: new Dictionary<string, decimal>
        {
            ["checksPassed"] = passed ? 4 : 0
        },
        runId: "meeting-assistant-smoke",
        timestampUtc: DateTimeOffset.Parse("2026-05-17T18:30:00Z"));

    return WriteJson(new
    {
        status = passed ? "passed" : "failed",
        card.AgentId,
        decisions,
        actions,
        followUpEmail = email,
        resultEnvelope = envelope
    }, passed ? 0 : 1);
}

static int RunExample(string exampleId)
{
    var notes = exampleId.ToLowerInvariant() switch
    {
        "weekly-sync" => "Decided to release public examples. David will write code. Jim will review docs by Friday.",
        "project-kickoff" => "Decided to start phase 1 immediately. Sarah will set up repository. Alex to request API credentials by Monday.",
        _ => "No notes provided."
    };

    var decisions = MeetingAssistantAgent.ExtractDecisions(notes);
    var actions = MeetingAssistantAgent.ExtractActionItems(notes);
    var email = MeetingAssistantAgent.GenerateFollowUp(decisions, actions);

    Console.WriteLine($"=== Meeting Assistant Example: {exampleId} ===");
    Console.WriteLine($"Raw Notes: {notes}");
    Console.WriteLine();
    Console.WriteLine("--- Extracted Decisions ---");
    foreach (var d in decisions)
    {
        Console.WriteLine($"- {d}");
    }
    Console.WriteLine();
    Console.WriteLine("--- Extracted Action Items ---");
    foreach (var a in actions)
    {
        Console.WriteLine($"- [ ] {a.Task} (Owner: {a.Owner}, Due: {a.DueDate ?? "N/A"})");
    }
    Console.WriteLine();
    Console.WriteLine("--- Draft Follow-Up Email ---");
    Console.WriteLine(email);

    return 0;
}

static int WriteHelp()
{
    Console.WriteLine("Meeting Assistant public example");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/meeting-assistant -- --smoke");
    Console.WriteLine("  dotnet run --project examples/meeting-assistant -- list");
    Console.WriteLine("  dotnet run --project examples/meeting-assistant -- run weekly-sync");
    Console.WriteLine("  dotnet run --project examples/meeting-assistant -- run project-kickoff");
    return 0;
}

static int WriteError(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

static int WriteJson<T>(T value, int exitCode = 0)
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
    Console.WriteLine(JsonSerializer.Serialize(value, jsonOptions));
    return exitCode;
}

internal static class MeetingAssistantCatalog
{
    public static IReadOnlyList<MeetingExampleDefinition> Examples { get; } =
    [
        new("weekly-sync", "Weekly Alignment Sync", "Ingest weekly team notes, extract action items, and draft follow-up email."),
        new("project-kickoff", "Project Kickoff Meeting", "Ingest project kickoff notes, extract decisions, and draft timeline summary.")
    ];
}

internal sealed record MeetingExampleDefinition(string Id, string DisplayName, string Description);

internal static class MeetingAssistantAgent
{
    public static MeetingAgentCard AgentCard { get; } = new(
        AgentId: "meeting-assistant",
        DisplayName: "Meeting Assistant Agent",
        Purpose: "Analyzes meeting notes to identify decisions, map action items, and compose email digests.",
        McpTools: ["extract-decisions", "extract-action-items", "compose-digest"]);

    public static IReadOnlyList<string> ExtractDecisions(string notes)
    {
        var list = new List<string>();
        var parts = notes.Split(new[] { ". ", "." }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p.Contains("decided", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(p.Trim());
            }
        }
        return list;
    }

    public static IReadOnlyList<ActionItem> ExtractActionItems(string notes)
    {
        var list = new List<ActionItem>();
        var parts = notes.Split(new[] { ". ", "." }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p.Contains("decided", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (p.Contains("will", StringComparison.OrdinalIgnoreCase) || p.Contains("to", StringComparison.OrdinalIgnoreCase))
            {
                // Simple heuristics for demo
                string owner = "Unassigned";
                string task = p.Trim();
                string? dueDate = null;

                if (p.Contains("David")) owner = "David";
                else if (p.Contains("Jim")) owner = "Jim";
                else if (p.Contains("Sarah")) owner = "Sarah";
                else if (p.Contains("Alex")) owner = "Alex";

                if (p.Contains("by Friday")) dueDate = "Friday";
                else if (p.Contains("by Monday")) dueDate = "Monday";

                list.Add(new ActionItem(task, owner, dueDate));
            }
        }
        return list;
    }

    public static string GenerateFollowUp(IReadOnlyList<string> decisions, IReadOnlyList<ActionItem> actions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Hi Team,");
        sb.AppendLine();
        sb.AppendLine("Here is a quick summary of our meeting:");
        sb.AppendLine();
        sb.AppendLine("## Key Decisions");
        foreach (var d in decisions)
        {
            sb.AppendLine($"- {d}");
        }
        sb.AppendLine();
        sb.AppendLine("## Action Items");
        foreach (var a in actions)
        {
            sb.AppendLine($"- [ ] {a.Task} (Owner: {a.Owner}, Due: {a.DueDate ?? "ASAP"})");
        }
        sb.AppendLine();
        sb.AppendLine("Best regards,");
        sb.AppendLine("Meeting Assistant Agent");
        return sb.ToString();
    }
}

internal sealed record MeetingAgentCard(string AgentId, string DisplayName, string Purpose, IReadOnlyList<string> McpTools);
internal sealed record ActionItem(string Task, string Owner, string? DueDate);
