# Example Quality Gates

Every public example should be easy to prove locally. The quality gate is:

1. Catalog entry exists and is valid.
2. C# projects build.
3. Runnable examples have a smoke command.
4. Smoke commands exit successfully without credentials or network access.
5. New or changed public files pass a public-content scan.
6. The story closeout records transcript or output evidence.

Run the local gate from the repository root:

```bash
scripts/verify-example-quality.sh
```

The default scan scope is `catalog`, because the current examples tree still has
legacy public-content findings that are being cleaned up in separate stories.
When adding a new example, scan the new path explicitly:

```bash
scripts/verify-example-quality.sh --scan-path examples/my-new-example
```

Use the full strict scan only when intentionally auditing the whole public tree:

```bash
scripts/verify-example-quality.sh --scan-scope all
```

## Evidence Convention

Each implementation story should record:

- the catalog entry id(s) changed;
- `dotnet build` result for affected projects;
- smoke command(s) and pass/fail result;
- output transcript or fixture path;
- public-content scan command and result;
- any follow-up story ids for core/plugin friction.

For smoke output, prefer deterministic JSON with `status`, `exampleId` or
`packId`, and a `resultEnvelope` where the example produces agent output.

## Known Limitation

The repository-wide public-content scan is intentionally stricter than the
current legacy examples tree. Until those older findings are retired, new work
should scan changed paths with `--scan-path` and record the full-tree residual
in closeout when relevant.
