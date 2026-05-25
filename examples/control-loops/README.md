# Control Loop Examples

This pack shows three public DotNetAgents control-loop shapes with local,
synthetic state:

- **Durable workflow**: ordered steps that can pause at an approval checkpoint
  and resume later.
- **State machine**: explicit lifecycle states, guards, transitions, and
  transition history.
- **Behavior tree**: tactical policy selection with preferred branches and
  fallbacks.

The examples use public packages only and do not call hosted services.

## Run

```bash
dotnet run --project examples/control-loops -- --smoke
```

Focused commands:

```bash
dotnet run --project examples/control-loops -- workflow
dotnet run --project examples/control-loops -- state-machine
dotnet run --project examples/control-loops -- behavior-tree
dotnet run --project examples/control-loops -- compare
```

## What To Look For

The smoke output includes one public result envelope per capability. Those
envelopes make the examples easy to validate locally and easy to feed into
future public challenge or comparison experiences.

Use the workflow example when your process has ordered steps and checkpointed
resume. Use the state-machine example when legal lifecycle transitions are the
safety rule. Use the behavior-tree example when you need readable tactical
decision policy with fallbacks.

## Public Boundary

The examples are intentionally local-first. They use synthetic support-case
data, local traces, and no provider credentials. Premium packages can add
managed durability, evaluation, and factory automation around these patterns;
this repository keeps the code path public and inspectable.
