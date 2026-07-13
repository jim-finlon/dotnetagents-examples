# Architecture Overview

DotNetAgents is organized as an open-core platform with a clear public/private
boundary.

## Public Layers

| Layer | Repository | What you get |
| --- | --- | --- |
| Core | `dotnetagents` | Agent runtime, workflows, tools, MCP/A2A surfaces, structured output, observability primitives |
| Plugins | `dotnetagents-plugins` | Optional provider and protocol adapters |
| Examples | `dotnetagents-examples` | Runnable apps plus the public docs source for `docs.dotnetagents.com` |

## Design Principles

1. **Software first.** Agents are durable .NET applications with DI, tools,
   workflows, and measurable results — not prompt-only scripts.
2. **Offline-first examples.** Smoke paths run without private DNA services.
3. **Protocol-aware.** MCP and A2A appear as explicit public surfaces.
4. **Honest boundary.** Premium DNA Factory capabilities (governed delivery,
   labs, eval receipts, credential custody, certification operations) stay
   commercial. Public docs may name the upgrade path without shipping private
   control-plane code.

## How An Example Fits Together

```text
Your app / example
  -> DotNetAgents core (runtime, tools, workflows)
  -> Optional plugins (providers, storage, protocol adapters)
  -> Local or vendor LLM endpoints you configure
```

Examples demonstrate:

- dependency-injected runtime setup
- clear tool boundaries
- workflow-oriented task execution
- public-safe MCP host patterns
- structured result envelopes suitable for later measurement

## Related Docs

- [Getting Started](getting-started.md)
- [API / Package Index](api-index.md)
- [Protocol Examples](protocol-examples.md)
- [Control Loop Examples](control-loop-examples.md)
- [Orchestration Examples](orchestration-examples.md)
- [Example Systems Showcase Roadmap](roadmap/example-systems-showcase.md)
