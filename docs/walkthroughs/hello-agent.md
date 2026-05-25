# Walkthrough: Hello Agent

`examples/hello-agent-cs` is the smallest public DotNetAgents shape. Use it to
prove your local toolchain works before adding services, providers, or plugins.

## What It Demonstrates

- command-line entry point
- structured result output
- simple agent identity shape
- offline smoke path
- public result envelope concept

## Run It

```bash
cd examples/hello-agent-cs
dotnet run -- --smoke
```

Expected result:

- process exits successfully
- output is JSON
- no live provider key is needed
- no hosted service is contacted

## Change It

Start with a tiny edit:

1. Find the greeting logic.
2. Change the message text.
3. Run the smoke command again.
4. Confirm the result envelope still reports success.

## Why This Matters

This example proves the first engineering loop:

- edit code
- run locally
- get structured output
- verify behavior without a production dependency

That loop is the foundation for larger agents. Add tools, workflows, memory, or
plugins only after the smallest loop is easy to understand.
