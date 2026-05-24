// SPDX-License-Identifier: Apache-2.0
//
// dna-arena bell. Story 6122c6f7 (SA-04-03).
// Sanity-check the bell cue + narration and append a BellRung event to the
// live arena ledger.

using System.CommandLine;

namespace SalesArena.Cli.Commands;

public static class BellCommand
{
    public const string AsciiBellRelativePath = "samples/sales-arena/assets/ascii/ascii-bell.txt";
    public const string DingLine = "DING DING DING";

    public static Command Create()
    {
        var command = new Command("bell", "Fire a test bell cue + narration sanity check.");

        var personaOption = new Option<string?>(
            name: "--persona",
            description: "Persona id whose narration voice should fire (defaults to mitch-and-murray).");
        var contestOption = new Option<string>(
            name: "--contest",
            getDefaultValue: () => "demo-2026",
            description: "Contest name to write the bell event under.");
        var workspaceOption = new Option<DirectoryInfo?>(
            name: "--workspace",
            description: "Arena workspace directory. Defaults to ./.arena.");

        command.AddOption(personaOption);
        command.AddOption(contestOption);
        command.AddOption(workspaceOption);

        command.SetHandler(
            async (persona, contest, workspace) =>
            {
                Environment.ExitCode = await HandleAsync(persona, contest, workspace, Console.Out, Console.Error, CancellationToken.None)
                    .ConfigureAwait(false);
            },
            personaOption,
            contestOption,
            workspaceOption);

        return command;
    }

    /// <summary>Pure handler. Locates the ASCII bell frame and prints it
    /// alongside the narration sentinel the demo script + tests assert
    /// against.</summary>
    public static int Handle(string? persona, TextWriter stdout, TextWriter stderr)
        => HandleAsync(persona, "demo-2026", workspace: null, stdout, stderr, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task<int> HandleAsync(
        string? persona,
        string contest,
        DirectoryInfo? workspace,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        var personaId = string.IsNullOrWhiteSpace(persona) ? "mitch-and-murray" : persona;
        var frame = LocateAsciiBell();
        if (frame is null)
        {
            stderr.WriteLine($"dna-arena bell: could not locate {AsciiBellRelativePath}.");
            return 3;
        }

        stdout.Write(File.ReadAllText(frame));
        stdout.WriteLine();
        stdout.WriteLine(DingLine);
        var cue = await ArenaCliBackend.RingBellAsync(contest, personaId, workspace, cancellationToken)
            .ConfigureAwait(false);
        stdout.WriteLine($"dna-arena bell: routed narration through persona '{personaId}'.");
        if (cue is not null)
        {
            stdout.WriteLine($"narrator: {cue.Line}");
        }
        return 0;
    }

    private static string? LocateAsciiBell()
    {
        var probes = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), AsciiBellRelativePath),
        };

        var assemblyDir = Path.GetDirectoryName(typeof(BellCommand).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            probes.Add(Path.Combine(assemblyDir, AsciiBellRelativePath));
            var dir = new DirectoryInfo(assemblyDir);
            while (dir is not null)
            {
                probes.Add(Path.Combine(dir.FullName, AsciiBellRelativePath));
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
