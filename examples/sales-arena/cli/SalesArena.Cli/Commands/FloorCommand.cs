// SPDX-License-Identifier: Apache-2.0
//
// dna-arena floor. Story 6122c6f7 (SA-04-03).
// Renders an ASCII leaderboard frame and refreshes it from the live
// leaderboard backend when a contest is supplied.

using System.CommandLine;

namespace SalesArena.Cli.Commands;

public static class FloorCommand
{
    public const string AsciiFrameRelativePath = "examples/sales-arena/assets/ascii/ascii-floor.txt";

    public static Command Create()
    {
        var command = new Command("floor", "Render the live ASCII leaderboard floor frame.");

        var contestOption = new Option<string?>(
            name: "--contest",
            description: "Contest name to read from the arena ledger.");

        var fallbackOption = new Option<bool>(
            name: "--ascii-only",
            description: "Force the ASCII fallback view instead of leaderboard rows.");
        var workspaceOption = new Option<DirectoryInfo?>(
            name: "--workspace",
            description: "Arena workspace directory. Defaults to ./.arena.");

        command.AddOption(contestOption);
        command.AddOption(fallbackOption);
        command.AddOption(workspaceOption);

        command.SetHandler(
            async (contest, asciiOnly, workspace) =>
            {
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                Environment.ExitCode = await HandleAsync(contest, asciiOnly, workspace, Console.Out, Console.Error, cts.Token, maxTicks: null)
                    .ConfigureAwait(false);
            },
            contestOption,
            fallbackOption,
            workspaceOption);

        return command;
    }

    /// <summary>Pure handler used by the xUnit tests. Locates the ASCII frame
    /// alongside the repository's <c>examples/sales-arena/assets/</c> tree and
    /// prints it to <paramref name="stdout"/>. Returns 0 on success, 3 on
    /// missing-asset, 2 on usage error.</summary>
    public static int Handle(string? contest, bool asciiOnly, TextWriter stdout, TextWriter stderr)
        => HandleAsync(contest, asciiOnly, workspace: null, stdout, stderr, CancellationToken.None, maxTicks: 1)
            .GetAwaiter().GetResult();

    public static async Task<int> HandleAsync(
        string? contest,
        bool asciiOnly,
        DirectoryInfo? workspace,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken,
        int? maxTicks)
    {
        var frame = LocateAsciiFrame();
        if (frame is null)
        {
            stderr.WriteLine($"dna-arena floor: could not locate {AsciiFrameRelativePath} relative to the current working directory or executable.");
            return 3;
        }

        var ticks = 0;
        do
        {
            stdout.Write(File.ReadAllText(frame));
            stdout.WriteLine();
            if (!asciiOnly && !string.IsNullOrWhiteSpace(contest))
            {
                var leaderboard = await ArenaCliBackend.ComputeLeaderboardAsync(contest, workspace, cancellationToken)
                    .ConfigureAwait(false);
                stdout.WriteLine($"dna-arena floor: live leaderboard for '{contest}' at {leaderboard.AsOfUtc:u}");
                foreach (var row in leaderboard.Entries)
                {
                    stdout.WriteLine($"{row.Position,2}. {row.Persona,-20} {row.Tier,-14} ${row.RevenueUsd,10:N0} {row.DealsWon}W/{row.DealsLost}L");
                }
                if (leaderboard.Entries.Count == 0)
                {
                    stdout.WriteLine("dna-arena floor: no ledger events have produced leaderboard rows yet.");
                }
            }
            else
            {
                stdout.WriteLine("dna-arena floor: ASCII fallback frame.");
            }

            ticks++;
            if (maxTicks is not null && ticks >= maxTicks.Value)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        while (!cancellationToken.IsCancellationRequested);

        return 0;
    }

    /// <summary>Resolve the ASCII frame against either the current working
    /// directory or the directory containing the running assembly so the
    /// command works both from a `dotnet run` and a packaged `dna-arena`
    /// tool invocation.</summary>
    private static string? LocateAsciiFrame()
    {
        var probes = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), AsciiFrameRelativePath),
        };

        var assemblyDir = Path.GetDirectoryName(typeof(FloorCommand).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            probes.Add(Path.Combine(assemblyDir, AsciiFrameRelativePath));
            // Walk up to the repository root from the assembly location.
            var dir = new DirectoryInfo(assemblyDir);
            while (dir is not null)
            {
                probes.Add(Path.Combine(dir.FullName, AsciiFrameRelativePath));
                dir = dir.Parent;
            }
        }

        foreach (var path in probes)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
