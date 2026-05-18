# Education Example Pack

This public pack covers tutoring, study planning, and quiz coaching as
deterministic offline examples.

## Run

```bash
dotnet run --project samples/education -- --smoke
```

Expected result: JSON with `"status": "passed"`, `exampleCount` equal to `3`,
and public result envelopes for each example.

The pack does not include private assessment systems, instructor dashboards,
managed learner memory, certification-grade scoring, or hosted classroom
services.
