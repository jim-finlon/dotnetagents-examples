// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using SalesArena.Cli.Commands;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Cli.Tests;

public sealed class InitCommandTests
{
    [Fact]
    public void Missing_leads_returns_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = InitCommand.Handle(leads: null, uiPort: 5005, bellPort: 5006, stdout, stderr);

        exit.Should().Be(2);
        stderr.ToString().Should().Contain("--leads is required");
    }

    [Fact]
    public void Missing_lead_pack_file_returns_io_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var leads = new FileInfo(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json"));

        var exit = InitCommand.Handle(leads, uiPort: 5005, bellPort: 5006, stdout, stderr);

        exit.Should().Be(3);
        stderr.ToString().Should().Contain("not found");
    }

    [Fact]
    public void Malformed_lead_pack_returns_io_error()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad-pack-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "not actually json");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = InitCommand.Handle(new FileInfo(path), uiPort: 5005, bellPort: 5006, stdout, stderr);

            exit.Should().Be(3);
            stderr.ToString().Should().Contain("not valid JSON");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Valid_lead_pack_bootstraps_ledger_and_prints_demo_ready_sentinel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"good-pack-{Guid.NewGuid():N}.json");
        var workspace = NewTempWorkspace();
        File.WriteAllText(path, "{ \"leads\": [{ \"id\": \"L-0001\" }, { \"id\": \"L-0002\" }] }");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = await InitCommand.HandleAsync(
                new FileInfo(path),
                uiPort: 5105,
                bellPort: 5106,
                workspace,
                stdout,
                stderr,
                CancellationToken.None);

            exit.Should().Be(0);
            var output = stdout.ToString();
            output.Should().Contain(InitCommand.DemoReadySentinel);
            output.Should().Contain("http://localhost:5105/floor");
            output.Should().Contain("port 5106");
            output.Should().Contain("ledger bootstrapped");
            stderr.ToString().Should().BeEmpty();

            var contestId = Path.GetFileNameWithoutExtension(path);
            await using var ledger = new SqliteArenaLedger($"Data Source={Path.Combine(workspace.FullName, "ledger.db")}");
            (await ledger.CountAsync(ArenaEventFilter.OfKind(contestId, ArenaEventKinds.LeadAssigned)))
                .Should().Be(2);
        }
        finally
        {
            File.Delete(path);
            if (workspace.Exists) workspace.Delete(recursive: true);
        }
    }

    private static DirectoryInfo NewTempWorkspace()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"dna-arena-cli-{Guid.NewGuid():N}"));
        dir.Create();
        return dir;
    }
}
