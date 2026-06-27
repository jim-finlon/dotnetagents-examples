from __future__ import annotations

import argparse
import hashlib
import json
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any


@dataclass(frozen=True)
class HelloAgentCard:
    agent_id: str
    display_name: str
    purpose: str
    a2a_registration_route: str
    mcp_tools: list[str]
    learning_event_shape: str


@dataclass(frozen=True)
class HelloResponse:
    message: str
    mcp_tool_name: str
    a2a_intent: str
    next_step: str


@dataclass(frozen=True)
class LearningEvent:
    problem_key: str
    service: str
    step: str
    outcome: str
    summary: str


AGENT_CARD = HelloAgentCard(
    agent_id="hello-agent-py",
    display_name="Hello DNA Agent Python",
    purpose=(
        "Offline engineering-distribution sample that maps one tiny tool to "
        "A2A, MCP, and learning-event concepts."
    ),
    a2a_registration_route="/.well-known/agent.json",
    mcp_tools=["hello", "card"],
    learning_event_shape="learning.event.v1",
)


def handle_hello(name: str) -> HelloResponse:
    return HelloResponse(
        message=(
            f"Hello, {name}. This sample is intentionally offline: no private "
            "control plane, credentials, or live services required."
        ),
        mcp_tool_name="hello",
        a2a_intent="agent.sample.hello",
        next_step=(
            "Open README.md, change the greeting in "
            "examples/hello_agent_py/run.py, then rerun --smoke."
        ),
    )


def record_learning_event(step: str, outcome: str) -> LearningEvent:
    return LearningEvent(
        problem_key="sample:hello-agent-py:smoke",
        service=AGENT_CARD.agent_id,
        step=step,
        outcome=outcome,
        summary=(
            "The Hello-agent Python sample smoke command validated the local "
            "A2A/MCP/learning-event shape without external dependencies."
        ),
    )


def create_result_envelope(passed: bool) -> dict[str, Any]:
    input_summary = "hello-agent-py --smoke David Carter"
    return {
        "schemaVersion": "dna.public-example.result.v1",
        "exampleId": AGENT_CARD.agent_id,
        "exampleVersion": "1.0.0",
        "runId": "hello-agent-py-smoke",
        "timestampUtc": datetime(2026, 5, 18, 14, 20, tzinfo=timezone.utc)
        .isoformat()
        .replace("+00:00", "Z"),
        "inputSummaryHash": "sha256:"
        + hashlib.sha256(input_summary.encode("utf-8")).hexdigest(),
        "outputArtifactRefs": [
            {
                "kind": "stdout",
                "ref": "console",
                "mediaType": "application/json",
            }
        ],
        "localValidation": {
            "status": "passed" if passed else "failed",
            "checks": [
                "agent card route present",
                "mcp tool list includes hello",
                "hello response contains requested name",
                "learning event includes stable problem key",
            ],
        },
        "selfReportedMetrics": {
            "checksPassed": 4 if passed else 0,
        },
    }


def run_smoke() -> int:
    card = AGENT_CARD
    hello = handle_hello("David Carter")
    learning = record_learning_event("hello-agent-py.smoke", "success")

    passed = (
        card.agent_id == "hello-agent-py"
        and card.a2a_registration_route == "/.well-known/agent.json"
        and "hello" in card.mcp_tools
        and "David Carter" in hello.message
        and learning.problem_key == "sample:hello-agent-py:smoke"
    )

    write_json(
        {
            "status": "passed" if passed else "failed",
            "agentId": card.agent_id,
            "a2ARegistrationRoute": card.a2a_registration_route,
            "mcpTools": card.mcp_tools,
            "message": hello.message,
            "problemKey": learning.problem_key,
            "outcome": learning.outcome,
            "resultEnvelope": create_result_envelope(passed),
        }
    )
    return 0 if passed else 1


def write_json(value: Any) -> None:
    print(json.dumps(value, indent=2, sort_keys=False))


def agent_card_json() -> dict[str, Any]:
    return {
        "agentId": AGENT_CARD.agent_id,
        "displayName": AGENT_CARD.display_name,
        "purpose": AGENT_CARD.purpose,
        "a2ARegistrationRoute": AGENT_CARD.a2a_registration_route,
        "mcpTools": AGENT_CARD.mcp_tools,
        "learningEventShape": AGENT_CARD.learning_event_shape,
    }


def hello_response_json(response: HelloResponse) -> dict[str, Any]:
    return {
        "message": response.message,
        "mcpToolName": response.mcp_tool_name,
        "a2AIntent": response.a2a_intent,
        "nextStep": response.next_step,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Hello DNA Agent Python sample")
    parser.add_argument("--smoke", action="store_true", help="Run the offline smoke check")
    parser.add_argument("command", nargs="?", default="--smoke")
    parser.add_argument("name", nargs="?", default="DNA developer")
    args = parser.parse_args(argv)

    if args.smoke or args.command == "--smoke":
        return run_smoke()
    if args.command == "card":
        write_json(agent_card_json())
        return 0
    if args.command == "hello":
        write_json(hello_response_json(handle_hello(args.name)))
        return 0

    print(f"Unknown command '{args.command}'.", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
