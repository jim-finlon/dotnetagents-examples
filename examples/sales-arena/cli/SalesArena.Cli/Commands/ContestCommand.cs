// SPDX-License-Identifier: Apache-2.0
//
// dna-arena contest {start|pause|resume|end}. Story 6122c6f7 (SA-04-03).
// Covers the SA-02-05 contest-lifecycle surface by writing phase-change
// events through the live append-only ledger backend.

using System.CommandLine;

namespace SalesArena.Cli.Commands;

public static class ContestCommand
{
    public static Command Create()
    {
        var command = new Command("contest", "Contest lifecycle: start / pause / resume / end.");

        command.AddCommand(BuildStartCommand());
        command.AddCommand(BuildLifecycleCommand("pause", "Pause an in-flight contest."));
        command.AddCommand(BuildLifecycleCommand("resume", "Resume a paused contest."));
        command.AddCommand(BuildLifecycleCommand("end", "End an in-flight contest."));

        return command;
    }

    private static Command BuildStartCommand()
    {
        var startCommand = new Command("start", "Start a new contest.");

        var nameOption = new Option<string>(
            name: "--name",
            description: "Contest name (e.g. 'tuesday-steak-knives').")
        {
            IsRequired = true,
        };
        var personasOption = new Option<string>(
            name: "--personas",
            description: "Comma-separated persona ids (e.g. 'roma,levene,moss').")
        {
            IsRequired = true,
        };
        var hoursOption = new Option<int>(
            name: "--hours",
            getDefaultValue: () => 1,
            description: "Contest duration in simulated hours.");
        var compressionOption = new Option<int>(
            name: "--time-compression",
            getDefaultValue: () => 60,
            description: "Time-compression factor (1 = real time, 60 = one minute of wall clock per simulated hour).");
        var workspaceOption = new Option<DirectoryInfo?>(
            name: "--workspace",
            description: "Arena workspace directory. Defaults to ./.arena.");

        startCommand.AddOption(nameOption);
        startCommand.AddOption(personasOption);
        startCommand.AddOption(hoursOption);
        startCommand.AddOption(compressionOption);
        startCommand.AddOption(workspaceOption);

        startCommand.SetHandler(
            async (name, personas, hours, compression, workspace) =>
            {
                Environment.ExitCode = await HandleStartAsync(name, personas, hours, compression, workspace, Console.Out, Console.Error, CancellationToken.None)
                    .ConfigureAwait(false);
            },
            nameOption,
            personasOption,
            hoursOption,
            compressionOption,
            workspaceOption);

        return startCommand;
    }

    private static Command BuildLifecycleCommand(string verb, string description)
    {
        var command = new Command(verb, description);

        var contestOption = new Option<string>(
            name: "--contest",
            description: "Contest name.")
        {
            IsRequired = true,
        };
        var workspaceOption = new Option<DirectoryInfo?>(
            name: "--workspace",
            description: "Arena workspace directory. Defaults to ./.arena.");
        command.AddOption(contestOption);
        command.AddOption(workspaceOption);

        command.SetHandler(
            async (contest, workspace) =>
            {
                Environment.ExitCode = await HandleLifecycleAsync(verb, contest, workspace, Console.Out, Console.Error, CancellationToken.None)
                    .ConfigureAwait(false);
            },
            contestOption,
            workspaceOption);

        return command;
    }

    /// <summary>Pure handler for <c>dna-arena contest start</c> — visible to tests.</summary>
    public static int HandleStart(string name, string personas, int hours, int compression, TextWriter stdout, TextWriter stderr)
        => HandleStartAsync(name, personas, hours, compression, workspace: null, stdout, stderr, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task<int> HandleStartAsync(
        string name,
        string personas,
        int hours,
        int compression,
        DirectoryInfo? workspace,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            stderr.WriteLine("dna-arena contest start: --name is required.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(personas))
        {
            stderr.WriteLine("dna-arena contest start: --personas is required.");
            return 2;
        }

        var personaList = personas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (personaList.Length == 0)
        {
            stderr.WriteLine("dna-arena contest start: --personas resolved to zero personas.");
            return 2;
        }

        if (hours <= 0)
        {
            stderr.WriteLine("dna-arena contest start: --hours must be positive.");
            return 2;
        }

        if (compression <= 0)
        {
            stderr.WriteLine("dna-arena contest start: --time-compression must be positive.");
            return 2;
        }

        await ArenaCliBackend.AppendContestPhaseAsync(
            name,
            "Started",
            $"personas:{string.Join(",", personaList)};hours:{hours};compression:{compression}",
            workspace,
            cancellationToken).ConfigureAwait(false);

        stdout.WriteLine($"dna-arena contest start: '{name}' with personas [{string.Join(", ", personaList)}], {hours}h @ {compression}x compression.");
        stdout.WriteLine("dna-arena contest start: wrote Started phase to the live arena ledger.");
        return 0;
    }

    /// <summary>Pure handler for the pause / resume / end lifecycle verbs.</summary>
    public static int HandleLifecycle(string verb, string contest, TextWriter stdout, TextWriter stderr)
        => HandleLifecycleAsync(verb, contest, workspace: null, stdout, stderr, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task<int> HandleLifecycleAsync(
        string verb,
        string contest,
        DirectoryInfo? workspace,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contest))
        {
            stderr.WriteLine($"dna-arena contest {verb}: --contest is required.");
            return 2;
        }

        var phase = verb switch
        {
            "pause" => "Paused",
            "resume" => "Resumed",
            "end" => "Ended",
            _ => verb,
        };
        await ArenaCliBackend.AppendContestPhaseAsync(contest, phase, $"cli:{verb}", workspace, cancellationToken)
            .ConfigureAwait(false);
        stdout.WriteLine($"dna-arena contest {verb}: wrote {phase} phase for contest '{contest}' to the live arena ledger.");
        return 0;
    }
}
