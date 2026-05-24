// SPDX-License-Identifier: Apache-2.0
//
// dna-arena replay. Story 6122c6f7 (SA-04-03).
// Writes the SA-04-01 Markdown replay report from the live ledger-backed
// replay engine.

using System.CommandLine;

namespace SalesArena.Cli.Commands;

public static class ReplayCommand
{
    public static Command Create()
    {
        var command = new Command("replay", "Generate the Markdown replay report for a contest.");

        var contestOption = new Option<string>(
            name: "--contest",
            description: "Contest name to render.")
        {
            IsRequired = true,
        };
        var outOption = new Option<FileInfo?>(
            name: "--out",
            description: "Output Markdown file path. Defaults to stdout when omitted.");
        var workspaceOption = new Option<DirectoryInfo?>(
            name: "--workspace",
            description: "Arena workspace directory. Defaults to ./.arena.");

        command.AddOption(contestOption);
        command.AddOption(outOption);
        command.AddOption(workspaceOption);

        command.SetHandler(
            async (contest, output, workspace) =>
            {
                Environment.ExitCode = await HandleAsync(contest, output, workspace, Console.Out, Console.Error, CancellationToken.None)
                    .ConfigureAwait(false);
            },
            contestOption,
            outOption,
            workspaceOption);

        return command;
    }

    /// <summary>Pure handler used by the xUnit tests. Renders a stable
    /// Markdown replay report from the live replay generator.</summary>
    public static int Handle(string contest, FileInfo? output, TextWriter stdout, TextWriter stderr)
        => HandleAsync(contest, output, workspace: null, stdout, stderr, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task<int> HandleAsync(
        string contest,
        FileInfo? output,
        DirectoryInfo? workspace,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contest))
        {
            stderr.WriteLine("dna-arena replay: --contest is required.");
            return 2;
        }

        try
        {
            var report = await ArenaCliBackend.GenerateReplayAsync(contest, workspace, output, cancellationToken)
                .ConfigureAwait(false);
            if (output is null)
            {
                stdout.Write(report.Markdown);
            }
            else
            {
                stdout.WriteLine($"dna-arena replay: wrote live replay to {output.FullName} ({report.Markdown.Length} bytes).");
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"dna-arena replay: failed to generate report: {ex.Message}");
            return 3;
        }
    }

}
