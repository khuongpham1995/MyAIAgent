---
name: kernel-memory
description: >
  Microsoft KernelMemory integration patterns for MyAIAgent. Covers the
  self-hosted KernelMemory daemon, document upload, readiness polling,
  semantic Q&A, and KernelMemory configuration (AI provider, vector store).
  Load when: working on the KernelMemory service, RAG features, document
  ingestion, semantic search, or knowledge base queries.
---

# KernelMemory Service (MyAIAgent)

## Overview

`MyAIAgent.KernelMemory.*` is a **self-hosted KernelMemory daemon** — it IS the RAG service. Other services connect to it via HTTP using typed `HttpClient` instances pointed at the daemon's base URL. There is no custom domain service abstraction layer; the daemon's own HTTP API is the interface.

## Project Structure

```
MyAIAgent.KernelMemory.Domain/
  Entities/DocumentRecord.cs            ← tracking model; not written by the current upload path

MyAIAgent.KernelMemory.Application/
  IoCExtension.cs

MyAIAgent.KernelMemory.Infrastructure/
  Models/KernelMemoryServiceModel.cs    ← web-service options model
  Data/KernelMemoryDbContext.cs         ← EF Core (DocumentRecords container on Cosmos; shared 400 RU/s database throughput)
  IoCExtension.cs
  Extensions/KernelMemoryPipelineExtension.cs

MyAIAgent.KernelMemory.Presentation/
  Program.cs                            ← self-hosted daemon bootstrap
  Extensions/WebApplicationExtension.cs ← RunKernelMemoryAsync, LogStartupDiagnostics
  Filters/HttpAuthEndpointFilter.cs     ← validates API key header; 401/403
  Filters/HttpErrorsEndpointFilter.cs   ← catches 413, logs all unhandled
  appsettings.json
```

## Presentation Bootstrap

`Program.cs` wires in this order:

```
AddApplication()
AddInfrastructure(config, isDevelopment)
AddGlobalExceptionHandling()
AddKernelMemory(...)          ← KernelMemoryPipelineExtension
UseExceptionHandler()
Cosmos containers are preprovisioned by Bicep; startup never creates resources.
RunKernelMemoryAsync(...)
```

## Endpoint Filters

Both filters are instantiated manually in `WebApplicationExtension` (not via DI) because `AddKernelMemoryEndpoints` accepts filter instances directly. `ILoggerFactory` is resolved from `app.Services` to create the loggers.

- `HttpAuthEndpointFilter` — validates the `Authorization` header against `ServiceAuthorization.AccessKey1`/`AccessKey2`; config validated once at construction
- `HttpErrorsEndpointFilter` — catches `BadHttpRequestException` 413 → 413 response; all other exceptions → log + rethrow to `GlobalExceptionHandler`

## Service Connections

| Service | Connects to KernelMemory? | Purpose |
|---|---|---|
| **Gateway** | Yes — `IKernelMemoryClient` / `KernelMemoryClient` (120s) | Admin: document upload only |
| **Orchestrator** | No — retrieval integration is planned | RAG during chat (augment replies with indexed knowledge) |
| **McpServer** | **No — must never connect** | MCP Server exposes tools only; RAG is Orchestrator's concern |

When adding a KernelMemory HTTP client to a service, follow this pattern in its Infrastructure `IoCExtension`:

```csharp
services.AddHttpClient<IKernelMemoryClient, KernelMemoryClient>(client =>
{
    client.BaseAddress = new Uri(configuration["KernelMemory:BaseUrl"]
        ?? throw new InvalidOperationException("KernelMemory:BaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddHttpRetryPolicy();
```

Aspire supplies reference metadata, but the current Gateway client reads `KernelMemory:BaseUrl` directly. Set that URL to the service endpoint; project references alone do not configure the typed client.

## KernelMemory HTTP API (key endpoints)

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/upload` | Ingest a document (multipart form) |
| `GET` | `/upload-status?documentId=...&index=...` | Poll indexing readiness |
| `POST` | `/ask` | Semantic Q&A — returns an LLM-generated answer with citations |
| `POST` | `/search` | Semantic search — returns ranked source chunks (no LLM generation) |
| `DELETE` | `/documents?documentId=...&index=...` | Delete a document and its embeddings |

### `/ask` request/response shape

```jsonc
// POST /ask
{ "question": "...", "index": "myaiagent-test", "filters": [], "minRelevance": 0.0 }

// response
{
  "question": "...",
  "noResult": false,
  "text": "The answer text...",
  "relevantSources": [
    { "sourceName": "file.pdf", "partitions": [ { "text": "...", "relevance": 0.9 } ] }
  ]
}
```

### `/upload-status` response excerpt

```json
{ "documentId": "...", "completed": true }
```

## Configuration Shape (`appsettings.json`)

```json
{
  "KernelMemory": {
    "DefaultIndexName": "myaiagent-test",
    "TextGeneratorType": "AzureOpenAI",
    "ServiceAuthorization": {
      "Enabled": true,
      "AuthenticationType": "APIKey",
      "HttpHeaderName": "Authorization",
      "AccessKey1": "",
      "AccessKey2": ""
    },
    "DataIngestion": {
      "EmbeddingGeneratorTypes": ["AzureOpenAIEmbedding"],
      "MemoryDbTypes": ["AzureAISearch"]
    },
    "Retrieval": {
      "MemoryDbType": "AzureAISearch",
      "EmbeddingGeneratorType": "AzureOpenAIEmbedding",
      "SearchClient": { "MaxMatchesCount": 100, "AnswerTokens": 300, "EmptyAnswer": "INFO NOT FOUND" }
    },
    "Services": {
      "AzureOpenAIEmbedding": { "Auth": "ApiKey", "Endpoint": "", "Deployment": "text-embedding-ada-002", "APIKey": "" },
      "AzureOpenAIText": { "Auth": "ApiKey", "Endpoint": "", "Deployment": "gpt-4o-mini", "APIKey": "" },
      "AzureAISearch": { "Auth": "ApiKey", "Endpoint": "", "APIKey": "" }
    }
  }
}
```

Use `dotnet user-secrets` for provider `APIKey`, Blob `ConnectionString`, and `AccessKey1`/`AccessKey2`. Cosmos and AAC use endpoints with Azure CLI identity locally and an explicit assigned managed identity in deployment. Never commit credentials.
