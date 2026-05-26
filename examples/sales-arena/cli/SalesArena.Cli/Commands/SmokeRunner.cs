// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNetAgents.Core.PublicExamples;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay;

namespace SalesArena.Cli.Commands;

public static class SmokeRunner
{
    public static async Task<int> RunAsync()
    {
        Console.Error.WriteLine("[Smoke Mode] Running deterministic Sales Arena teaser simulation...");

        var tempWorkspace = Path.Combine(Path.GetTempPath(), $"sales-arena-smoke-{Guid.NewGuid():N}");
        var workspaceDir = new DirectoryInfo(tempWorkspace);
        workspaceDir.Create();

        var leadsPath = Path.Combine(tempWorkspace, "leads.json");
        var leadsJson = new
        {
            leads = new[]
            {
                new { id = "L-0001" },
                new { id = "L-0002" }
            }
        };

        try
        {
            await File.WriteAllTextAsync(leadsPath, JsonSerializer.Serialize(leadsJson)).ConfigureAwait(false);

            var leadsFile = new FileInfo(leadsPath);

            // 1. Initialize workspace and ledger
            var initCode = await InitCommand.HandleAsync(
                leadsFile,
                uiPort: 5005,
                bellPort: 5006,
                workspaceDir,
                TextWriter.Null,
                TextWriter.Null,
                CancellationToken.None).ConfigureAwait(false);

            if (initCode != 0)
            {
                Console.Error.WriteLine($"[Smoke Mode] InitCommand failed with code {initCode}");
                return initCode;
            }

            // 2. Start contest
            var startCode = await ContestCommand.HandleStartAsync(
                "smoke-contest",
                "roma,levene,moss",
                hours: 1,
                compression: 60,
                workspaceDir,
                TextWriter.Null,
                TextWriter.Null,
                CancellationToken.None).ConfigureAwait(false);

            if (startCode != 0)
            {
                Console.Error.WriteLine($"[Smoke Mode] ContestCommand.start failed with code {startCode}");
                return startCode;
            }

            // 3. Append synthetic events simulating a teaser contest (all timestamps must be in the past relative to compute)
            await using (var ledger = new SqliteArenaLedger(ArenaCliBackend.ResolveLedgerConnectionString(workspaceDir)))
            {
                var now = DateTimeOffset.UtcNow;
                var events = new List<ArenaEvent>
                {
                    // Roma progression
                    NewEvent("smoke-contest", ArenaEventKinds.LeadAssigned, now.AddSeconds(-60),
                        new LeadAssignedPayload("L-0001", "roma", "manual"), "L-0001", "roma"),
                    NewEvent("smoke-contest", ArenaEventKinds.LeadResearched, now.AddSeconds(-50),
                        new LeadResearchedPayload("L-0001", "roma", 3, "brief-roma-L-0001"), "L-0001", "roma"),
                    NewEvent("smoke-contest", ArenaEventKinds.TouchSent, now.AddSeconds(-40),
                        new TouchSentPayload("L-0001", "roma", "email", "intro", "v1", "Intro email", 450), "L-0001", "roma"),
                    NewEvent("smoke-contest", ArenaEventKinds.MeetingHeld, now.AddSeconds(-30),
                        new MeetingHeldPayload("L-0001", "roma", 30, 1, 1, "meet-roma-L-0001"), "L-0001", "roma"),
                    NewEvent("smoke-contest", ArenaEventKinds.ProposalSent, now.AddSeconds(-20),
                        new ProposalSentPayload("L-0001", "roma", "pro", 48000m, "prop-roma-L-0001"), "L-0001", "roma"),
                    NewEvent("smoke-contest", ArenaEventKinds.DealClosed, now.AddSeconds(-10),
                        new DealClosedPayload("L-0001", "roma", "Won", 48000m, null), "L-0001", "roma"),
                    NewEvent("smoke-contest", ArenaEventKinds.BellRung, now.AddSeconds(-9),
                        new BellRungPayload("deal_won", "roma", "L-0001", "Roma closed L-0001 for $48,000!"), "L-0001", "roma"),

                    // Levene progression
                    NewEvent("smoke-contest", ArenaEventKinds.LeadAssigned, now.AddSeconds(-55),
                        new LeadAssignedPayload("L-0002", "levene", "manual"), "L-0002", "levene"),
                    NewEvent("smoke-contest", ArenaEventKinds.LeadResearched, now.AddSeconds(-45),
                        new LeadResearchedPayload("L-0002", "levene", 2, "brief-levene-L-0002"), "L-0002", "levene"),
                    NewEvent("smoke-contest", ArenaEventKinds.TouchSent, now.AddSeconds(-35),
                        new TouchSentPayload("L-0002", "levene", "email", "intro", "v1", "Intro email 2", 300), "L-0002", "levene"),
                    NewEvent("smoke-contest", ArenaEventKinds.MeetingHeld, now.AddSeconds(-25),
                        new MeetingHeldPayload("L-0002", "levene", 30, 1, 1, "meet-levene-L-0002"), "L-0002", "levene"),
                    NewEvent("smoke-contest", ArenaEventKinds.ProposalSent, now.AddSeconds(-15),
                        new ProposalSentPayload("L-0002", "levene", "starter", 25000m, "prop-levene-L-0002"), "L-0002", "levene"),
                    NewEvent("smoke-contest", ArenaEventKinds.DealClosed, now.AddSeconds(-5),
                        new DealClosedPayload("L-0002", "levene", "Won", 25000m, null), "L-0002", "levene"),

                    // Moss progression
                    NewEvent("smoke-contest", ArenaEventKinds.LeadAssigned, now.AddSeconds(-54),
                        new LeadAssignedPayload("L-0002", "moss", "manual"), "L-0002", "moss"),
                    NewEvent("smoke-contest", ArenaEventKinds.LeadResearched, now.AddSeconds(-44),
                        new LeadResearchedPayload("L-0002", "moss", 1, "brief-moss-L-0002"), "L-0002", "moss"),
                    NewEvent("smoke-contest", ArenaEventKinds.TouchSent, now.AddSeconds(-34),
                        new TouchSentPayload("L-0002", "moss", "email", "intro", "v1", "Intro email 3", 200), "L-0002", "moss"),
                    NewEvent("smoke-contest", ArenaEventKinds.MeetingHeld, now.AddSeconds(-24),
                        new MeetingHeldPayload("L-0002", "moss", 30, 0, 0, "meet-moss-L-0002"), "L-0002", "moss"),
                    NewEvent("smoke-contest", ArenaEventKinds.DealClosed, now.AddSeconds(-4),
                        new DealClosedPayload("L-0002", "moss", "Lost", null, "The leads are weak"), "L-0002", "moss")
                };

                await ledger.AppendManyAsync(events, CancellationToken.None).ConfigureAwait(false);
            }

            // 4. Compute leaderboard
            var leaderboard = await ArenaCliBackend.ComputeLeaderboardAsync("smoke-contest", workspaceDir, CancellationToken.None).ConfigureAwait(false);
            Console.Error.WriteLine("[Smoke Mode] Live Leaderboard computed successfully:");
            foreach (var row in leaderboard.Entries)
            {
                Console.Error.WriteLine($"  {row.Position}. {row.Persona} ({row.Tier}) - ${row.RevenueUsd:N0} (Won: {row.DealsWon}, Lost: {row.DealsLost})");
            }

            // 5. Generate Replay report
            var replay = await ArenaCliBackend.GenerateReplayAsync("smoke-contest", workspaceDir, null, CancellationToken.None).ConfigureAwait(false);
            Console.Error.WriteLine($"[Smoke Mode] Replay Markdown generated ({replay.Markdown.Length} bytes).");

            // Validate that Roma won
            var topPersona = leaderboard.Entries.FirstOrDefault()?.Persona;
            var passed = topPersona == "roma" && leaderboard.Entries.Count == 3;

            var validation = new PublicExampleValidationSummary(
                passed ? "passed" : "failed",
                new List<string>
                {
                    "SQLite ledger initialized successfully",
                    "Contest started with personas: roma, levene, moss",
                    "Synthetic deal events written to ledger",
                    "Leaderboard computed successfully (Roma in 1st place)",
                    "Contest replay markdown generated successfully"
                });

            var envelope = PublicExampleResultEnvelope.Create(
                exampleId: "sales-arena",
                exampleVersion: "1.0.0",
                inputSummary: "dna-arena --smoke",
                localValidation: validation,
                outputArtifactRefs: new List<PublicExampleOutputArtifactRef>
                {
                    new("stdout", "console", "application/json")
                },
                selfReportedMetrics: new Dictionary<string, decimal>
                {
                    ["dealsWon"] = 2,
                    ["dealsLost"] = 1,
                    ["totalRevenueUsd"] = 73000m
                },
                runId: "sales-arena-smoke-" + Guid.NewGuid().ToString("N")[..8]);

            var json = PublicExampleResultEnvelopeJson.Serialize(envelope);
            Console.WriteLine(json);

            return passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Smoke Mode] Unexpected error during simulation: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 4;
        }
        finally
        {
            try
            {
                if (workspaceDir.Exists)
                {
                    workspaceDir.Delete(recursive: true);
                }
            }
            catch
            {
                // Silently ignore cleanup failures in temp directory
            }
        }
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
