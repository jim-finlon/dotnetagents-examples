# Running Examples

Most examples are designed to run offline first.

## Basic Run

```bash
git clone https://github.com/jim-finlon/dotnetagents-examples.git
cd dotnetagents-examples/examples/hello-agent-cs
dotnet run -- --smoke
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

## Validation

Before modifying an example:

```bash
dotnet build
dotnet test
```

For examples without a test project, run the documented smoke command and keep
the output deterministic.
