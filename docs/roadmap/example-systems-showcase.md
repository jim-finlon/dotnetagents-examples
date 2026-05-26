# Example Systems Showcase Roadmap

DotNetAgents examples are being expanded into a public portfolio of runnable agent systems. The goal is simple: a developer should be able to clone the examples, run real .NET agent applications, and see how the public platform grows into serious production patterns.

The examples also serve as pressure tests. When an example exposes awkward setup, missing docs, missing helpers, or plugin friction, that becomes core/plugin improvement work.

## What Is Coming

| Family | What it demonstrates | SDLC story |
| --- | --- | --- |
| Catalog spine | Metadata, smoke/live contract, runnable index. | `b2e48ea3-d075-4a72-a574-9bb1072ffbbd` |
| Foundation | Tools, structured output, streaming, model routing, retries, usage. | `a65f4fc8-e7ca-4216-8605-ac69c181c402` |
| Protocol | MCP host/client and A2A handoff examples. | `ae811ae3-c856-4828-858a-9c3bbf46281f` |
| Orchestration | Writer/editor/judge, planner/executor/verifier, human approval. | `14628e9f-61c7-4b83-99e5-490262208641` |
| Control loops | Durable workflows, state machines, behavior trees, resume. | `29c8fc48-aa98-4d8d-97fe-e3279cf47c0d` |
| Business systems | Customer support, meeting assistant, operations triage. | `58b88b04-c80c-4a1b-9af6-851aa4eeb1d3` |
| Developer systems | Code review, release notes, docs maintenance, test authoring. | `4675e1fe-81bb-4bb4-bd16-b5de750c01ab` |
| RAG/data | Knowledge assistant, document correction, citation verifier. | `4fea72a0-99f5-44f3-b071-9ceea0e04756` |
| Plugin showcase | Vector, messaging, storage, database, browser, UI, multimodal. | `815104bf-32b4-4ef9-ab73-53133c8ab386` |
| Public game-style teaser (Implemented) | Gamified agent comparison with public-safe result summaries. | `a09cd058-42ef-43cd-9b01-14c9d7877fa3` |

## Quality Bar

Every new example should include:

- A local smoke path with no API keys.
- Optional live mode with environment-variable configuration.
- Typed output, transcript, or result envelope.
- README with safety and extension notes.
- Catalog entry.
- Public leak scan and build/test evidence.

## Public/Premium Boundary

The public examples show the open-core foundation. Premium DNA Factory packages build on these patterns with managed evaluation environments, certification, simulator-style hardening, and autonomous software-factory operation where human review can remain in the loop.

The public examples do not include private prompts, private datasets, proprietary scoring, internal lab mechanics, private SDLC operations, or hosted-service internals.
