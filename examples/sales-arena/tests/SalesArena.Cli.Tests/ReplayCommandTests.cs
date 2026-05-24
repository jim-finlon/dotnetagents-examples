// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using SalesArena.Cli.Commands;
using Xunit;

namespace SalesArena.Cli.Tests;

public sealed class ReplayCommandTests
{
    [Fact]
    public void Empty_contest_returns_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = ReplayCommand.Handle(contest: "", output: null, stdout, stderr);

        exit.Should().Be(2);
        stderr.ToString().Should().Contain("--contest is required");
    }

    [Fact]
    public void Stdout_mode_prints_live_replay_markdown()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = ReplayCommand.Handle(contest: "demo-2026", output: null, stdout, stderr);

        exit.Should().Be(0);
        stdout.ToString().Should().Contain("# Sales Arena Replay — demo-2026");
    }

    [Fact]
    public void File_mode_writes_live_replay_markdown_to_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"replay-{Guid.NewGuid():N}.md");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exit = ReplayCommand.Handle(contest: "demo-2026", output: new FileInfo(path), stdout, stderr);

            exit.Should().Be(0);
            File.Exists(path).Should().BeTrue();
            var written = File.ReadAllText(path);
            written.Should().Contain("# Sales Arena Replay — demo-2026");
            written.Should().Contain("Final Leaderboard");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
