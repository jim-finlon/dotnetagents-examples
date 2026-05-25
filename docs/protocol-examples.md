# Protocol Examples

DotNetAgents exposes two protocol families in the public package train:

- **MCP** for tools, IDEs, dashboards, CLIs, and human-operated clients.
- **A2A** for agent-to-agent discovery, task handoff, and event streams.

The examples in this repository keep that split visible without requiring any
private DNA services.

## Run The Protocol Pack

```bash
dotnet run --project examples/protocols -- --smoke
```

Focused commands:

```bash
dotnet run --project examples/protocols -- mcp-consumer
dotnet run --project examples/protocols -- a2a-handoff
dotnet run --project examples/protocols -- boundary
```

## Run The Thin MCP Host Smoke

```bash
dotnet run --project examples/mcp-thin-host-template -- --smoke
```

The smoke command validates the public host shape without binding a port:

- instructions bootstrap;
- tool listing;
- tool call success;
- unknown-tool rejection;
- local transcript shape.

To run the HTTP server itself:

```bash
dotnet run --project examples/mcp-thin-host-template
```

The sample host maps `/health`, `/mcp/instructions`, `/mcp/tools`,
`/mcp/tools/call`, and streamable MCP HTTP endpoints.

## Choosing MCP Or A2A

Use MCP when the caller is a tool client: an IDE, CLI, dashboard, notebook, or a
human-operated integration surface.

Use A2A when the caller is another agent that needs to discover capabilities,
send a task, and receive a response or stream of events.

The premium platform uses the same public protocol vocabulary before adding
managed trust, routing, evaluation, and factory automation around it. The public
examples intentionally stop at the protocol contract and local execution shape.

## Public Safety Rules

- Keep sample endpoints localhost, in-process, or placeholder-only.
- Do not include private LAN hosts, production URLs, or real tokens.
- Use synthetic requests and transcripts.
- Keep private laboratory, simulator, and Arena operating mechanics out of
  public examples.
