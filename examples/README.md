# DotNetAgents Examples Portfolio

Welcome to the public examples portfolio for the **DotNetAgents 10** platform. This repository contains fully functional, business-focused agentic applications designed to showcase how to build, test, and run autonomous agents using the open-core C# framework.

## Architecture & Open-Core Boundary

These examples consume only the public, open-core libraries:
- `DotNetAgents.Core` (Shared primitives, models, and example result schema)
- `DotNetAgents.AgentFramework` (Agent capabilities and execution runtime)

They are designed to operate completely independently of the premium control-plane and enterprise laboratory services (such as private worker pools, automated evaluation gauntlets, and advanced routing engines).

---

## Getting Started

### Prerequisites

- **.NET 10 SDK** or later
- (Optional) Local **Ollama** server running, or API keys for OpenAI/Anthropic.

### Execution Modes

Every example project in this portfolio supports two distinct modes:

#### 1. Offline Smoke Mode (`--smoke`)
Runs immediately without any external network connections, API keys, or model server dependencies. It executes local, deterministic checks to verify that the environment, build configuration, and envelope schemas are correctly set up.
```bash
dotnet run --project samples/hello-agent-cs -- --smoke
```

#### 2. Live Execution Mode
Uses active LLM providers to perform real work. Configure your preferred model endpoint by setting standard environment variables before running the application:

```bash
# To run via OpenAI:
export OPENAI_API_KEY="your-openai-api-key"
export OPENAI_MODEL="gpt-4o" # Defaults to gpt-3.5-turbo

# To run via Anthropic:
export ANTHROPIC_API_KEY="your-anthropic-api-key"
export ANTHROPIC_MODEL="claude-3-5-sonnet-20241022" # Defaults to claude-3-sonnet-20240229

# To run via local Ollama:
export OLLAMA_MODEL="llama3"
export OLLAMA_HOST="http://localhost:11434" # Defaults to http://localhost:11434
```

Then, run the program specifying the command:
```bash
dotnet run --project samples/hello-agent-cs -- hello "Alice"
```

---

## Examples Catalog

The repository is structured as a collection of specialized business domains:

The expanded Example Systems Showcase roadmap is tracked in
[`../docs/roadmap/example-systems-showcase.md`](../docs/roadmap/example-systems-showcase.md).
Each future example family must include an offline smoke mode, optional live
mode, expected output evidence, and public/private boundary notes.

The machine-readable catalog for current and planned examples is
[`catalog.v1.json`](catalog.v1.json). Future example stories should update this
catalog when they add new folders or change smoke/live commands.

### 1. [Hello Agent C#](hello-agent-cs/)
A starter project demonstrating the basic lifecycle of an agent card, tool definition, and simple interactive chat completions in both offline and live modes.

### 2. [Foundation Examples](foundation/)
Runnable public basics for tool calling, structured output, streaming progress,
model routing, retry/error envelopes, and usage reporting.

### 3. [Business Operations](business-operations/) *(Coming Soon)*
Automates common workspace tasks:
- **Project Planner**: Converts high-level goals into step-by-step markdown milestones and JSON tasks.
- **CRM Follow-Up**: Drafts personalized outreach messages for sales leads.
- **Communications Triage**: Classifies emails and documents by urgency and category.
- **Appointment Assistant**: Suggests calendar schedules from free-form text.

### 4. [Document Extraction](document-extraction/) *(Coming Soon)*
Demonstrates processing local text files and PDFs, chunking, and implementing local retrieval-augmented generation (RAG).

### 5. [Writing & Publishing](writing-publishing/) *(Coming Soon)*
Agentic assistants for drafting proposals, creating content repurposing schedules, and generating marketing material.

### 6. [Education & Onboarding](education/) *(Coming Soon)*
An interactive educational coach that walks users through onboarding materials and dynamically generates quizzes to test comprehension.

---

## Verification and Safety Gates

To run the automated leakage audits and validation tests:
```bash
dotnet test DotNetAgents/tests/DotNetAgents.Core.Tests --filter PublicExampleResultEnvelopeTests
```

This gate ensures that none of the public-facing examples leak premium endpoints, internal platform dependencies, or private API keys.
