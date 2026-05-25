using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => RunSmoke(),
    "list" => WriteJson(CustomerSupportCatalog.Examples),
    "run" => RunExample(args.Skip(1).FirstOrDefault() ?? "billing-issue"),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static int RunSmoke()
{
    var card = CustomerSupportAgent.AgentCard;
    var triage = CustomerSupportAgent.TriageTicket("billing-issue", "I was charged twice for my subscription this month.");
    var lookup = CustomerSupportAgent.LookupKnowledge("billing-issue");
    var escalation = CustomerSupportAgent.DecideEscalation(triage, lookup);
    var transcript = CustomerSupportAgent.GenerateTranscript(triage, lookup, escalation);

    var passed = card.AgentId == "customer-support-triage" &&
                 card.McpTools.Contains("triage") &&
                 triage.Category == "billing" &&
                 triage.Urgency == "high" &&
                 lookup.Contains("Double Charge") &&
                 escalation.ShouldEscalate &&
                 transcript.TranscriptText.Contains("billing");

    var envelope = PublicExampleResultEnvelope.Create(
        exampleId: card.AgentId,
        exampleVersion: "1.0.0",
        inputSummary: "customer-support-triage --smoke billing-issue",
        localValidation: new PublicExampleValidationSummary(
            passed ? "passed" : "failed",
            [
                "agent card present",
                "ticket triage categorization correct",
                "knowledge lookup completed",
                "escalation decision correct",
                "transcript generated successfully"
            ]),
        outputArtifactRefs:
        [
            new PublicExampleOutputArtifactRef("sample-output", "triage.json", "application/json"),
            new PublicExampleOutputArtifactRef("sample-output", "transcript.md", "text/markdown")
        ],
        selfReportedMetrics: new Dictionary<string, decimal>
        {
            ["checksPassed"] = passed ? 5 : 0
        },
        runId: "customer-support-triage-smoke",
        timestampUtc: DateTimeOffset.Parse("2026-05-17T18:25:00Z"));

    return WriteJson(new
    {
        status = passed ? "passed" : "failed",
        card.AgentId,
        triage,
        lookup,
        escalation,
        transcript,
        resultEnvelope = envelope
    }, passed ? 0 : 1);
}

static int RunExample(string ticketId)
{
    var (subject, description) = ticketId.ToLowerInvariant() switch
    {
        "billing-issue" => ("Double charge", "I was charged twice for my subscription this month."),
        "login-error" => ("Cannot log in", "Getting 500 error when clicking sign-in button."),
        _ => ("General query", "How do I upgrade my account?")
    };

    var triage = CustomerSupportAgent.TriageTicket(ticketId, description);
    var lookup = CustomerSupportAgent.LookupKnowledge(ticketId);
    var escalation = CustomerSupportAgent.DecideEscalation(triage, lookup);
    var transcript = CustomerSupportAgent.GenerateTranscript(triage, lookup, escalation);

    Console.WriteLine($"=== Customer Support Triage: {ticketId} ===");
    Console.WriteLine($"Subject: {subject}");
    Console.WriteLine($"Description: {description}");
    Console.WriteLine();
    Console.WriteLine($"Triage Category: {triage.Category}");
    Console.WriteLine($"Triage Urgency: {triage.Urgency}");
    Console.WriteLine($"Escalation Flag: {escalation.ShouldEscalate} (Reason: {escalation.Reason})");
    Console.WriteLine();
    Console.WriteLine("--- KB Lookup Stub ---");
    Console.WriteLine(lookup);
    Console.WriteLine();
    Console.WriteLine("--- Final Transcript Summary ---");
    Console.WriteLine(transcript.TranscriptText);

    return 0;
}

static int WriteHelp()
{
    Console.WriteLine("Customer support triage public example");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/customer-support -- --smoke");
    Console.WriteLine("  dotnet run --project examples/customer-support -- list");
    Console.WriteLine("  dotnet run --project examples/customer-support -- run billing-issue");
    Console.WriteLine("  dotnet run --project examples/customer-support -- run login-error");
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

internal static class CustomerSupportCatalog
{
    public static IReadOnlyList<SupportExampleDefinition> Examples { get; } =
    [
        new("billing-issue", "Billing Double Charge Triage", "Triage double billing issue, resolve/escalate, output transcript."),
        new("login-error", "Login System Failure Triage", "Triage sign-in server issue, resolve/escalate, output transcript.")
    ];
}

internal sealed record SupportExampleDefinition(string Id, string DisplayName, string Description);

internal static class CustomerSupportAgent
{
    public static SupportAgentCard AgentCard { get; } = new(
        AgentId: "customer-support-triage",
        DisplayName: "Customer Support Triage Agent",
        Purpose: "Triages support tickets, stubs kb check, and determines escalation workflows.",
        McpTools: ["triage", "lookup", "escalate"]);

    public static TicketTriage TriageTicket(string ticketId, string description)
    {
        var category = "general";
        var urgency = "low";

        if (description.Contains("charge", StringComparison.OrdinalIgnoreCase) || 
            description.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("subscription", StringComparison.OrdinalIgnoreCase))
        {
            category = "billing";
            urgency = "high";
        }
        else if (description.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                 description.Contains("sign-in", StringComparison.OrdinalIgnoreCase) ||
                 description.Contains("500", StringComparison.OrdinalIgnoreCase))
        {
            category = "technical";
            urgency = "high";
        }

        return new TicketTriage(ticketId, category, urgency);
    }

    public static string LookupKnowledge(string ticketId)
    {
        return ticketId.ToLowerInvariant() switch
        {
            "billing-issue" => "KB Article #109: Double Charge Policy. Refund the duplicate transaction and confirm billing cycle alignment.",
            "login-error" => "KB Article #402: Authentication Service Outage. Escalated to DevOps if system status is red.",
            _ => "KB Article #001: General Support Escalation Guidelines."
        };
    }

    public static EscalationDecision DecideEscalation(TicketTriage triage, string kbLookup)
    {
        if (triage.Urgency == "high")
        {
            return new EscalationDecision(true, $"High urgency category '{triage.Category}' requires tier-2 operations triage.");
        }
        return new EscalationDecision(false, "Resolved at tier-1 via general KB guidelines.");
    }

    public static SupportTranscript GenerateTranscript(TicketTriage triage, string kbLookup, EscalationDecision escalation)
    {
        var text = $"TICKET: {triage.TicketId}\n" +
                   $"CATEGORY: {triage.Category}\n" +
                   $"URGENCY: {triage.Urgency}\n" +
                   $"KB_RESOLUTION: {kbLookup}\n" +
                   $"ESCALATED: {escalation.ShouldEscalate} ({escalation.Reason})";

        return new SupportTranscript(triage.TicketId, text);
    }
}

internal sealed record SupportAgentCard(string AgentId, string DisplayName, string Purpose, IReadOnlyList<string> McpTools);
internal sealed record TicketTriage(string TicketId, string Category, string Urgency);
internal sealed record EscalationDecision(bool ShouldEscalate, string Reason);
internal sealed record SupportTranscript(string TicketId, string TranscriptText);
