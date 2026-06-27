# Writing and Publishing Example Pack

This sample pack demonstrates content drafting, copy repurposing, calendar planning, and editorial assistance utilizing open-core C# `DotNetAgents` abstractions. It highlights how businesses can automate marketing workflows, client communications, and editorial QA tasks.

---

## Capabilities

1. **Proposal Writer (`proposal-writer`)**: Generates structured business proposals, including scope, introduction, and pricing tables from a brief description.
2. **Content Repurposer (`content-repurposer`)**: Converts a technical topic or blog post summary into an engaging LinkedIn update and a concise email newsletter.
3. **Publishing Planner (`publishing-planner`)**: Formulates a structured 4-week publishing calendar targeting specific channels, grouped by week and annotated with topic priorities.
4. **Writing Assistant (`writing-assistant`)**: Reviews drafts to provide tone assessments, clarity improvements, and suggested rewrites.

---

## Configuration & Setup

By default, the examples run in offline mock mode. To run live LLM generations, configure one of the following environment variables:

```bash
# OpenAI
export OPENAI_API_KEY="your-openai-api-key"
export OPENAI_MODEL="gpt-4o" # Optional, defaults to gpt-3.5-turbo

# Anthropic
export ANTHROPIC_API_KEY="your-anthropic-api-key"
export ANTHROPIC_MODEL="claude-3-5-sonnet-20240620" # Optional, defaults to claude-3-sonnet-20240229

# Local Ollama
export OLLAMA_MODEL="llama3"
export OLLAMA_HOST="http://localhost:11434" # Optional, defaults to localhost
```

---

## Commands & Usage

### Run Deterministic Offline Smoke Check
Validate that the entire catalog builds and executes successfully:
```bash
dotnet run --project samples/writing-publishing -- --smoke
```

### List Examples
Display all defined writing examples and their expected checks and outputs:
```bash
dotnet run --project samples/writing-publishing -- list
```

### Run an Example (Live Mode)
Execute a specific example. If LLM keys are configured, it runs live model prompts; otherwise, it returns the default offline mock envelope:
```bash
dotnet run --project samples/writing-publishing -- run proposal-writer
```

Other available examples to run:
```bash
dotnet run --project samples/writing-publishing -- run content-repurposer
dotnet run --project samples/writing-publishing -- run publishing-planner
dotnet run --project samples/writing-publishing -- run writing-assistant
```

---

## Premium Platform Upgrade Path

While this sample utilizes the public open-core libraries to demonstrate basic text generation workflows, upgrading to the enterprise platform unlocks advanced production features:

- **Brand-Voice Fine-Tuning**: Integration with governed pattern memory to enforce company-specific terminology, style guidelines, and tone-of-voice profiles automatically.
- **Multimodal Asset Creation**: Support for automated media generation (e.g. cover art, infographics) integrated into the content plan.
- **Autonomous Channel Publishing**: Direct integration with social channel APIs (LinkedIn, Twitter, Mastodon) and CMS tools (WordPress, Ghost) with built-in human-in-the-loop review approvals.
- **Scheduled Continuity**: Cron-based triggers and state-persistence to allow background agents to coordinate and publish content without user interaction.
