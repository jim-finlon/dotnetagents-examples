// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using SalesArena.Cli.Commands;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Cli.Tests;

public sealed class ContestCommandTests
{
    [Fact]
    public void Start_with_empty_name_returns_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = ContestCommand.HandleStart(name: "", personas: "roma", hours: 1, compression: 60, stdout, stderr);

        exit.Should().Be(2);
        stderr.ToString().Should().Contain("--name is required");
    }

    [Fact]
    public void Start_with_empty_personas_returns_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = ContestCommand.HandleStart(name: "tuesday", personas: "", hours: 1, compression: 60, stdout, stderr);

        exit.Should().Be(2);
        stderr.ToString().Should().Contain("--personas is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Start_with_non_positive_hours_returns_usage_error(int hours)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = ContestCommand.HandleStart(name: "tuesday", personas: "roma", hours, compression: 60, stdout, stderr);

        exit.Should().Be(2);
        stderr.ToString().Should().Contain("--hours must be positive");
    }

    [Fact]
    public async Task Start_happy_path_writes_started_phase_to_ledger()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var workspace = NewTempWorkspace();

        var exit = await ContestCommand.HandleStartAsync(
            name: "tuesday-steak-knives",
            personas: "roma,levene,moss",
            hours: 1,
            compression: 60,
            workspace,
            stdout,
            stderr,
            CancellationToken.None);

        exit.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("'tuesday-steak-knives'");
        output.Should().Contain("[roma, levene, moss]");
        output.Should().Contain("1h @ 60x");
        output.Should().Contain("wrote Started phase");
        stderr.ToString().Should().BeEmpty();

        await using var ledger = NewLedger(workspace);
        (await ledger.CountAsync(ArenaEventFilter.OfKind("tuesday-steak-knives", ArenaEventKinds.ContestPhaseChanged)))
            .Should().Be(1);
        workspace.Delete(recursive: true);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    [InlineData("end")]
    public void Lifecycle_with_empty_contest_returns_usage_error(string verb)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = ContestCommand.HandleLifecycle(verb, contest: "", stdout, stderr);

        exit.Should().Be(2);
        stderr.ToString().Should().Contain("--contest is required");
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    [InlineData("end")]
    public async Task Lifecycle_happy_path_writes_phase_to_ledger(string verb)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var workspace = NewTempWorkspace();

        var exit = await ContestCommand.HandleLifecycleAsync(verb, contest: "demo-2026", workspace, stdout, stderr, CancellationToken.None);

        exit.Should().Be(0);
        stdout.ToString().Should().Contain("contest 'demo-2026'");
        await using var ledger = NewLedger(workspace);
        (await ledger.CountAsync(ArenaEventFilter.OfKind("demo-2026", ArenaEventKinds.ContestPhaseChanged)))
            .Should().Be(1);
        workspace.Delete(recursive: true);
    }

    private static DirectoryInfo NewTempWorkspace()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"dna-arena-cli-{Guid.NewGuid():N}"));
        dir.Create();
        return dir;
    }

    private static SqliteArenaLedger NewLedger(DirectoryInfo workspace) =>
        new($"Data Source={Path.Combine(workspace.FullName, "ledger.db")}");
}
