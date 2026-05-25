using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SalesArena.Knowledge.Agent;

public interface IKnowledgeSource
{
    IReadOnlyList<KnowledgeChunk> LoadChunks();
}

public sealed class InMemoryKnowledgeSource : IKnowledgeSource
{
    private readonly IReadOnlyList<KnowledgeChunk> _chunks;
    public InMemoryKnowledgeSource(IReadOnlyList<KnowledgeChunk> chunks) => _chunks = chunks;
    public IReadOnlyList<KnowledgeChunk> LoadChunks() => _chunks;
}

/// <summary>
/// Reads `.md` files from a directory tree and splits each into chunks at top-level
/// (`# ` and `## `) heading boundaries. Each chunk's id is `{relativePath}#chunk-{index}`.
/// Refuses to traverse outside the configured root.
/// </summary>
public sealed class FileSystemKnowledgeSource : IKnowledgeSource
{
    private readonly string _root;

    public FileSystemKnowledgeSource(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root required", nameof(root));
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Knowledge root not found: {full}");
        _root = full;
    }

    public IReadOnlyList<KnowledgeChunk> LoadChunks()
    {
        var chunks = new List<KnowledgeChunk>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
            var text = File.ReadAllText(path);
            int chunkIndex = 0;
            foreach (var (heading, body) in SplitByHeadings(text))
            {
                chunks.Add(new KnowledgeChunk(
                    ChunkId: $"{relative}#chunk-{chunkIndex}",
                    DocumentSourceId: relative,
                    Heading: heading,
                    Text: body));
                chunkIndex++;
            }
        }
        return chunks;
    }

    private static IEnumerable<(string Heading, string Body)> SplitByHeadings(string text)
    {
        var lines = text.Split('\n');
        string heading = "(preamble)";
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("# ") || line.StartsWith("## "))
            {
                if (sb.Length > 0)
                {
                    yield return (heading, sb.ToString().Trim());
                    sb.Clear();
                }
                heading = line.TrimStart('#').Trim();
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        if (sb.Length > 0)
            yield return (heading, sb.ToString().Trim());
    }
}
