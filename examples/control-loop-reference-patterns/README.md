# Control Loop Reference Patterns

This sample catalog shows how DNA services should compose chains, workflows,
state machines, behavior trees, governance, and observability for common
agentic service classes.

The catalog is intentionally template-oriented. It gives future services a
starting shape without forcing them to copy an arbitrary production service.
Each sample separates these layers:

- Chain logic: bounded model/tool steps that transform one input into one output.
- Workflow: durable multi-step orchestration, retries, compensation, and gates.
- Lifecycle state: externally visible state transitions and resumability.
- Reactive policy: stimulus classification and behavior-tree decisions.
- Governance: actor permissions, preview/confirm controls, quotas, and risk.
- Observability: events, traces, metrics, evidence refs, and SDLC correlation.

## Samples

| Sample | Service Class | Pilot Baseline |
|---|---|---|
| `durable-workflow-service.yaml` | Durable workflow service for long-running SDLC or release work | SdlcAgent autonomous loop and release workflows |
| `reactive-policy-service.yaml` | Reactive policy service for stimulus-driven defensive or monitoring loops | SecurityScanningAgent behavior worker |
| `guarded-control-plane-service.yaml` | Guarded control-plane service for high-impact infrastructure/control work | InfrastructureControl mutation and drift flows |
| `evolutionary-service.yaml` | Evolutionary service for experiment, mutation, and promotion loops | PromptSpecialist and Learning Lab evolution flows |

## Adoption Rule

New control-loop services should start by selecting the closest sample, then
replace the placeholder domain terms with service-specific names. Existing pilot
stories can cite these samples as implementation baselines while they adopt the
shared DotNetAgents control-loop seams.

## Non-Goals

- These samples do not replace the real DotNetAgents runtime contracts.
- They do not grant production mutation authority.
- They do not contain secrets, credentials, provider tokens, or production data.
- They do not require the `DotNetAgents` submodule to be edited in this workspace
  story.
