# Install Quickstart (Source-Based)

The Open Core `v0.1.0-preview` cut is a **source release**. Do not assume
nuget.org packages until a distribution channel is explicitly approved.

## 1. Clone The Public Repos

```bash
git clone https://github.com/jim-finlon/dotnetagents.git
git clone https://github.com/jim-finlon/dotnetagents-plugins.git
git clone https://github.com/jim-finlon/dotnetagents-examples.git
cd dotnetagents-examples
```

Optional: check out the preview tag for a pinned start:

```bash
git checkout v0.1.0-preview
```

## 2. Run A Smoke Example

```bash
dotnet run --project examples/hello-agent-cs -- --smoke
dotnet run --project examples/foundation -- --smoke
```

## 3. Read The Docs

- [Getting Started](../../docs/getting-started.md)
- [Running Examples](../../docs/running-examples.md)
- [API / Package Index](../../docs/api-index.md)

## 4. Package Install (Future)

<!-- PACKAGE-INSTALL-INSERTION-POINT
When NuGet or another feed is approved, replace this section with:
  dotnet add package <ApprovedPackageId> --version <R4Version>
Do not invent package ids here.
-->

Package-install instructions are intentionally reserved. Until then, use
ProjectReference or a local feed as documented by each example.
