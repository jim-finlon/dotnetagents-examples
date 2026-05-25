# Protocol Examples

This pack demonstrates the public protocol split:

- **MCP** is for tools, IDEs, dashboards, and human-operated clients.
- **A2A** is for agent-to-agent task handoff and agent-card discovery.

The examples run offline with in-process fixtures. They do not require Core 4,
private LAN hosts, production credentials, or hosted DNA services.

## Smoke Mode

```bash
dotnet run --project examples/protocols -- --smoke
```

Expected result:

- JSON with `status` set to `passed`;
- `exampleCount` equal to `3`;
- result envelopes for MCP consumer, A2A handoff, and protocol boundary;
- zero network calls.

The checked-in expected transcript is
[`transcripts/smoke-output.json`](transcripts/smoke-output.json).

## Focused Commands

```bash
dotnet run --project examples/protocols -- mcp-consumer
dotnet run --project examples/protocols -- a2a-handoff
dotnet run --project examples/protocols -- boundary
```

## MCP Consumer

The MCP consumer example lists local tool definitions, calls
`summarize_protocol_note`, preserves a correlation id, and records a transcript.
It models the flow a CLI, IDE, dashboard, or human-operated tool client would
use when talking to a public-safe tool host.

## A2A Handoff

The A2A handoff example publishes an agent card, sends a local task to a
receiving agent, and preserves correlation metadata. It models agent-to-agent
handoff without depending on private worker pools or hosted services.

## Public Boundary

The protocol boundary command is intentionally terse. Public examples should
teach the protocol contract and route selection, not private operating mechanics.
