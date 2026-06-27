#!/usr/bin/env bash
# demo-mode.sh — boot the exact demo state for the 12-minute live-demo
# walkthrough described in examples/sales-arena/README.md.
#
# Goals:
#   1. One-command boot from a clean clone.
#   2. Deterministic seeding so the script's edge-case beats (first
#      meeting, glengarry drip, first close, training-loop prompt
#      promotion) fire on the timing the demo script promises.
#   3. Latency-safe — falls back to cached LLM responses if the live
#      endpoint is slow, so a 12-minute take doesn't get derailed by
#      a moody model.
#   4. Rehearsable — `--rehearse` walks the cached events step-by-step
#      without actually starting the contest, so presenters can verify
#      timing before the camera rolls.
#
# This helper is a scaffold today: it expects `dna-arena` (SA-04) and
# a built Arena (SA-01..05) to be on PATH. When those land, the
# helper boots from a clean state in &lt; 90 seconds and prints the
# DEMO MODE READY banner expected by the demo script's Beat 2.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
ARENA_DIR="$ROOT/examples/sales-arena"
CACHE_DIR="$ARENA_DIR/cli/scripts/.demo-cache"
LEAD_PACK="$ARENA_DIR/lead-packs/synthetic-200.json"
CONTEST_NAME="demo-2026"
UI_PORT="${SALES_ARENA_UI_PORT:-5005}"
BELL_PORT="${SALES_ARENA_BELL_PORT:-5006}"

mode="live"
rehearse=0
use_cache=0
show_help=0

usage() {
  cat <<'USAGE'
demo-mode.sh — boot the Sales Arena demo state for the 12-minute live take.

Usage:
  bash examples/sales-arena/cli/scripts/demo-mode.sh [options]

Options:
  --use-cache    Force the contest to use cached LLM responses (recovery
                 mode when the live endpoint is slow).
  --rehearse     Walk the cached event timeline step-by-step without
                 starting a real contest — useful for pre-take rehearsal.
  --reset        Tear down any previous demo state and start clean.
  -h, --help     Show this help.

Environment:
  SALES_ARENA_UI_PORT     Manager UI port (default 5005).
  SALES_ARENA_BELL_PORT   CLI bell-stream port (default 5006).
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --use-cache) use_cache=1; shift ;;
    --rehearse) rehearse=1; shift ;;
    --reset) reset=1; shift ;;
    -h|--help) show_help=1; shift ;;
    *) echo "demo-mode: unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ "$show_help" -eq 1 ]]; then
  usage
  exit 0
fi

banner() {
  printf '\n========================================================\n'
  printf '  %s\n' "$1"
  printf '========================================================\n\n'
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "demo-mode: required command not found on PATH: $1" >&2
    echo "demo-mode: build the Arena first (dotnet build examples/sales-arena/) and ensure dna-arena is on PATH." >&2
    exit 2
  fi
}

if [[ "$rehearse" -eq 1 ]]; then
  banner "DEMO MODE — REHEARSAL"
  if [[ ! -d "$CACHE_DIR" ]]; then
    echo "demo-mode: cache directory not found at $CACHE_DIR" >&2
    echo "demo-mode: run a successful live demo at least once to populate the cache before rehearsing." >&2
    exit 1
  fi
  echo "Walking cached event timeline from $CACHE_DIR..."
  echo "(Each event prints; press Enter to advance, Ctrl-C to stop.)"
  for event in "$CACHE_DIR"/*.event.json; do
    [[ -f "$event" ]] || continue
    printf '\n--- event: %s ---\n' "$(basename "$event")"
    if command -v jq >/dev/null 2>&1; then
      jq -r '"\(.timestamp_ms // "?") ms  \(.kind // "?")  \(.summary // "")"' "$event"
    else
      head -c 200 "$event"; echo
    fi
    read -r -p "next? " _ || true
  done
  banner "REHEARSAL DONE"
  exit 0
fi

require_cmd dna-arena
require_cmd dotnet

banner "DEMO MODE — BOOT"
echo "Repo root:      $ROOT"
echo "Arena dir:      $ARENA_DIR"
echo "Lead pack:      $LEAD_PACK"
echo "Contest name:   $CONTEST_NAME"
echo "Manager UI:     http://localhost:$UI_PORT/floor"
echo "Bell stream:    http://localhost:$BELL_PORT"
echo "Mode:           $([[ "$use_cache" -eq 1 ]] && echo "cached LLM responses" || echo "live LLM endpoint")"
echo

if [[ "${reset:-0}" -eq 1 ]]; then
  echo "Resetting previous demo state..."
  dna-arena reset --confirm
fi

echo "Building Arena (incremental)..."
dotnet build "$ARENA_DIR/" --nologo --verbosity minimal

echo
echo "Initializing contest workspace from synthetic 200-lead pack..."
dna-arena init \
  --leads "$LEAD_PACK" \
  --ui-port "$UI_PORT" \
  --bell-port "$BELL_PORT"

if [[ "$use_cache" -eq 1 ]]; then
  echo
  echo "Forcing cached LLM responses for stable 12-minute timing..."
  dna-arena config llm --provider cache --cache-dir "$CACHE_DIR"
fi

echo
echo "Seeding the deterministic demo timeline (first meeting at 5:30, glengarry drip at 6:30,"
echo "first close at 8:00, training-loop promotion candidates at 10:00, approval at 10:30)..."
dna-arena seed-demo --contest "$CONTEST_NAME" --timeline demo-2026-v1

banner "DEMO MODE READY"
echo "Open:           http://localhost:$UI_PORT/floor"
echo "Start contest:  dna-arena contest start \\"
echo "                    --name \"$CONTEST_NAME\" \\"
echo "                    --personas roma,levene,moss \\"
echo "                    --hours 1 \\"
echo "                    --time-compression 60"
echo
echo "After the bell: dna-arena replay summary --contest $CONTEST_NAME"
echo
echo "If anything goes wrong on the take, see the Recovery Beats table"
echo "in examples/sales-arena/README.md."
