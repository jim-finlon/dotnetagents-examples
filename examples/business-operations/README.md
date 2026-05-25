# Business Operations Examples

This example pack demonstrates how agents can automate common, day-to-day office and business workflow tasks using the open-core **DotNetAgents 10** platform.

## Catalog of Examples

1. **`project-planner`**: Analyzes a goal statement to produce milestones, tasks, owners, and risk management points.
2. **`crm-follow-up`**: Evaluates new customer leads and drafts personalized outreach email text.
3. **`communications-triage`**: Categorizes and sorts incoming support inbox messages by urgency and topic.
4. **`appointment-assistant`**: Extracts meeting details from free-form request texts and suggests calendar focus blocks.
5. **`time-management`**: Proposes a daily productivity schedule based on meetings, deadlines, and active tasks.

---

## How to Run

### Offline Smoke Test
Run the pack's overall offline smoke suite to verify that all definitions serialize to correct validation envelopes:
```bash
dotnet run --project samples/business-operations -- --smoke
```

### Listing Available Examples
To see details of each definition in the catalog:
```bash
dotnet run --project samples/business-operations -- list
```

### Running an Example Offline (Fallback)
If no API keys are configured, the example will execute in its offline fallback mode, instantly returning a sample output and result envelope:
```bash
dotnet run --project samples/business-operations -- run project-planner
```

### Running an Example Live with LLM Providers
To run real live completions, configure one of the supported providers (OpenAI, Anthropic, or local Ollama) in your environment:

```bash
# Example: Running live via OpenAI
export OPENAI_API_KEY="your-api-key"
export OPENAI_MODEL="gpt-4o"

dotnet run --project samples/business-operations -- run project-planner
```

---

## Upgrade Path to Premium Platform Services

For business applications requiring complex, production-ready capabilities, DotNetAgents offers seamless upgrade paths:
- **State Persistence**: Utilize the enterprise state-machine and database persistence engines to track multi-turn workflow executions.
- **Collaborative Teams**: Coordinate multi-agent cohorts (e.g., separating planning from execution and validation) via the premium Laboratory orchestrator.
- **Durable Memory & Lessons**: Enable agent networks to persist long-term learnings and retrieve contextual lessons automatically via the HiveMind service.
- **Enterprise Safety & Evals**: Run safety gates and compliance scans using the automated evaluation gauntlets.
