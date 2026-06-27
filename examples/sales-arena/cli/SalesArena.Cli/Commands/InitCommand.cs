// SPDX-License-Identifier: Apache-2.0
//
// dna-arena init. Story 6122c6f7 (SA-04-03).
// Bootstraps a contest workspace from a lead-pack JSON file and seeds the
// append-only sqlite ledger used by the orchestrator and replay backend.

using System.CommandLine;
using System.Text.Json;

namespace SalesArena.Cli.Commands;

public static class InitCommand
{
    public const string DemoReadySentinel = "DEMO MODE READY";

    public static Command Create()
    {
        var command = new Command("init", "Initialize a contest workspace from a lead-pack JSON file.");

        var leadsOption = new Option<FileInfo?>(
            name: "--leads",
            description: "Path to the lead-pack JSON file (e.g. examples/sales-arena/lead-packs/synthetic-200.json).")
        {
            IsRequired = true,
        };
        var uiPortOption = new Option<int>(
            name: "--ui-port",
            getDefaultValue: () => 5005,
            description: "Manager UI port.");
        var bellPortOption = new Option<int>(
            name: "--bell-port",
            getDefaultValue: () => 5006,
            description: "CLI bell-stream port.");
        var workspaceOption = new Option<DirectoryInfo?>(
            name: "--workspace",
            description: "Arena workspace directory. Defaults to ./.arena.");

        command.AddOption(leadsOption);
        command.AddOption(uiPortOption);
        command.AddOption(bellPortOption);
        command.AddOption(workspaceOption);

        command.SetHandler(
            async (leads, uiPort, bellPort, workspace) =>
            {
                Environment.ExitCode = await HandleAsync(leads, uiPort, bellPort, workspace, Console.Out, Console.Error, CancellationToken.None)
                    .ConfigureAwait(false);
            },
            leadsOption,
            uiPortOption,
            bellPortOption,
            workspaceOption);

        return command;
    }

    /// <summary>
    /// Pure handler used by both the CLI surface and the xUnit tests. Returns
    /// the exit code (0 success / 2 usage error / 3 IO error). Writes to the
    /// supplied <paramref name="stdout"/> / <paramref name="stderr"/> writers
    /// so tests can capture output.
    /// </summary>
    public static int Handle(FileInfo? leads, int uiPort, int bellPort, TextWriter stdout, TextWriter stderr)
        => HandleAsync(leads, uiPort, bellPort, workspace: null, stdout, stderr, CancellationToken.None)
            .GetAwaiter().GetResult();

    public static async Task<int> HandleAsync(
        FileInfo? leads,
        int uiPort,
        int bellPort,
        DirectoryInfo? workspace,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (leads is null)
        {
            stderr.WriteLine("dna-arena init: --leads is required.");
            return 2;
        }

        if (!leads.Exists)
        {
            stderr.WriteLine($"dna-arena init: lead-pack file not found: {leads.FullName}");
            return 3;
        }

        try
        {
            // Probe the JSON shape; full lead-pack-v2 validation lands on the
            // parent SA-05-02 / SA-06-03 story. Today we confirm the file
            // parses as a JSON document.
            using var stream = leads.OpenRead();
            using var document = JsonDocument.Parse(stream);
            _ = document.RootElement;
        }
        catch (JsonException ex)
        {
            stderr.WriteLine($"dna-arena init: lead-pack is not valid JSON ({ex.Message}).");
            return 3;
        }

        try
        {
            await ArenaCliBackend.BootstrapLedgerAsync(leads, uiPort, bellPort, workspace, stdout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"dna-arena init: failed to bootstrap ledger ({ex.Message}).");
            return 3;
        }
        catch (JsonException ex)
        {
            stderr.WriteLine($"dna-arena init: failed to bootstrap ledger ({ex.Message}).");
            return 3;
        }

        stdout.WriteLine($"dna-arena init: lead-pack accepted ({leads.FullName}).");
        stdout.WriteLine($"dna-arena init: manager UI binding hint http://localhost:{uiPort}/floor.");
        stdout.WriteLine($"dna-arena init: bell stream binding hint port {bellPort}.");
        stdout.WriteLine(DemoReadySentinel);
        return 0;
    }
}
