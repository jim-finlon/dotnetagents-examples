# DotNetAgents Public Docs (`docs.dotnetagents.com`)

This tree is the public technical docs source for DotNetAgents examples and the
go-live content for `docs.dotnetagents.com`.

## Start Here

1. [Getting Started](getting-started.md) — clone, smoke, first path
2. [Architecture Overview](architecture-overview.md) — public layers and boundary
3. [API / Package Index](api-index.md) — repos, tags, protocol landing links

## Example Guides

1. [Example Systems Showcase Roadmap](roadmap/example-systems-showcase.md)
2. [Example Catalog](example-catalog.md)
3. [Example Contract](example-contract.md)
4. [Example Quality Gates](example-quality-gates.md)
5. [Friction Ledger](friction-ledger.md)
6. [Protocol Examples](protocol-examples.md)
7. [Control Loop Examples](control-loop-examples.md)
8. [Orchestration Examples](orchestration-examples.md)
9. [Choosing An Example](choosing-an-example.md)
10. [Running Examples](running-examples.md)
11. [Extending Examples](extending-examples.md)
12. [Result Envelopes And Arena Compatibility](result-envelopes-and-arena.md)
13. [Troubleshooting](troubleshooting.md)

## Walkthroughs

- [Hello Agent](walkthroughs/hello-agent.md)
- [MCP Thin Host](walkthroughs/mcp-thin-host.md)
- [Document Extraction](walkthroughs/document-extraction.md)

## Hosting Note

Static docs publishing for `docs.dotnetagents.com` reuses the existing DigitalOcean
/ dna-web platform path. See [HOSTING-RECONCILIATION.md](HOSTING-RECONCILIATION.md).
DNS flips remain operator-gated where the registrar requires a human.

The examples are intentionally public-safe. They show runnable patterns without
private hosted services, private datasets, premium code, or operational runbooks.
