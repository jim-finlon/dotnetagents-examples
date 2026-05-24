// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Orchestrator.Narration;
using SalesArena.Replay;

namespace SalesArena.Cli.Commands;

internal static class ArenaCliBackend
{
    public const string DefaultWorkspaceDirectory = ".arena";
    public const string DefaultLedgerFileName = "ledger.db";
    public const string LeadPoolPersona = "__lead_pool__";

    public static string ResolveLedgerConnectionString(DirectoryInfo? workspace)
    {
        var root = workspace?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), DefaultWorkspaceDirectory);
        Directory.CreateDirectory(root);
        return $"Data Source={Path.Combine(root, DefaultLedgerFileName)}";
    }

    public static async Task<int> BootstrapLedgerAsync(
        FileInfo leads,
        int uiPort,
        int bellPort,
        DirectoryInfo? workspace,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var leadIds = ReadLeadIds(leads);
        await using var ledger = new SqliteArenaLedger(ResolveLedgerConnectionString(workspace));
        var contestId = Path.GetFileNameWithoutExtension(leads.Name);
        var now = DateTimeOffset.UtcNow;
        var events = new List<ArenaEvent>
        {
            NewEvent(
                contestId,
                ArenaEventKinds.ContestPhaseChanged,
                now,
                new ContestPhaseChangedPayload("Init", $"lead-pack:{leads.FullName};ui:{uiPort};bell:{bellPort}")),
        };

        events.AddRange(leadIds.Select((leadId, index) => NewEvent(
            contestId,
            ArenaEventKinds.LeadAssigned,
            now.AddMilliseconds(index + 1),
            new LeadAssignedPayload(leadId, LeadPoolPersona, leads.FullName),
            leadId,
            LeadPoolPersona)));

        await ledger.AppendManyAsync(events, cancellationToken).ConfigureAwait(false);
        stdout.WriteLine($"dna-arena init: ledger bootstrapped at {ResolveLedgerPath(workspace)} with {leadIds.Count} leads.");
        return leadIds.Count;
    }

    public static async Task AppendContestPhaseAsync(
        string contest,
        string phase,
        string? reason,
        DirectoryInfo? workspace,
        CancellationToken cancellationToken)
    {
        await using var ledger = new SqliteArenaLedger(ResolveLedgerConnectionString(workspace));
        await ledger.AppendAsync(
            NewEvent(contest, ArenaEventKinds.ContestPhaseChanged, DateTimeOffset.UtcNow, new ContestPhaseChangedPayload(phase, reason)),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Leaderboard> ComputeLeaderboardAsync(
        string contest,
        DirectoryInfo? workspace,
        CancellationToken cancellationToken)
    {
        await using var ledger = new SqliteArenaLedger(ResolveLedgerConnectionString(workspace));
        var leaderboard = new LeaderboardEngine(ledger);
        return await leaderboard.ComputeAsync(contest, new RevenueScoring(), DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<ArenaCue?> RingBellAsync(
        string contest,
        string? persona,
        DirectoryInfo? workspace,
        CancellationToken cancellationToken)
    {
        await using var ledger = new SqliteArenaLedger(ResolveLedgerConnectionString(workspace));
        var payload = new BellRungPayload(
            Reason: "operator_cli",
            Persona: persona,
            LeadId: null,
            NarrationLine: $"{persona ?? "the floor"} gets the bell.");
        var evt = await ledger.AppendAsync(
            NewEvent(contest, ArenaEventKinds.BellRung, DateTimeOffset.UtcNow, payload, persona: persona),
            cancellationToken).ConfigureAwait(false);

        var narrator = new StubArenaNarrator();
        var resolver = new InMemoryCueScriptResolver(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [CueKinds.BellRung] = new[] { "{persona}: {reason}. {line}" },
        });
        var cueEngine = new NarrationCueEngine(narrator, resolver);
        var result = await cueEngine.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
        return result.Cue;
    }

    public static async Task<ReplayReport> GenerateReplayAsync(
        string contest,
        DirectoryInfo? workspace,
        FileInfo? output,
        CancellationToken cancellationToken)
    {
        await using var ledger = new SqliteArenaLedger(ResolveLedgerConnectionString(workspace));
        var generator = new ReplayGenerator(ledger, new LeaderboardEngine(ledger));
        var options = new ReplayOptions(contest, new RevenueScoring(), DateTimeOffset.UtcNow);
        if (output is null)
        {
            return await generator.GenerateAsync(options, cancellationToken).ConfigureAwait(false);
        }

        return await generator.ExportToFileAsync(options, output.FullName, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ReadLeadIds(FileInfo leads)
    {
        using var stream = leads.OpenRead();
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("leads", out var leadsElement) || leadsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("lead-pack must contain a 'leads' array.");
        }

        var ids = new List<string>();
        foreach (var lead in leadsElement.EnumerateArray())
        {
            if (lead.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                var value = id.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ids.Add(value);
                }
            }
        }

        return ids;
    }

    private static string ResolveLedgerPath(DirectoryInfo? workspace)
    {
        var root = workspace?.FullName ?? Path.Combine(Directory.GetCurrentDirectory(), DefaultWorkspaceDirectory);
        return Path.Combine(root, DefaultLedgerFileName);
    }

    private static ArenaEvent NewEvent<TPayload>(
        string contestId,
        string kind,
        DateTimeOffset occurredAtUtc,
        TPayload payload,
        string? leadId = null,
        string? persona = null)
        where TPayload : class =>
        new()
        {
            ContestId = contestId,
            Kind = kind,
            OccurredAtUtc = occurredAtUtc,
            LeadId = leadId,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        };
}
