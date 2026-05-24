using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Knowledge.Agent;

public interface IKnowledgeAgent
{
    Task<KnowledgeAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default);
}

/// <summary>
/// Deterministic, offline knowledge agent. Indexes chunks once on construction using an
/// inverted index, then ranks query results by sum of term frequency × inverse document
/// frequency. Returns top-N hits with a stable snippet (first 240 chars of the chunk text).
/// </summary>
public sealed class KnowledgeAgent : IKnowledgeAgent
{
    private static readonly HashSet<string> DefaultStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","is","are","was","were","be","been","being",
        "to","of","in","on","at","for","with","by","from","as","that","this","these","those",
        "i","you","he","she","it","we","they","me","him","her","us","them",
        "do","does","did","have","has","had","will","would","can","could","should","may","might",
    };

    private readonly IReadOnlyList<KnowledgeChunk> _chunks;
    private readonly Dictionary<string, Dictionary<int, int>> _index;
    private readonly Dictionary<string, double> _idf;
    private readonly IReadOnlySet<string> _stopWords;

    public KnowledgeAgent(IKnowledgeSource source, IReadOnlySet<string>? stopWords = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        _stopWords = stopWords ?? DefaultStopWords;
        _chunks = source.LoadChunks();
        _index = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        for (int chunkIdx = 0; chunkIdx < _chunks.Count; chunkIdx++)
        {
            foreach (var token in Tokenize(_chunks[chunkIdx].Text))
            {
                if (!_index.TryGetValue(token, out var posting))
                {
                    posting = new Dictionary<int, int>();
                    _index[token] = posting;
                }
                posting[chunkIdx] = posting.TryGetValue(chunkIdx, out var n) ? n + 1 : 1;
            }
        }
        _idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var docCount = Math.Max(1, _chunks.Count);
        foreach (var (term, posting) in _index)
            _idf[term] = Math.Log(1.0 + (double)docCount / posting.Count);
    }

    public Task<KnowledgeAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        if (query is null) throw new ArgumentNullException(nameof(query));
        if (string.IsNullOrWhiteSpace(query.QueryText))
            throw new ArgumentException("Query text is required.", nameof(query));
        if (query.TopN <= 0)
            throw new ArgumentException("TopN must be > 0.", nameof(query));

        var diagnostics = new List<string>();
        var queryTerms = Tokenize(query.QueryText).ToArray();
        if (queryTerms.Length == 0)
        {
            diagnostics.Add("All query tokens were filtered out as stop-words.");
            return Task.FromResult(new KnowledgeAnswer(query.QueryText,
                Array.Empty<KnowledgeHit>(), Array.Empty<string>(), diagnostics));
        }

        var scores = new Dictionary<int, double>();
        foreach (var term in queryTerms)
        {
            if (!_index.TryGetValue(term, out var posting)) continue;
            var idf = _idf[term];
            foreach (var (chunkIdx, tf) in posting)
                scores[chunkIdx] = scores.GetValueOrDefault(chunkIdx) + tf * idf;
        }

        var hits = scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => _chunks[kv.Key].ChunkId, StringComparer.Ordinal)
            .Take(query.TopN)
            .Select(kv => new KnowledgeHit(
                _chunks[kv.Key].ChunkId,
                Snippet(_chunks[kv.Key].Text),
                Math.Round(kv.Value, 4)))
            .ToArray();

        if (hits.Length == 0)
            diagnostics.Add($"No chunks matched query terms: {string.Join(", ", queryTerms)}.");

        var sources = hits
            .Select(h => _chunks.First(c => c.ChunkId == h.ChunkId).DocumentSourceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(new KnowledgeAnswer(query.QueryText, hits, sources, diagnostics));
    }

    private IEnumerable<string> Tokenize(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
            else
            {
                if (sb.Length >= 2)
                {
                    var token = sb.ToString();
                    if (!_stopWords.Contains(token)) yield return token;
                }
                sb.Clear();
            }
        }
        if (sb.Length >= 2)
        {
            var token = sb.ToString();
            if (!_stopWords.Contains(token)) yield return token;
        }
    }

    private static string Snippet(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed.Substring(0, 240) + "…";
    }
}
