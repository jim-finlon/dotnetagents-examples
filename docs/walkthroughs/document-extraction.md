# Walkthrough: Document Extraction

`examples/document-extraction` shows a common agent workflow: take an input
document, extract structured information, validate it, and emit a measurable
result.

## What It Demonstrates

- document-oriented workflow shape
- structured extraction output
- validation before use
- result envelope for local comparison
- public-safe offline operation

## Run It

```bash
cd examples/document-extraction
dotnet run
```

Check the example README for exact arguments and sample input paths.

## Extend It

Start with a new field:

1. Add the field to the extraction result model.
2. Populate it from sample input.
3. Add validation for missing or malformed values.
4. Update the smoke output.
5. Add or update a test fixture.

## Production Shape

A production document workflow usually adds:

- artifact storage for inputs and outputs
- redaction before model calls
- source references for extracted fields
- confidence or review status
- human review for low-confidence extraction
- retention policy for uploaded files

Keep the public example small. Add real providers and storage only when you can
test their failure modes.
