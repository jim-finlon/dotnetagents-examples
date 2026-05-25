# Running Examples

Most examples are designed to run offline first.

The command list in [`../examples/catalog.v1.json`](../examples/catalog.v1.json)
is the authoritative catalog for smoke and live commands. Individual README
files may include extra walkthrough commands, but the catalog is what future
validation scripts should consume.

## Basic Run

```bash
git clone https://github.com/jim-finlon/dotnetagents-examples.git
cd dotnetagents-examples
dotnet run --project examples/foundation -- --smoke
```

Expected behavior:

- process exits successfully
- output is structured JSON
- no live credentials are required
- no private service is contacted

## Live Integrations

Some examples can be extended to call live providers. Live calls should be
explicitly enabled and configured with environment variables.

Example pattern:

```bash
export EXAMPLE_PROVIDER_API_KEY="<your key>"
export DOTNETAGENTS_EXAMPLE_RUN_LIVE=1
dotnet run
```

Do not commit the key. Do not paste it into `appsettings.json`.

## Package Sources

The public package train is preview. If packages are not available from your
configured package source yet, use the repository project references or local
package feed documented by the individual example.

## Running From The Repository Root

Some README files show commands from the repository root. Others assume you are
inside the example folder. If a command fails because a project path cannot be
found, check the example README and run from the path it names.

The foundation pack is the first stop for new users:

```bash
dotnet run --project examples/foundation -- tools
dotnet run --project examples/foundation -- structured-output
dotnet run --project examples/foundation -- streaming
```

## Validation

Before modifying an example:

```bash
dotnet build
dotnet test
```

For examples without a test project, run the documented smoke command and keep
the output deterministic.

## Smoke/Live Contract

Every runnable example should have:

- a smoke command that runs with no credentials, network access, private hosted
  service, or production data;
- optional live mode configured through environment variables;
- structured output or a stable transcript;
- a README safety note;
- a catalog entry.

See [Example Contract](example-contract.md) for the full gate.

For the automated local gate, run:

```bash
scripts/verify-example-quality.sh
```
