# Meeting Assistant Example

This sample demonstrates an offline meeting assistant agent that ingests notes, extracts decisions and action items with owners and due dates, and generates a follow-up draft email.

## Commands

From the repository root:

```bash
# Run deterministic offline smoke checks
dotnet run --project public/dotnetagents-examples/examples/meeting-assistant -- --smoke

# List available support cases
dotnet run --project public/dotnetagents-examples/examples/meeting-assistant -- list

# Run a specific support case
dotnet run --project public/dotnetagents-examples/examples/meeting-assistant -- run weekly-sync
dotnet run --project public/dotnetagents-examples/examples/meeting-assistant -- run project-kickoff
```

## Public / Private Boundary

- **Public**: Basic open-core C# `DotNetAgents` abstractions for decision and action-item extraction, follow-up composition, and validation.
- **Private**: Automated background calendar sync to enterprise mail systems, persistent meeting transcripts database, and live meeting summaries processed via premium LLM gateways.
