# Document Extraction and Local Knowledge Ingestion Demo

This sample demonstrates document parsing, text chunking, local keyword-based retrieval, and Live LLM-based Retrieval-Augmented Generation (RAG) using standard environment variables and the open-core `DotNetAgents` libraries.

It is designed to showcase how C# applications can build lightweight knowledge search systems without requiring complex database deployments.

---

## Capabilities

1. **Deterministic Offline Smoke Verification (`--smoke`)**: Allows instant automated test validation and environment health checks with no external API dependency.
2. **Document Ingestion (`ingest`)**: Parses text files into structural sections using Markdown headers or paragraph breaks, normalizing formatting.
3. **Retrieval-Augmented Generation (`query`)**: Matches query terms against ingested text chunks, ranks matching sections, and optionally passes the retrieved context to a live LLM (OpenAI, Anthropic, or Ollama) to synthesize a final natural answer.

---

## Configuration & Setup

By default, the query command uses keyword-matching fallback mode. To enable Live LLM completions, configure one of the following environment variables:

```bash
# OpenAI
export OPENAI_API_KEY="your-openai-api-key"
export OPENAI_MODEL="gpt-4o-mini" # Optional, defaults to gpt-3.5-turbo

# Anthropic
export ANTHROPIC_API_KEY="your-anthropic-api-key"
export ANTHROPIC_MODEL="claude-3-5-sonnet-20240620" # Optional, defaults to claude-3-sonnet-20240229

# Local Ollama
export OLLAMA_MODEL="gemma2"
export OLLAMA_HOST="http://localhost:11434" # Optional, defaults to localhost
```

---

## Commands & Usage

### Run Deterministic Offline Smoke Check
Verify environment setups, assembly configurations, and output formats instantly:
```bash
dotnet run --project samples/document-extraction -- --smoke
```

### Ingest a Document
Parse and inspect the extracted chunks from a text document (defaults to the included `sample-doc.txt`):
```bash
dotnet run --project samples/document-extraction -- ingest samples/document-extraction/sample-doc.txt
```

### Query a Document (Local RAG)
Perform keyword matching and search retrieval. If LLM keys are configured, it runs live context completion:
```bash
dotnet run --project samples/document-extraction -- query "What is the escalation path for P0 issues?"
```

To run a query against a custom text file:
```bash
dotnet run --project samples/document-extraction -- query path/to/my-document.txt "What are the core metrics?"
```

---

## Premium Platform Upgrade Path

This sample utilizes the public open-core libraries for basic parsing and matching. When migrating to the enterprise production environment on Tyr, the following premium services become available:

- **AI-Driven Layout Analysis**: High-fidelity OCR and layout parser service for PDFs, Word files, and scanned images.
- **Semantic Vector Embeddings**: Sub-second search indexing utilizing vector databases (e.g. pgvector, Qdrant) with hybrid keyword-vector retrieval.
- **Automatic Intake Pipelines**: Autonomous document ingestion agents triggered by file system events, email attachments, or S3/Blob storage uploads.
- **Secure Hosted LLMs**: Access to centralized inference endpoints with built-in PII redaction and secret scrubbing gates.
