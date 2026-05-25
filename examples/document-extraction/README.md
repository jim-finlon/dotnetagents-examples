# Document Extraction Demo

This public demo shows the shape of document extraction plus local knowledge
ingestion without private services. The smoke path uses deterministic sample
chunks and emits a public result envelope.

## Run

```bash
dotnet run --project samples/document-extraction -- --smoke
```

Expected result: JSON with `"status": "passed"` and a
`dotnetagents.public-example.result.v1` envelope for `document-extraction`.

This demo is public-offering code. It does not use private credential custody,
hosted document queues, proprietary layout extraction, or private evaluation
systems.
