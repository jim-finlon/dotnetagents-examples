# Foundation Examples

This pack shows the smallest useful public DotNetAgents patterns before moving
into domain examples. It is intentionally offline-first: the smoke command runs
without credentials, network calls, private services, hosted state, or
production data.

## What It Demonstrates

| Example | Command | Public API exercised |
| --- | --- | --- |
| Tool calling | `tools` | `ITool`, `ToolResult`, `ToolRegistry` |
| Structured output | `structured-output` | `JsonOutputParser<T>` |
| Streaming progress | `streaming` | `ILLMModel<TInput,TOutput>.GenerateStreamAsync` |
| Model routing | `routing` | env/provider route planning before live calls |
| Retry and error envelopes | `retry` | `RetryPolicy`, `RetryPolicyOptions` |
| Usage reporting | `usage` | result-envelope metrics and local counters |

## Smoke Mode

Run all foundation examples:

```bash
dotnet run --project examples/foundation -- --smoke
```

Expected result:

- JSON with `status` set to `passed`;
- `exampleCount` equal to `6`;
- one `resultEnvelope` per foundation capability;
- no provider key, network call, or private service dependency.

The checked-in transcript is
[`transcripts/smoke-output.json`](transcripts/smoke-output.json).

## Focused Commands

Run a single capability:

```bash
dotnet run --project examples/foundation -- tools
dotnet run --project examples/foundation -- structured-output
dotnet run --project examples/foundation -- streaming
dotnet run --project examples/foundation -- routing
dotnet run --project examples/foundation -- retry
dotnet run --project examples/foundation -- usage
```

These commands all emit deterministic JSON so they can be copied into docs,
tests, or CI logs.

## Live Mode

The pack does not require live mode. The model-routing example documents the
same environment-variable posture used by richer examples:

```bash
export OPENAI_API_KEY="<your key>"
export OPENAI_MODEL="gpt-4o-mini"

export ANTHROPIC_API_KEY="<your key>"
export ANTHROPIC_MODEL="claude-3-5-haiku-20241022"

export OLLAMA_MODEL="llama3"
export OLLAMA_HOST="http://localhost:11434"
```

Future examples can pass the selected route into `PublicLlmProvider` when they
need real generation. Keep live mode optional and bounded.

## Public Boundary

This pack uses only public open-core APIs and synthetic inputs. It does not
describe proprietary evaluation mechanics, premium evaluator internals, private
worker pools, credential custody, or production control-plane operations.

Premium packages use these same public building blocks as the low-level
execution vocabulary before adding managed routing, evaluation, and software
factory automation.
