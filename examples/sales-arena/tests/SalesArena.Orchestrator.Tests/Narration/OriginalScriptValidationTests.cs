using FluentAssertions;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

/// <summary>
/// SA-02-06 acceptance: cold-open + all narration must be DNA-original. This
/// test scans the on-disk script files for known copyrighted Glengarry quotes.
/// If anyone (human or LLM) accidentally pastes a line in, the build catches it.
/// </summary>
public sealed class OriginalScriptValidationTests
{
    // Distinctive multi-word fragments. Single common words (e.g. "leads") are
    // intentionally NOT here — they appear in original copy. Anything below is
    // a stable, recognisable phrase that no original DNA copy needs.
    private static readonly string[] _forbiddenFragments =
    {
        "Coffee is for closers",
        "Coffee's for closers",
        "Always Be Closing",
        "ABC.",
        "ABC ",
        "Put. That coffee. Down.",
        "Put that coffee down",
        "A-I-D-A",
        "Attention. Interest. Decision. Action",
        "The Cadillac El Dorado",
        "The leads are weak",
        "You called Mitch and Murray",
        "I made $970,000 last year",
        "third prize is you're fired",
    };

    public static IEnumerable<object[]> ScriptFiles()
    {
        var dir = FindScriptDir();
        foreach (var path in Directory.EnumerateFiles(dir, "*.txt", SearchOption.TopDirectoryOnly))
        {
            yield return new object[] { Path.GetFileName(path), path };
        }
    }

    [Theory]
    [MemberData(nameof(ScriptFiles))]
    public void Script_file_contains_no_copyrighted_glengarry_fragments(string fileName, string path)
    {
        var contents = File.ReadAllText(path);
        foreach (var forbidden in _forbiddenFragments)
        {
            contents.Should().NotContain(forbidden,
                $"the script file {fileName} must be DNA-original — found a copyrighted fragment");
        }
    }

    [Fact]
    public void Cold_open_file_exists_and_is_non_empty()
    {
        var dir = FindScriptDir();
        var path = Path.Combine(dir, "cold-open.txt");
        File.Exists(path).Should().BeTrue("SA-02-06 acceptance requires cold-open.txt");

        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToArray();
        lines.Should().NotBeEmpty("cold-open.txt must ship with at least one usable line");
    }

    [Fact]
    public void At_least_five_cue_scripts_ship()
    {
        var dir = FindScriptDir();
        var count = Directory.EnumerateFiles(dir, "*.txt").Count();
        count.Should().BeGreaterOrEqualTo(5,
            "SA-02-06 acceptance requires 5+ theatre cue script files");
    }

    private static string FindScriptDir()
    {
        // Tests run with the orchestrator's content files copied to output, but
        // we keep a source-tree fallback so test failures point at the actual
        // committed scripts (CI-friendly).
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Narration", "scripts"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "orchestrator", "Narration", "scripts"),
        };

        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (Directory.Exists(resolved))
            {
                return resolved;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate Narration/scripts; checked: " + string.Join(", ", candidates));
    }
}
