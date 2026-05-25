using FluentAssertions;
using SalesArena.Orchestrator.Narration;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

public sealed class FileSystemCueScriptResolverTests : IDisposable
{
    private readonly string _dir;

    public FileSystemCueScriptResolverTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sa-narration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void ContestOpened_loads_from_cold_open_txt()
    {
        File.WriteAllText(Path.Combine(_dir, "cold-open.txt"),
            """
            # comment to ignore
            Floor open, {contest}.
            Lights up.
            """);
        var resolver = new FileSystemCueScriptResolver(_dir);

        var first = resolver.Resolve(CueKinds.ContestOpened, new Dictionary<string, string> { ["contest"] = "c-1" });
        var second = resolver.Resolve(CueKinds.ContestOpened, new Dictionary<string, string> { ["contest"] = "c-1" });

        first.Should().Be("Floor open, c-1.");
        second.Should().Be("Lights up.");
    }

    [Fact]
    public void DealClosed_loads_from_deal_closed_txt_via_kebab_case()
    {
        File.WriteAllText(Path.Combine(_dir, "deal-closed.txt"), "{persona} closed {lead}.");
        var resolver = new FileSystemCueScriptResolver(_dir);

        var line = resolver.Resolve(CueKinds.DealClosed, new Dictionary<string, string>
        {
            ["persona"] = "roma",
            ["lead"] = "L-9",
        });

        line.Should().Be("roma closed L-9.");
    }

    [Fact]
    public void Missing_file_returns_null()
    {
        var resolver = new FileSystemCueScriptResolver(_dir);
        var line = resolver.Resolve(CueKinds.DealClosed, new Dictionary<string, string>());
        line.Should().BeNull();
    }

    [Fact]
    public void Comments_and_blank_lines_are_filtered()
    {
        File.WriteAllText(Path.Combine(_dir, "bell-rung.txt"),
            """
            # only blanks below

            # comment

            single line
            """);
        var resolver = new FileSystemCueScriptResolver(_dir);

        var line = resolver.Resolve(CueKinds.BellRung, new Dictionary<string, string>());
        line.Should().Be("single line");
    }

    [Fact]
    public void Missing_directory_throws()
    {
        Action act = () => _ = new FileSystemCueScriptResolver("/does/not/exist/at/all");
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void KebabCase_handles_PascalCase_cleanly()
    {
        FileSystemCueScriptResolver.KebabCase("PersonaPromoted").Should().Be("persona-promoted");
        FileSystemCueScriptResolver.KebabCase("BellRung").Should().Be("bell-rung");
        FileSystemCueScriptResolver.KebabCase("GlengarryDripped").Should().Be("glengarry-dripped");
    }
}
