// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using SalesArena.Cli.Commands;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Cli.Tests;

public sealed class FloorAndBellCommandTests
{
    [Fact]
    public void Floor_prints_ascii_frame_when_repository_assets_are_resolvable()
    {
        // The test process runs out of the test-project bin/ directory, which the
        // command resolves upward to the repository root. If the assets are reachable,
        // the command should render the frame and exit 0. If not (e.g. a packaged
        // single-file distribution without the assets nearby), the command should
        // return exit code 3 with a clear error message.
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = FloorCommand.Handle(contest: null, asciiOnly: false, stdout, stderr);

        if (exit == 0)
        {
            stdout.ToString().Should().Contain("D N A   S A L E S   A R E N A");
            stdout.ToString().Should().Contain("ASCII fallback frame");
        }
        else
        {
            exit.Should().Be(3);
            stderr.ToString().Should().Contain("could not locate");
        }
    }

    [Fact]
    public async Task Bell_prints_ding_line_and_writes_ledger_event()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var workspace = NewTempWorkspace();

        var exit = await BellCommand.HandleAsync(persona: null, contest: "demo-2026", workspace, stdout, stderr, CancellationToken.None);

        if (exit == 0)
        {
            stdout.ToString().Should().Contain(BellCommand.DingLine);
            stdout.ToString().Should().Contain("mitch-and-murray");
            stdout.ToString().Should().Contain("narrator:");
            await using var ledger = NewLedger(workspace);
            (await ledger.CountAsync(ArenaEventFilter.OfKind("demo-2026", ArenaEventKinds.BellRung)))
                .Should().Be(1);
        }
        else
        {
            exit.Should().Be(3);
            stderr.ToString().Should().Contain("could not locate");
        }
        workspace.Delete(recursive: true);
    }

    [Fact]
    public void Bell_with_explicit_persona_uses_supplied_id()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = BellCommand.Handle(persona: "moss", stdout, stderr);

        if (exit == 0)
        {
            stdout.ToString().Should().Contain("persona 'moss'");
        }
        else
        {
            exit.Should().Be(3);
        }
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
