# Orchestration Examples

Orchestration means more than asking several agents to chat. A useful system
assigns roles, preserves typed handoffs, and records the evidence needed to
trust the outcome.

Run the public pack:

```bash
dotnet run --project examples/orchestration -- --smoke
```

## Writer, Editor, Judge

```bash
dotnet run --project examples/orchestration -- writer-editor-judge
```

Use this pattern when a draft should improve through critique and a final
verdict. The sample keeps the handoff typed: request, draft sections, edits, and
judge verdict.

## Planner, Executor, Verifier

```bash
dotnet run --project examples/orchestration -- planner-executor-verifier
```

Use this pattern when work should be checked by a role that did not execute it.
The sample creates a plan, produces step evidence, and verifies the required
evidence list.

## Preview And Confirm

```bash
dotnet run --project examples/orchestration -- approval
```

Use this pattern when a proposed action should be visible before it is
committed. The sample preview does not mutate external state, refuses an invalid
token, and confirms the matching preview with a local evidence reference.

## Pattern Comparison

```bash
dotnet run --project examples/orchestration -- compare
```

| Pattern | Best Fit |
| --- | --- |
| Writer/editor/judge | Draft quality improves through critique and an independent verdict. |
| Planner/executor/verifier | Execution needs a separate verification gate. |
| Preview/confirm approval | A proposed action needs explicit approval before commit. |

## Optional Live Extensions

The smoke path is deterministic. To extend a role with a live model, keep the
same typed handoff shape and add:

- a model allowlist;
- a maximum token and cost budget;
- a deterministic fallback transcript;
- the same public result envelope.

The public repository should not include production approval systems, private
review prompts, or scoring internals.
