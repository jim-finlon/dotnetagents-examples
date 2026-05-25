# Troubleshooting

## `dotnet` Command Not Found

Install the .NET 10 SDK and restart your shell. Verify:

```bash
dotnet --info
```

## Package Restore Fails

The public package train is preview. Check:

- package version in the project file
- configured NuGet sources
- whether the example expects project references instead of published packages
- network access to your package source

## A Live Provider Call Fails

Confirm:

- the live mode environment variable is set
- the provider key environment variable is set
- the key has permission for the requested operation
- the endpoint is reachable
- the example supports live mode

Never print the raw key while debugging.

## MCP Client Cannot See Tools

Check:

- the host is running
- `/health` responds
- `/mcp/instructions` responds
- `/mcp/tools` returns a tool list
- CORS or auth settings match the client

Start with the thin host template before adding domain logic.

## Output Is Not Deterministic

For examples and tests, prefer deterministic modes:

- fake provider
- fixed seed
- local test data
- stable timestamps when supported
- no live network call

Use live calls for integration checks, not for the basic smoke path.
