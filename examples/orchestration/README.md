# Orchestration Examples

This pack demonstrates multi-role agent patterns as deterministic .NET code:

- **Writer/editor/judge**: generation, critique, revision, and verdict.
- **Planner/executor/verifier**: separate planning, execution, and checking.
- **Preview/confirm approval**: show a proposed action, refuse the wrong token,
  then confirm the matching preview.

The examples are local and synthetic. They do not call model providers unless
you extend them yourself.

## Run

```bash
dotnet run --project examples/orchestration -- --smoke
```

Focused commands:

```bash
dotnet run --project examples/orchestration -- writer-editor-judge
dotnet run --project examples/orchestration -- planner-executor-verifier
dotnet run --project examples/orchestration -- approval
dotnet run --project examples/orchestration -- compare
```

## Why These Patterns Matter

Single-agent scripts are easy to start but hard to trust. Orchestration makes
the roles visible: who drafted, who checked, what was approved, and what
evidence proves the run.

Premium packages can connect these public patterns to managed evaluation,
approval dashboards, and autonomous software-factory workflows. The public
examples keep the code path small, inspectable, and safe to fork.

## Public Boundary

All inputs are synthetic. The approval example writes no external state; it only
returns a local evidence reference. Keep live provider extensions bounded by a
cost cap, a model allowlist, and the same result-envelope shape.
