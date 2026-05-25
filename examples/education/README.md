# Education Example Pack

This sample pack demonstrates concept explanations, study timeline planning, and quiz coaching utilizing the open-core C# `DotNetAgents` abstractions. It highlights how organizations can build automated educational assistants, onboarding guides, and training simulators.

---

## Capabilities

1. **Educational Tutor (`educational-tutor`)**: Explains complex concepts (e.g. async/await) in a beginner-friendly manner using analogies and code snippets.
2. **Study Planner (`study-planner`)**: Sequences study goals into structured daily schedules covering key milestones and study tracks.
3. **Quiz Coach (`quiz-coach`)**: Designs practice questions and feedback rubrics to assess user comprehension and support topic mastery.

---

## Configuration & Setup

By default, the examples run in offline mock mode. To run live LLM tutoring loops, configure one of the following environment variables:

```bash
# OpenAI
export OPENAI_API_KEY="your-openai-api-key"
export OPENAI_MODEL="gpt-4o-mini" # Optional, defaults to gpt-3.5-turbo

# Anthropic
export ANTHROPIC_API_KEY="your-anthropic-api-key"
export ANTHROPIC_MODEL="claude-3-5-sonnet-20240620" # Optional, defaults to claude-3-sonnet-20240229

# Local Ollama
export OLLAMA_MODEL="gemma2"
export OLLAMA_HOST="http://localhost:11434" # Optional, defaults to localhost
```

---

## Commands & Usage

### Run Deterministic Offline Smoke Check
Validate that the entire education pack builds and executes successfully:
```bash
dotnet run --project samples/education -- --smoke
```

### List Examples
Display all defined education examples and their expected checks and outputs:
```bash
dotnet run --project samples/education -- list
```

### Run an Example (Live Mode)
Execute a specific example. If LLM keys are configured, it runs live model prompts; otherwise, it returns the default offline mock envelope:
```bash
dotnet run --project samples/education -- run educational-tutor
```

Other available examples to run:
```bash
dotnet run --project samples/education -- run study-planner
dotnet run --project samples/education -- run quiz-coach
```

---

## Premium Platform Upgrade Path

While this sample pack utilizes the public open-core libraries to demonstrate basic text-generation educational workflows, upgrading to the enterprise platform on Tyr unlocks advanced production capabilities:

- **Persistent Learner Profiles**: Integration with centralized databases to track user progress, identify knowledge gaps, and dynamically adjust difficulty based on performance history.
- **Instructor Dashboard & Analytics**: Blazor-based admin console to monitor learner engagement, review cohort analytics, and override automated grading verdicts.
- **Hosted Cognitive Classrooms**: Multi-agent setups where specialized AI tutors (e.g., code reviewer agent, security specialist agent) roleplay realistic business scenarios to teach hands-on skills.
- **Certification & Compliance Auditing**: Cryptographically verifiable evaluation records to satisfy regulatory training requirements.
