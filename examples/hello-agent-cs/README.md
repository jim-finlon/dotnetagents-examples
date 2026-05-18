# Hello DNA Agent C# Sample

This is the first-run sample for the engineering distribution pack. It is
deliberately offline: no Tyr, platform service, live credentials, or deployment target is
required.

## Run

From the repository root:

```bash
dotnet run --project samples/hello-agent-cs -- --smoke
```

Expected result: JSON with `"status": "passed"` and a
`resultEnvelope.schemaVersion` value of `dna.public-example.result.v1`.

Useful exploratory commands:

```bash
dotnet run --project samples/hello-agent-cs -- card
dotnet run --project samples/hello-agent-cs -- hello "DNA developer"
```

## What This Demonstrates

- **A2A shape:** the `card` command exposes the sample agent identity and the
  `/.well-known/agent.json` route an A2A host would publish.
- **MCP shape:** the sample lists two tiny tools, `hello` and `card`, and keeps
  tool output structured.
- **Lesson shape:** the smoke command emits a `lesson.event.v1`-style local
  record with a stable problem signature.
- **Result envelope:** the smoke command emits a public, competition-compatible
  result file shape with a schema version, example id/version, run id,
  timestamp, input-summary hash, output artifact refs, local validation summary,
  and self-reported metrics.

The sample does not start an HTTP server. The goal is a small first success
before a developer graduates to the fuller MCP thin-host template at
[`../mcp-thin-host-template`](../mcp-thin-host-template).

The result envelope is a file format only. It is not a score, certification,
benchmark verdict, evaluator implementation, tournament runner, mutation loop,
promotion gate, or hosted competition engine.

## First Change

Edit `Program.cs`, change the greeting text in `HandleHello`, and rerun:

```bash
dotnet run --project samples/hello-agent-cs -- --smoke
```

If the smoke still passes, the local edit loop is working.
