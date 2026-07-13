# Getting Started With DotNetAgents Examples

This is the public getting-started page for `docs.dotnetagents.com`.

DotNetAgents is an open .NET framework for building observable, tool-using,
multi-agent systems. These examples run locally without DNA private control-plane
services or other hosted factory runtimes.

## Prerequisites

- .NET 10 SDK
- Git
- Optional provider keys only for live extensions (smoke paths stay offline)

## Clone And Smoke

```bash
git clone https://github.com/jim-finlon/dotnetagents-examples.git
cd dotnetagents-examples
dotnet run --project examples/hello-agent-cs -- --smoke
dotnet run --project examples/foundation -- --smoke
```

Expect a successful exit and structured JSON output. Many examples emit the
`dna.public-example.result.v1` envelope with `"status": "passed"`.

## Recommended First Path

1. Run `examples/hello-agent-cs` — smallest console shape.
2. Run `examples/foundation` — tools, structured output, routing, retry.
3. Read [Choosing An Example](choosing-an-example.md) and pick a domain pack.
4. Follow a walkthrough:
   - [Hello Agent](walkthroughs/hello-agent.md)
   - [MCP Thin Host](walkthroughs/mcp-thin-host.md)
   - [Document Extraction](walkthroughs/document-extraction.md)

## Source-Based Consumption (No NuGet Assumption)

The first public cut is a **source release** (`v0.1.0-preview`). Until a
distribution channel is decided, consume DotNetAgents by cloning the public
repos and building from source or ProjectReference:

| Repo | GitHub mirror | Role |
| --- | --- | --- |
| `dotnetagents` | https://github.com/jim-finlon/dotnetagents | Public core packages |
| `dotnetagents-plugins` | https://github.com/jim-finlon/dotnetagents-plugins | Optional adapters |
| `dotnetagents-examples` | https://github.com/jim-finlon/dotnetagents-examples | Runnable examples + this docs tree |

Release tags:

- https://github.com/jim-finlon/dotnetagents/releases/tag/v0.1.0-preview
- https://github.com/jim-finlon/dotnetagents-plugins/releases/tag/v0.1.0-preview
- https://github.com/jim-finlon/dotnetagents-examples/releases/tag/v0.1.0-preview

<!-- PACKAGE-INSTALL-INSERTION-POINT
When a package channel is approved, add `dotnet add package ...` instructions
here for the R4 versions. Do not invent nuget.org package ids before that.
-->

## What Stays Out Of Scope

Public examples may mention where premium upgrades fit. They must not implement
private factory services, managed credential custody, hosted workflows,
proprietary media continuity, scoring/certification engines, mutation loops,
promotion gates, or private benchmark data.

## Next Docs

- [Architecture Overview](architecture-overview.md)
- [API / Package Index](api-index.md)
- [Running Examples](running-examples.md)
- [Troubleshooting](troubleshooting.md)
