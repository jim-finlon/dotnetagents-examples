# Customer Support Triage Example

This sample demonstrates an offline customer support triage agent that classifies incoming tickets, stubs knowledge-base lookups, decides whether to resolve or escalate the issue, and generates a support transcript.

## Commands

From the repository root:

```bash
# Run deterministic offline smoke checks
dotnet run --project public/dotnetagents-examples/examples/customer-support -- --smoke

# List available support cases
dotnet run --project public/dotnetagents-examples/examples/customer-support -- list

# Run a specific support case
dotnet run --project public/dotnetagents-examples/examples/customer-support -- run billing-issue
dotnet run --project public/dotnetagents-examples/examples/customer-support -- run login-error
```

## Public / Private Boundary

- **Public**: Basic open-core C# `DotNetAgents` abstractions for ticket categorization, routing, KB lookups, and serialization.
- **Private**: Automated live routing to the SdlcAgent operations desk, agent token metrics logging, and enterprise CRM credentials (held in CredentialsAgent).
