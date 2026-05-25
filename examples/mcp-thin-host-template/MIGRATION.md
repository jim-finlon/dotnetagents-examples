# Thin MCP Host Migration Notes

These notes are for existing DNA services that already expose MCP endpoints but
still carry bespoke `Program.cs` startup code. The target is not a new framework
inside every app. The target is a smaller host where each service owns its tools
and the shared MCP seams own the repeatable transport behavior.

## Target Pattern

Move toward this shape:

1. Keep service stores, adapters, policies, and background workers as normal
   service registrations.
2. Keep one concrete `IMcpToolProvider` per service as the owner of tool names,
   categories, argument validation, preview/confirm semantics, and response
   guidance.
3. Register optional shared seams by capability:
   - `AddAgentLearningProjection(...)`
   - `AddGeneticContractReader(...)`
   - `McpLearningDecorator`
4. Map MCP and health endpoints in a small, predictable block:
   - `UseCursorMcpCors()`
   - optional API key or edge-auth middleware
   - `MapMcpEndpoints(serviceName, true, instructions)`
   - `MapMcpStreamableHttp(serviceName, displayName, version)`
   - `/health`, `/`, and optional `/genetic/contract`

## Incremental Migration

Start with the smallest safe diff:

1. Add or identify the service's concrete `IMcpToolProvider`.
2. Move tool metadata and tool-call dispatch out of `Program.cs` if any remains
   there.
3. Keep existing service dependencies and endpoint behavior unchanged.
4. Add `GET /health` if missing.
5. Replace bespoke `/mcp/tools` and `/mcp/tools/call` wiring with
   `MapMcpEndpoints(...)`.
6. Add `MapMcpStreamableHttp(...)` only after basic tool listing/call behavior
   is covered by tests.
7. Add learning projection and genetic-contract mapping after the host has
   stable service-name and tool-provider semantics.

## Validation Checklist

- `GET /health` returns a small non-secret health payload.
- `GET /mcp/instructions` returns the service bootstrap.
- `GET /mcp/tools` includes `get_instructions` plus service-specific tools.
- `POST /mcp/tools/call` returns `NOT_FOUND` for unknown tools.
- Auth rejects mutating or high-impact calls when the configured API key is
  missing or invalid.
- Optional streamable MCP endpoint is mapped with the same service name.
- Optional `/genetic/contract` returns policy metadata and does not expand
  production authority.
- Optional learning projection is a decorator around tool execution, not a
  dependency every tool must know about.

## Existing Host Reality Check

`security-scanning-agent/src/SecurityScanningAgent.Api/Program.cs` already proves
the pattern can support:

- API-key middleware with public health/instructions exceptions.
- `AddAgentLearningProjection(...)`.
- `AddGeneticContractReader(...)`.
- `McpLearningDecorator`.
- `MapMcpEndpoints(...)`.
- `MapMcpStreamableHttp(...)`.
- `/genetic/contract`.
- A large service-specific `SecurityScanningMcpToolProvider` with preview,
  confirmation, redaction, and allowlist rules.

`infrastructure-control-agent/src/InfrastructureControl.Api/Program.cs` already
proves the same pattern can support:

- Larger service dependency graphs.
- SignalR/dashboard endpoints alongside MCP.
- Learning projection to multiple targets.
- Genetic-contract endpoint mapping.
- Tool-provider ownership of high-impact infrastructure preview/confirm tools.

New services should start from the sample template instead of copying either of
those hosts wholesale. Existing services should migrate by aligning one block at
a time, preserving behavior and tests after each step.

## Anti-Patterns To Avoid

- Copying an arbitrary service `Program.cs` into a new service.
- Registering tools directly in endpoint lambdas.
- Putting secret values in appsettings samples.
- Letting learning projection or genetic-contract support leak into every tool
  method.
- Creating a second MCP transport shape for the same service without a clear
  compatibility reason.
