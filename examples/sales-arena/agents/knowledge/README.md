# Knowledge Agent

Story `d900dfc6` (SA-01-08e). Deterministic, offline keyword-retrieval agent over
the Sales Arena knowledge corpus.

The on-disk corpus lives at [`examples/sales-arena/knowledge/`](../../knowledge/)
(40+ Markdown files: persona objection scripts, lead-pack briefs, pricing card,
templates). This slice ships an in-memory inverted-index retrieval layer over
that corpus.

## Surface

- `IKnowledgeAgent.AnswerAsync(KnowledgeQuery)` → `KnowledgeAnswer` with top-N
  `KnowledgeHit` items + distinct `CitedSources` + diagnostics.
- `KnowledgeAgent` — deterministic indexer: lowercase token + stop-word filter,
  inverted index from term → (chunkIdx → tf), TF × IDF ranking, 240-char
  snippets. Stable ordinal tiebreak by chunk id.
- `IKnowledgeSource` interface with two impls:
  - `FileSystemKnowledgeSource(root)` — reads `*.md` recursively, splits by
    `#` / `##` heading boundaries, stable `{relativePath}#chunk-{index}` ids.
  - `InMemoryKnowledgeSource` — explicit chunk list for tests.
- Records: `KnowledgeDocument`, `KnowledgeChunk`, `KnowledgeQuery`,
  `KnowledgeHit`, `KnowledgeAnswer`.

## Deferred to follow-up

- Embedding-based retrieval via `DotNetAgents.AgenticRAG`.
- Integration with `DotNetAgents.Knowledge` indexing pipeline so the Sales Arena
  reuses the platform's persisted-index format.
- Query expansion / LLM rewrite + multi-turn dialog memory.
- Hot-reload of the corpus when files change on disk.
- Integration with Training Agent's post-contest analysis (cite the corpus
  passages that informed each next-best-action decision).
