# DotNetAgents MCP Thin Host Template

This sample is the public starting point for a new DotNetAgents HTTP MCP service.
It keeps the host thin: service-specific behavior belongs in an
`IMcpToolProvider`, while shared host seams own endpoint mapping, streamable MCP,
event projection, policy metadata reading, CORS, health, and optional auth.

Use this template when creating a new service that should expose:

- `GET /health`
- `GET /mcp/instructions`
- `GET /mcp/tools`
- `POST /mcp/tools/call`
- optional streamable MCP HTTP
- optional policy metadata endpoint
- optional agent-event projection around tool calls

## Files

| File | Purpose |
|---|---|
| `src/Program.cs` | Minimal host composition and endpoint mapping. |
| `src/SampleMcpToolProvider.cs` | Service-specific tools behind `IMcpToolProvider`. |
| `appsettings.sample.json` | Placeholder config with no secrets. |
| `MIGRATION.md` | How existing thin hosts move toward this pattern. |

## Host Shape

The preferred pattern is:

1. Register service-specific stores/adapters.
2. Register optional shared seams:
   - `AddAgentEventProjection(...)`
   - `AddPolicyMetadataReader(...)`
3. Register the concrete tool provider.
4. Register `IMcpToolProvider` directly or through `McpLearningDecorator`.
5. Build the app.
6. Add common middleware:
   - `UseCursorMcpCors()`
   - optional API-key middleware from config/env
7. Map common endpoints:
   - `/health`
   - `/`
   - optional `/policy/metadata`
   - `MapMcpEndpoints(...)`
   - optional `MapMcpStreamableHttp(...)`

## Security Notes

- Do not put API keys or tokens in this sample.
- Use config/env placeholders and resolve real secrets through your own secure
  configuration provider.
- Keep unauthenticated endpoints narrow: typically `/health`, `/`, and
  `/mcp/instructions`.
- High-impact tools should implement preview/confirm semantics in the tool
  provider rather than bypassing policy in `Program.cs`.

## When To Deviate

Use a custom host only when the service truly needs additional transports,
specialized middleware, or non-HTTP runtime behavior. Even then, keep the MCP
surface and tool-provider contract aligned with this template.
