# API And Package Index

This index is the public package reference landing page for
`docs.dotnetagents.com`. It points at the open-core repositories and the
example catalog rather than duplicating generated API reference pages.

## Public Repositories (v0.1.0-preview)

| Package train | Repository | Start here |
| --- | --- | --- |
| Core | https://github.com/jim-finlon/dotnetagents | Root README + package projects under `src/` |
| Plugins | https://github.com/jim-finlon/dotnetagents-plugins | Adapter packages for providers and protocols |
| Examples | https://github.com/jim-finlon/dotnetagents-examples | Runnable samples under `examples/` |

Release tag for all three: **`v0.1.0-preview`**.

## Source Consumption

Until a package feed is an approved distribution channel, prefer:

```bash
git clone https://github.com/jim-finlon/dotnetagents.git
git clone https://github.com/jim-finlon/dotnetagents-plugins.git
git clone https://github.com/jim-finlon/dotnetagents-examples.git
```

Then open an example project and use ProjectReference (or a local package feed
documented by that example) to the core packages you need.

<!-- PACKAGE-INSTALL-INSERTION-POINT
Future NuGet / feed install commands land here after an operator-approved
distribution decision. Do not document nuget.org package ids prematurely.
-->

## Example Catalog (Runnable Surface)

Machine-readable catalog:

- [`../examples/catalog.v1.json`](../examples/catalog.v1.json)

Human guides:

- [Example Catalog](example-catalog.md)
- [Choosing An Example](choosing-an-example.md)
- [Running Examples](running-examples.md)

## Protocol Surfaces

| Surface | Docs |
| --- | --- |
| MCP thin host | [MCP Thin Host walkthrough](walkthroughs/mcp-thin-host.md) |
| MCP / A2A examples | [Protocol Examples](protocol-examples.md) |
| Control loops | [Control Loop Examples](control-loop-examples.md) |
| Multi-role orchestration | [Orchestration Examples](orchestration-examples.md) |

## Comparison And Positioning

- Platform comparison guide (core repo):  
  https://github.com/jim-finlon/dotnetagents/blob/main/COMPARISON.md
- Architecture overview: [architecture-overview.md](architecture-overview.md)
