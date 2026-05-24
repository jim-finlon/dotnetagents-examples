# Hello DNA Agent Python Sample

This is the Python sibling of the engineering distribution pack's first-run
sample. The primary DotNetAgents 10 example remains
[`../hello-agent-cs`](../hello-agent-cs); this sample is a cross-language shape
check for the same A2A, MCP, lesson-event, and result-envelope concepts. It is
deliberately offline: no Tyr, Core 4, live credentials, or deployment target is
required.

## Run

From the repository root:

```bash
python3 -m samples.hello_agent_py.run --smoke
```

Expected result: JSON with `"status": "passed"` and a
`resultEnvelope.schemaVersion` value of `dna.public-example.result.v1`.

Useful exploratory commands:

```bash
python3 -m samples.hello_agent_py.run card
python3 -m samples.hello_agent_py.run hello "DNA developer"
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
  timestamp, input-summary hash, output artifact refs, local validation
  summary, and self-reported metrics.

The sample does not start an HTTP server. The goal is a small first success
before a developer graduates to a fuller MCP or A2A host.

The result envelope is a file format only. It is not a score, certification,
benchmark verdict, evaluator implementation, tournament runner, mutation loop,
promotion gate, or hosted competition engine.

## First Change

Edit `../hello_agent_py/run.py`, change the greeting text in `handle_hello`,
and rerun:

```bash
python3 -m samples.hello_agent_py.run --smoke
```

If the smoke still passes, the local edit loop is working.
