# Example Contract

Every runnable DotNetAgents example has two modes:

- **Smoke mode** proves the example can run locally without secrets, network access, private hosted services, or production data.
- **Live mode** is optional and uses environment variables for provider configuration.

The smoke path is the publish gate. Live mode is a demonstration path.

## Smoke Mode

A smoke command should:

- exit with code `0` when the local example passes;
- print structured JSON or a stable text transcript;
- require no credentials;
- avoid all network calls;
- avoid private endpoints and production data;
- include a result envelope when the example produces agent output;
- stay fast enough to run in CI.

Example:

```bash
dotnet run --project examples/hello-agent-cs -- --smoke
```

## Live Mode

Live mode should:

- be optional;
- use environment variables only;
- document provider/model variables;
- fall back cleanly when no provider is configured, when that is appropriate;
- keep prompts and inputs synthetic or public-safe;
- document cost and timeout expectations when relevant.

Example:

```bash
export OPENAI_API_KEY="<your key>"
dotnet run --project examples/business-operations -- run project-planner
```

Do not commit keys. Do not paste keys into `appsettings.json`. Do not make live mode the only way to understand an example.

## README Requirements

Each runnable example README should include:

- what the example demonstrates;
- packages and plugin families exercised;
- smoke command;
- live command, or a statement that live mode is not supported;
- expected output;
- public/private boundary note;
- first extension point;
- validation notes.

## Catalog Requirements

When adding or materially changing an example, update [`../examples/catalog.v1.json`](../examples/catalog.v1.json). The catalog is the stable machine-readable index for future docs, validation scripts, and generated tables.

## Validation Evidence

An implementation story should close with:

- build command and result;
- smoke command and result;
- changed catalog entry ids;
- output fixture or transcript path when applicable;
- public content scan result;
- follow-up story ids for core/plugin friction.

Run [Example Quality Gates](example-quality-gates.md) before closeout:

```bash
scripts/verify-example-quality.sh --scan-path <changed-example-or-doc-path>
```
