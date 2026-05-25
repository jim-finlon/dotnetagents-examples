#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CATALOG="$ROOT/examples/catalog.v1.json"
SKIP_BUILD=0
SKIP_SMOKE=0
SCAN_SCOPE="catalog"
SCAN_PATHS=()

usage() {
  cat <<'USAGE'
Usage: scripts/verify-example-quality.sh [options]

Validates the public DotNetAgents examples suite:
  - catalog JSON parses and satisfies core invariants
  - top-level C# example projects build
  - runnable catalog smoke commands exit 0
  - selected paths pass a conservative public-content scan

Options:
  --skip-build          Skip dotnet build matrix.
  --skip-smoke          Skip catalog smoke commands.
  --scan-scope SCOPE    catalog | docs | all | explicit. Default: catalog.
  --scan-path PATH      Add a path to scan. Implies --scan-scope explicit.
  -h, --help            Show this help.

Notes:
  Use --scan-path for newly added examples while the legacy examples tree still
  contains known public-content audit findings. The full --scan-scope all mode is
  intentionally strict and may fail until those legacy findings are cleaned.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-build)
      SKIP_BUILD=1
      shift
      ;;
    --skip-smoke)
      SKIP_SMOKE=1
      shift
      ;;
    --scan-scope)
      SCAN_SCOPE="${2:?missing value for --scan-scope}"
      shift 2
      ;;
    --scan-path)
      SCAN_SCOPE="explicit"
      SCAN_PATHS+=("${2:?missing value for --scan-path}")
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "ERROR: unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

case "$SCAN_SCOPE" in
  catalog|docs|all|explicit) ;;
  *)
    echo "ERROR: --scan-scope must be catalog, docs, all, or explicit" >&2
    exit 2
    ;;
esac

need_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "ERROR: required tool '$1' is not on PATH" >&2
    exit 2
  fi
}

need_tool jq
need_tool rg

if [[ ! -f "$CATALOG" ]]; then
  echo "ERROR: missing catalog: $CATALOG" >&2
  exit 2
fi

echo "== catalog =="
jq empty "$CATALOG"
jq -e '
  .schemaVersion == "dotnetagents.public-examples.catalog.v1"
  and (.examples | type == "array")
  and (.examples | length > 0)
  and (([.examples[].id] | length) == ([.examples[].id] | unique | length))
  and ([.examples[] | select(.maturity == "runnable" and (.smokeCommand == null or .smokeCommand == ""))] | length == 0)
  and ([.examples[] | select((.boundaryNote // "") == "")] | length == 0)
' "$CATALOG" >/dev/null
echo "catalog ok: $(jq '.examples | length' "$CATALOG") entries"

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  need_tool dotnet
  echo "== build matrix =="
  while IFS= read -r -d '' csproj; do
    rel="${csproj#$ROOT/}"
    echo "build $rel"
    dotnet build "$csproj" --nologo >/tmp/dotnetagents-example-build.log
    tail -n 3 /tmp/dotnetagents-example-build.log
  done < <(find "$ROOT/examples" -maxdepth 2 -name '*.csproj' -print0 | sort -z)
fi

if [[ "$SKIP_SMOKE" -eq 0 ]]; then
  echo "== smoke commands =="
  while IFS=$'\t' read -r id command; do
    [[ -n "$id" ]] || continue
    echo "smoke $id"
    (
      cd "$ROOT"
      bash -lc "$command"
    ) >/tmp/dotnetagents-example-smoke-"$id".out
    test -s /tmp/dotnetagents-example-smoke-"$id".out
    head -c 160 /tmp/dotnetagents-example-smoke-"$id".out
    echo
  done < <(jq -r '.examples[] | select(.smokeCommand != null) | [.id, .smokeCommand] | @tsv' "$CATALOG")
fi

scan_targets=()
case "$SCAN_SCOPE" in
  catalog)
    scan_targets+=("$CATALOG" "$ROOT/docs/example-catalog.md" "$ROOT/docs/example-contract.md")
    ;;
  docs)
    scan_targets+=("$ROOT/README.md" "$ROOT/docs" "$ROOT/examples/README.md")
    ;;
  all)
    scan_targets+=("$ROOT")
    ;;
  explicit)
    if [[ "${#SCAN_PATHS[@]}" -eq 0 ]]; then
      echo "ERROR: --scan-scope explicit requires --scan-path" >&2
      exit 2
    fi
    for path in "${SCAN_PATHS[@]}"; do
      scan_targets+=("$ROOT/$path")
    done
    ;;
esac

echo "== public content scan ($SCAN_SCOPE) =="
for target in "${scan_targets[@]}"; do
  if [[ ! -e "$target" ]]; then
    echo "ERROR: scan target not found: $target" >&2
    exit 2
  fi
done

scanner_path="$(realpath "$ROOT/scripts/verify-example-quality.sh")"
filtered_scan_targets=()
for target in "${scan_targets[@]}"; do
  if [[ "$(realpath "$target")" == "$scanner_path" ]]; then
    continue
  fi
  filtered_scan_targets+=("$target")
done

if [[ "${#filtered_scan_targets[@]}" -eq 0 ]]; then
  echo "public content scan ok (scanner implementation excluded)"
elif rg -n -i \
  --glob '!scripts/verify-example-quality.sh' \
  -e 'good\s*rx|goodrx' \
  -e 'mission_control|SdlcAgent|review cadre|AEQ|autonomous lane|closeout policy|process incident bundle|cadre verdict' \
  -e 'LearningLab|aeq_run_benchmark|experiment_evolution|variant promotion|cohort comparison|genetic algorithm|experiment lab' \
  -e 'forge\.dna\.lan|tyr:5070|mimir|loki|helios|:5001|:5070|:5075|:5106|192\.168\.' \
  -e 'claim_story_for_execution|select_next_story|record_story_closeout' \
  -e 'CREDENTIALS_ADMIN_API_KEY|SESSION_PERSISTENCE_API_KEY' \
  -e '-----BEGIN (RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----|sk-[A-Za-z0-9_-]{20,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}' \
  "${filtered_scan_targets[@]}"; then
  echo "ERROR: public content scan found private/internal terms in selected targets" >&2
  exit 1
else
  echo "public content scan ok"
fi

echo "== result =="
echo "example quality gates passed"
