# Walkthrough: MCP Thin Host

`examples/mcp-thin-host-template` is the starter for an HTTP MCP service.

## What It Demonstrates

- minimal ASP.NET Core host
- `IMcpToolProvider`-style separation
- health endpoint
- MCP instructions/tools/call endpoints
- optional streamable MCP endpoint
- public-safe configuration placeholders

## Run It

```bash
cd examples/mcp-thin-host-template
dotnet run
```

Then inspect:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/mcp/instructions
curl http://localhost:5000/mcp/tools
```

Adjust the port if the template uses a different launch profile.

## Extend It

1. Rename the service.
2. Add one read-only tool.
3. Validate missing arguments.
4. Return a structured result.
5. Add a test for unknown tool behavior.

Keep service-specific behavior in the tool provider. Keep endpoint mapping,
health, CORS, auth, and transport setup in the host.

## Before Production

- add auth
- add OpenTelemetry
- add preview/confirm for mutating tools
- add configuration validation
- add failure tests
- document live environment variables
