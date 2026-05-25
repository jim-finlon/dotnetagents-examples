# DotNetAgents Examples

This repository contains public, runnable examples for the DotNetAgents platform.

DotNetAgents is a .NET 10 platform for building production agents that can plan,
act, observe, improve, and operate under governance. The examples here show how
to use the public package train without depending on private hosted services,
premium code, customer projects, or internal infrastructure.

Start here when you want to see what the platform feels like as software.

The examples intentionally show the public foundation, not the private factory
machinery. The same patterns are leveraged in private agent repositories for
governed delivery, evaluation, and operations, while premium packages add
managed labs, certification receipts, and advanced Arena packs.

## Example Catalog

| Example | Purpose |
| --- | --- |
| `examples/hello-agent-cs` | Smallest runnable console shape for a DotNetAgents app. |
| `examples/document-extraction` | Public document extraction worker pattern. |
| `examples/public-entrepreneur-examples` | Starter agents for solo and small-business workflows. |
| `examples/business-operations` | CRM, calendar, communication, and project-management patterns. |
| `examples/education` | Tutor and classroom-oriented public examples. |
| `examples/writing-publishing` | Writing and publishing assistant patterns. |
| `examples/mcp-thin-host-template` | Public MCP thin-host starter. |
| `examples/road-access-dotnet-consumer` | Consumer integration sample. |

The next public release tranches are tracked as the Example Systems Showcase:
foundation patterns, protocol examples, orchestration loops, control-loop
systems, business/developer/RAG demos, plugin showcases, and a public-safe
game-style teaser. See the roadmap for story ids and delivery order.

## How This Fits With DotNetAgents

- `dotnetagents` is the public core platform.
- `dotnetagents-plugins` contains optional integration adapters.
- `dotnetagents-examples` shows the public packages in runnable applications.

## Documentation

The `/docs` tree explains how to use and adapt the examples:

- [Example Systems Showcase Roadmap](docs/roadmap/example-systems-showcase.md)
- [Example Catalog](docs/example-catalog.md)
- [Example Contract](docs/example-contract.md)
- [Choosing An Example](docs/choosing-an-example.md)
- [Running Examples](docs/running-examples.md)
- [Extending Examples](docs/extending-examples.md)
- [Result Envelopes And Arena Compatibility](docs/result-envelopes-and-arena.md)
- [Troubleshooting](docs/troubleshooting.md)

Main platform repository:

https://github.com/jim-finlon/dotnetagents

Comparison guide:

https://github.com/jim-finlon/dotnetagents/blob/main/COMPARISON.md

## What The Examples Demonstrate

These examples focus on agent systems as durable .NET software:

- dependency-injected runtime setup
- clear tool boundaries
- workflow-oriented task execution
- public-safe MCP host patterns
- structured application surfaces instead of prompt-only scripts
- starter patterns for agents that can be measured and improved over time

The examples intentionally avoid private implementation details. They should be
safe to read, fork, and adapt as public starter code.

The machine-readable catalog lives at
[`examples/catalog.v1.json`](examples/catalog.v1.json). Future example lanes
should update that file whenever they add, rename, mature, or retire an example.

## Quick Start

Install .NET 10, clone the repository, and inspect an example:

```bash
git clone https://github.com/jim-finlon/dotnetagents-examples.git
cd dotnetagents-examples/examples/hello-agent-cs
dotnet run
```

Some examples may require package sources or preview packages that match the
current DotNetAgents package train. Check the individual project files for exact
package references.

## Public-Safe Scope

This repository does not include private factory workflows, hosted-service
internals, private memory services, credential custody internals, proprietary
prompts, private LAN hosts, or commercial media workflow code.

If an example needs those capabilities, it should use a public abstraction,
document the missing external dependency, or remain out of this repository until
it can be presented safely.

Roadmap examples will include more workflow agents, MCP/A2A service templates,
memory patterns, voice and multimodal flows, and a public-friendly gamified
Arena experience for comparing agent strategies without exposing proprietary
scoring internals.

## License

DotNetAgents examples are licensed under Apache-2.0. See `LICENSE` and `NOTICE`.
