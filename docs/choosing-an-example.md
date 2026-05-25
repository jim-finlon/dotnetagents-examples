# Choosing An Example

Choose the smallest example that teaches the thing you need.

| Example | Start Here When |
| --- | --- |
| `hello-agent-cs` | you want the smallest runnable shape |
| `mcp-thin-host-template` | you need to expose tools over MCP |
| `document-extraction` | you are processing files into structured output |
| `business-operations` | you are building CRM, calendar, or project workflows |
| `public-entrepreneur-examples` | you want small-business automation patterns |
| `education` | you are building tutor or classroom workflows |
| `writing-publishing` | you are building writing, editorial, or publishing assistants |
| `road-access-dotnet-consumer` | you need a private-network consumer pattern |

## Recommended Path

1. Run `hello-agent-cs`.
2. Read its output shape and result envelope.
3. Run `mcp-thin-host-template` if your product needs tool clients.
4. Pick one domain example.
5. Replace the sample tool logic with your own application service.
6. Add tests before adding live provider credentials.

## What Not To Start With

Do not start by wiring every provider, plugin, and workflow. Start with one
useful task, one or two tools, and deterministic output. Add memory, retrieval,
messaging, and UI once the first loop is understandable.
