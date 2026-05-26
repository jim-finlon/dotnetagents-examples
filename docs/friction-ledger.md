# Core and Plugin Friction Ledger

Examples are not just demonstrations; they are controlled pressure tests for DotNetAgents public packages. During the implementation of any example system, developers and agents will inevitably hit friction—awkward APIs, missing dependency injection helpers, heavy serialization ceremony, or configuration complexity. 

This document defines how we capture, classify, and route that friction so it becomes trackable SDLC work instead of hidden workarounds.

---

## Friction Record Template

When you discover friction in an example lane, document it using this template. Include it in the example's `README.md`, the implementation story's closeout notes, or a linked issue:

```text
Example:             [Name of the example system, e.g., hello-agent-cs]
Package/plugin:      [Affected DotNetAgents package or plugin name, e.g., DotNetAgents.Core]
Observed friction:   [Brief description of the awkward ergonomics or missing helper]
Developer impact:    [How it affects readability, boilerplate count, or setup time]
Reproduction:        [Link or code block showing the awkward code pattern]
Suggested fix:       [How the API or helper should ideally behave]
Boundary check:      [Confirming this is public-safe and does not expose premium internals]
Validation needed:   [How to verify the fix works (e.g., unit test, compile check)]
Routed story id:     [SDLC story GUID or "Inline Fix" / "NO_FOLLOWUP_REQUIRED"]
```

---

## Severity Levels

We classify friction using three severity levels:

| Severity | Meaning | Action |
| :--- | :--- | :--- |
| **P1 (Critical)** | Blocks a public example from compiling or running, or fundamentally misleads users. | Fix inline before publish, or block the example story until resolved. |
| **P2 (Major)** | The example runs, but requires awkward boilerplate, heavy nesting, or verbose configurations that distract from the core design. | File a follow-up SDLC story unless a safe, low-blast-radius inline fix is possible. |
| **P3 (Minor)** | Minor naming mismatches, discoverability papercuts, logging noise, or extension ergonomics. | File a follow-up if the issue is observed recurring across multiple examples. |

---

## Routing & Resolution Rules

When friction is recorded, choose one of the following resolution paths:

### 1. Inline Fix
- **When**: The change is small, entirely local to your expected paths, has a low blast radius, and is covered by the example quality check suite.
- **Action**: Fix it in the same branch/worktree. Ensure the fix does not break backward compatibility or other public examples.

### 2. Same-Epic Follow-Up
- **When**: The issue is directly related to the example system itself but is too large or risky to include in the current implementation pass.
- **Action**: Propose/file a child story under the same epic and mark the current story as blocked by or related to it.

### 3. Core Story (`DotNetAgents.Core`)
- **When**: The friction stems from core abstractions, lifecycle events, message structures, or framework-wide serialization issues.
- **Action**: File an SDLC story targeting the core library repository and link it.

### 4. Plugin Story (`DotNetAgents.Plugins`)
- **When**: The friction is localized to a specific integration, such as a vector database, messaging provider, or LLM client adapter.
- **Action**: File an SDLC story targeting the plugins package repository.

### 5. Docs Story
- **When**: The friction is caused by outdated, incomplete, or confusing setup guides, missing protocol specs, or unclear parameters.
- **Action**: File a documentation-specific SDLC story.

### 6. Premium/Private Deferral
- **When**: The resolution requires access to DNA Factory proprietary engines (e.g., live Arena tournament simulation, genetic prompt mutation, internal evaluation scoring).
- **Action**: Defer the fix to the private backlog. Mark the public ledger entry with `NOT_PUBLIC_SAFE: <reason>` and route it to the premium platform queue.

---

## SDLC Story-Linking Guidance

Do not bury friction details or workarounds in plain-text closeout logs. To link follow-up work correctly:

- **Linking Stories**: In your story closeout metadata, populate the deferred follow-ups:
  ```text
  FOLLOWUP_DEFERRED_STORY_IDS: [story-id-1, story-id-2]
  ```
- **Audited Deferrals**: If the friction is decided to be out of scope or is a known limitation that will not be fixed, close the story using one of the audited deferral markers in your closeout notes:
  - `NO_FOLLOWUP_REQUIRED: [Detailed, technical reason why a fix is unnecessary]`
  - `NOT_DOING: [Explanation of why this pattern is avoided by design]`
  - `NON_GOAL: [Explanation of why this capability is out of scope for the public repository]`

---

## Seeded Friction Categories

These categories represent typical friction points to watch for across example families:

### Foundation Examples
- **DI Registration Ceremony**: Verbose boilerplate required to register agents, tools, or fallbacks in `IServiceCollection`.
- **Model Fallback Configuration**: Rigid fallback chains that make it hard to configure local-LLM fallbacks for offline smoke tests.
- **Streaming Event Parsing**: Heavy ceremony required to extract partial chunk text from streaming event envelopes.

### Protocol Examples
- **MCP Client Handshake**: Complex setup needed to establish an MCP client session over stdio or SSE.
- **A2A Authentication**: Heavy signature validation ceremony for exchanging A2A agent cards.

### Orchestration Examples
- **HITL Interruption Handling**: Verbose state machine definitions required to pause execution for human approvals.
- **Critic/Writer Synchronization**: Complex message passing boilerplate when coordinating loops between multiple specialist agents.

### Plugin Examples
- **Provider-Specific Settings**: Configuration properties that leak vendor-specific names into generic plugin interfaces.
- **Vector-Store Stubbing**: Missing local in-memory vector-store providers for key-free offline smoke testing.
- **Computer Use Safety Gates**: Inability to easily preview or confirm high-risk OS/browser actions before execution.

---

## Boundary and Safety Rules

Ledger entries must remain strictly **public-safe**:
- **Do not** log or reference private API keys, system tokens, or LAN credentials.
- **Do not** expose internal hostnames, LAN network structures, or private server endpoints.
- **Do not** detail DNA Factory private scoring heuristics, evaluation datasets, or private code reviewer prompts.
- **Always** frame the public/private boundary cleanly:
  > *Preferred phrasing*: "This example operates offline using local stubs. Premium DNA Factory environments use managed simulator runs to evaluate and certify the same agent pattern."
