# Business Operations Example Pack

This pack provides public, commodity business-agent examples that are useful
without private factory services. Each example emits the public result envelope
so future competition or hosted ingestion paths can consume the same shape.

## Run

From the repository root:

```bash
dotnet run --project samples/business-operations -- --smoke
```

Expected result: JSON with `"status": "passed"`, `exampleCount` equal to `5`,
and one `dna.public-example.result.v1` envelope for each example.

Useful exploratory commands:

```bash
dotnet run --project samples/business-operations -- list
dotnet run --project samples/business-operations -- run project-planner
```

## Included Examples

- Basic project planner.
- Basic CRM follow-up.
- Communications triage.
- Appointment assistant.
- Time-management assistant.

These examples intentionally stay in the public lane: local sample data,
public package references, no private credential custody, no managed workflow
state, no hosted automation surface, and no evaluator or promotion loop.

## Related public samples

- [`samples/sales-evaluation/`](../sales-evaluation/README.md) — the flagship
  multi-agent Sales evaluation that exercises 30+ public DNA packages.
  
- [`samples/public-entrepreneur-examples/`](../public-entrepreneur-examples/README.md)
  — the nine commodity public-example agents (research assistant,
  meeting summarizer, proposal writer, etc.).
