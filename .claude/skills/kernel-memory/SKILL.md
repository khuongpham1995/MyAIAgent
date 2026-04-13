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
  Entities/DocumentRecord.cs            ← document upload tracking entity
  Models/KernelMemoryServiceModel.cs    ← startup config options

MyAIAgent.KernelMemory.Application/
  Extensions/IoCExtension.cs

MyAIAgent.KernelMemory.Infrastructure/
  Data/KernelMemoryDbContext.cs         ← EF Core (DocumentRecords table; SQLite dev / Cosmos prod)
  Extensions/IoCExtension.cs
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
EnsureDbCreatedAsync<KernelMemoryDbContext>()   ← dev only (SQLite)
RunKernelMemoryAsync(...)
```

## Endpoint Filters

Both filters are instantiated manually in `WebApplicationExtension` (not via DI) because `AddKernelMemoryEndpoints` accepts filter instances directly. `ILoggerFactory` is resolved from `app.Services` to create the loggers.

- `HttpAuthEndpointFilter` — validates the `Authorization` header against `ServiceAuthorization.AccessKey1`/`AccessKey2`; config validated once at construction
- `HttpErrorsEndpointFilter` — catches `BadHttpRequestException` 413 → 413 response; all other exceptions → log + rethrow to `GlobalExceptionHandler`

## How Other Services Connect

Services that need to call the KernelMemory daemon register a typed `HttpClient` in their own Infrastructure `IoCExtension`:

```csharp
services.AddHttpClient<IKernelMemoryClient, KernelMemoryClient>(client =>
{
    client.BaseAddress = new Uri(configuration[MainSubSysConstant.Config.KernelMemoryBaseUrl]
        ?? throw new InvalidOperationException("KernelMemory:BaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddHttpRetryPolicy();
```

In Aspire, the base URL is injected automatically via service discovery using the `kernel-memory` resource name.

## KernelMemory HTTP API (key endpoints)

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/upload` | Ingest a document (multipart form) |
| `GET` | `/documents/{documentId}/status` | Poll indexing readiness |
| `POST` | `/ask` | Semantic Q&A — returns an LLM-generated answer with citations |
| `POST` | `/search` | Semantic search — returns ranked source chunks (no LLM generation) |
| `DELETE` | `/documents/{documentId}` | Delete a document and its embeddings |

### `/ask` request/response shape

```json
// POST /ask
{ "question": "...", "index": "default", "filters": [], "minRelevance": 0.0, "limit": 1 }

// response
{
  "question": "...",
  "noResult": false,
  "result": "The answer text...",
  "relevantSources": [
    { "sourceName": "file.pdf", "partitions": [ { "text": "...", "relevance": 0.9 } ] }
  ]
}
```

### `/documents/{id}/status` response shape

```json
{ "documentId": "...", "completed": true, "failed": false, "steps": [...], "remainingSteps": [] }
```

## Configuration Shape (`appsettings.json`)

```json
{
  "KernelMemory": {
    "DefaultIndexName": "default",
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
      "MemoryDbTypes": ["AzureCosmosDbNoSQL"]
    },
    "Retrieval": {
      "MemoryDbType": "AzureCosmosDbNoSQL",
      "EmbeddingGeneratorType": "AzureOpenAIEmbedding",
      "SearchClient": { "MaxMatchesCount": 100, "AnswerTokens": 300, "EmptyAnswer": "INFO NOT FOUND" }
    },
    "Services": {
      "AzureOpenAIEmbedding": { "Auth": "ApiKey", "Endpoint": "", "Deployment": "text-embedding-ada-002", "APIKey": "" },
      "AzureOpenAIText": { "Auth": "ApiKey", "Endpoint": "", "Deployment": "gpt-4o-mini", "APIKey": "" },
      "AzureCosmosDbNoSQL": { "Auth": "ConnectionString", "ConnectionString": "" }
    }
  }
}
```

Use `dotnet user-secrets` for `APIKey`, `ConnectionString`, and `AccessKey1`/`AccessKey2`. Never commit them.
