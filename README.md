# DotNetAgents Examples

Open-source sample agents and applications that demonstrate the public DotNetAgents platform without exposing DNA private factory, premium, test environment, or hosted-service implementation.

This repository is a fresh-history snapshot from the LAN Forgejo source of truth. It intentionally contains public-safe examples only. Private workspace history and premium/private platform code are not part of this repository.

## Included examples

- `hello-agent-cs` - smallest runnable console shape for a DotNetAgents app.
- `document-extraction` - public document extraction worker demo.
- `public-entrepreneur-examples` - starter entrepreneur agents.
- `business-operations` - basic CRM, calendar, communication, and project-management patterns.
- `education` - tutor/classroom-oriented public examples.
- `writing-publishing` - public-safe writing assistant patterns.
- `mcp-thin-host-template` - public MCP thin-host starter.
- `road-access-dotnet-consumer` - public consumer integration sample.

Deferred for a later public release: competitive multi-agent demos, control-loop
reference packs, and non-.NET language ports. They need separate public writing,
review, and validation before they belong in this repository.

The examples use public package references and avoid platform service
implementations, orchestration service internals, memory service internals,
credential provider custody internals, evaluation/test environment
orchestration, private LAN hosts, proprietary prompts, and commercial media
workflow code.
