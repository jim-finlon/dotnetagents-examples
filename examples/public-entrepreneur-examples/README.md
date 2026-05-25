# Public Entrepreneur Example Pack

This pack rebuilds the original nine public examples as deterministic offline
smoke definitions. It is a compileable bridge for the public offering while the
full agent implementations are built out.

## Run

From the repository root:

```bash
dotnet run --project examples/public-entrepreneur-examples -- --smoke
```

Expected result: JSON with `"status": "passed"`, `exampleCount` equal to `9`,
and one `dotnetagents.public-example.result.v1` envelope for each example.

Useful exploratory commands:

```bash
dotnet run --project examples/public-entrepreneur-examples -- list
dotnet run --project examples/public-entrepreneur-examples -- run research-assistant
```

## Included Examples

- Research Assistant.
- Meeting Summarizer.
- Local Knowledge-Base Assistant.
- Proposal Writer.
- Invoice Helper.
- Content Repurposer.
- Customer-Support Triage.
- Writing Assistant.
- Educational Tutor.

The pack uses only `DotNetAgents.Core` and public sample code. It does not
connect to private factory services, hosted state, credential custody, evaluator
loops, or production data.

## Related public samples

- [`examples/business-operations/`](../business-operations/README.md) —
  commodity business-agent examples (project planner, CRM follow-up,
  communications triage, appointment + time-management).
