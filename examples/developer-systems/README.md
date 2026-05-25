# Developer Systems Pack Examples

This package contains public example implementations showcasing agentic developer-productivity patterns. These examples operate on diffs, commits, markdown files, and C# signatures in a safe, fully-offline mock environment.

## Examples Included

1. **Code Review Assistant (`code-review`)**: Triages git diff files and suggests stylistic/correctness comments.
2. **Release-Note Generator (`release-notes`)**: Groups list of commits by conventional commit types and generates release highlights.
3. **Docs Maintainer (`docs-maintainer`)**: Audits markdown documents for Yaml frontmatter completeness and link hygiene.
4. **Test Authoring Assistant (`test-authoring`)**: Automatically designs unit test structures from C# interface signatures.

## Running the Examples

### List Examples
```bash
dotnet run --project examples/developer-systems/DeveloperSystemsExamples.csproj -- list
```

### Run Smoke Tests
```bash
dotnet run --project examples/developer-systems/DeveloperSystemsExamples.csproj -- --smoke
```

### Run Specific Examples
```bash
dotnet run --project examples/developer-systems/DeveloperSystemsExamples.csproj -- run code-review
dotnet run --project examples/developer-systems/DeveloperSystemsExamples.csproj -- run release-notes
dotnet run --project examples/developer-systems/DeveloperSystemsExamples.csproj -- run docs-maintainer
dotnet run --project examples/developer-systems/DeveloperSystemsExamples.csproj -- run test-authoring
```
