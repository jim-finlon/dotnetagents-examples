// SPDX-License-Identifier: Apache-2.0
//
// dna-arena CLI entry point. Story 6122c6f7 (SA-04-03).
// Five subcommands route to per-command Create() factories that follow the
// DotNetAgents.CLI pattern. Each command currently emits stable
// "would do X" output the demo-mode.sh + the xUnit tests can assert against;
// the orchestrator (SA-02-01..07) and replay engine (SA-04-01..02) wiring
// lands when those stories ship.

using System.CommandLine;
using SalesArena.Cli.Commands;

namespace SalesArena.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DNA Sales Arena CLI — init / contest / floor / bell / replay.")
        {
            Name = "dna-arena",
        };

        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(ContestCommand.Create());
        rootCommand.AddCommand(FloorCommand.Create());
        rootCommand.AddCommand(BellCommand.Create());
        rootCommand.AddCommand(ReplayCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
