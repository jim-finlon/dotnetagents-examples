# Control Loop Examples

Control loops make agent behavior inspectable. Instead of hiding a process in a
single prompt, you describe how work advances, where it can stop, and what
evidence proves each step.

Run the public pack:

```bash
dotnet run --project examples/control-loops -- --smoke
```

## Workflow

Use a workflow when work has ordered steps: intake, classify, draft, approve,
publish. The sample pauses at `human-approval`, records a checkpoint, and
resumes with the same synthetic state after approval is granted.

```bash
dotnet run --project examples/control-loops -- workflow
```

Choose this shape when you need:

- step evidence;
- retry or resume points;
- human approval;
- repeatable business process.

## State Machine

Use a state machine when lifecycle correctness matters. The sample ticket moves
from `New` to `Triaged` to `WaitingForApproval` to `Resolved`, with guards
controlling which transitions are legal.

```bash
dotnet run --project examples/control-loops -- state-machine
```

Choose this shape when you need:

- explicit valid states;
- guarded transitions;
- transition history;
- lifecycle-driven safety.

## Behavior Tree

Use a behavior tree when policy should try preferred decisions before fallback
decisions. The sample support policy tries direct resolution first and falls
back to requesting more context.

```bash
dotnet run --project examples/control-loops -- behavior-tree
```

Choose this shape when you need:

- readable tactical policy;
- condition/action composition;
- fallback paths;
- localized decision traces.

## Pattern Comparison

```bash
dotnet run --project examples/control-loops -- compare
```

| Pattern | Best Fit |
| --- | --- |
| Durable workflow | Ordered work that pauses, resumes, retries, or waits for approval. |
| State machine | Lifecycle state and legal transitions are the key safety rule. |
| Behavior tree | Tactical policy should try preferred branches before fallbacks. |

## How This Connects To Premium Packages

The public examples show the vocabulary: workflows, states, policies, traces,
and result envelopes. Premium packages can add managed durability, simulation,
counter-agent review, and software-factory automation around those same ideas.
The code here intentionally stops at local execution and public-safe evidence.
