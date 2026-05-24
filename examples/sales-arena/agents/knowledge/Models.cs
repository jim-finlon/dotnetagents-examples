using System.Collections.Generic;

namespace SalesArena.Knowledge.Agent;

public sealed record KnowledgeDocument(string SourceId, string RelativePath, string Title, string Body);

public sealed record KnowledgeChunk(string ChunkId, string DocumentSourceId, string Heading, string Text);

public sealed record KnowledgeQuery(string QueryText, int TopN = 5);

public sealed record KnowledgeHit(string ChunkId, string Snippet, double Score);

public sealed record KnowledgeAnswer(
    string QueryText,
    IReadOnlyList<KnowledgeHit> Hits,
    IReadOnlyList<string> CitedSources,
    IReadOnlyList<string> Diagnostics);
