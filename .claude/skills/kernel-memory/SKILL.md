---
name: kernel-memory
description: >
  Microsoft KernelMemory integration patterns for MyAIAgent. Covers the
  self-hosted KernelMemory daemon, document upload, readiness polling,
  semantic Q&A, the IDocumentService abstraction, and KernelMemory
  configuration (AI provider, vector store).
  Load when: working on the KernelMemory service, RAG features, document
  ingestion, semantic search, or knowledge base queries.
---

# KernelMemory Service (MyAIAgent)

## Overview

`MyAIAgent.KernelMemory.*` is a **self-hosted KernelMemory daemon** — it IS the RAG service. Other services connect to it via HTTP using `MemoryWebClient`.

## Project Structure

```
MyAIAgent.KernelMemory.Domain/
  Entities/DocumentRecord.cs            ← document upload tracking entity
  Options/KernelMemoryServiceOptions.cs
  Options/ServiceAuthorizationOptions.cs

MyAIAgent.KernelMemory.Application/
  Extensions/IoCExtension.cs

MyAIAgent.KernelMemory.Infrastructure/
  Data/KernelMemoryDbContext.cs         ← EF Core Cosmos (DocumentRecords container)
  Extensions/IoCExtension.cs
  Extensions/KernelMemoryPipelineExtension.cs

MyAIAgent.KernelMemory.Presentation/
  Program.cs                            ← self-hosted daemon bootstrap
  Extensions/WebApplicationExtension.cs ← RunKernelMemoryAsync, MapEndpoints
  Filters/HttpAuthEndpointFilter.cs     ← validates API key header; 401/403
  Filters/HttpErrorsEndpointFilter.cs   ← catches 413, logs all unhandled
  appsettings.json
```

## Domain Abstraction

`IDocumentService` (in `KernelMemory.Domain/Interfaces/`) defines three operations:
- `UploadDocumentAsync(filePath, documentId, ct)` — ingests a document
- `IsDocumentReadyAsync(documentId, ct)` — polls indexing status
- `AskAsync(question, ct)` — semantic Q&A

`DocumentService` in Infrastructure wraps `IKernelMemory` (the SDK) and implements `IScoped`.

## Upload + Poll Pattern

Upload is async — document indexing happens in the background. Always poll `IsDocumentReadyAsync` after upload before querying. See `Application/Handlers/` for the existing upload handler implementation.

## Endpoint Filters

Both filters are instantiated manually in `WebApplicationExtension.MapEndpoints` (not via DI) because `AddKernelMemoryEndpoints` accepts filter instances directly. `ILoggerFactory` is resolved from `app.Services` to create the loggers.

- `HttpAuthEndpointFilter` — validates the `Authorization` header against `ServiceAuthorization.AccessKey1`/`AccessKey2`; config validated once at construction
- `HttpErrorsEndpointFilter` — catches `BadHttpRequestException` 413 → 413 response; all other exceptions → log + rethrow to `GlobalExceptionHandler`

## Presentation Bootstrap

`Program.cs` wires: `AddApplication` → `AddInfrastructure` → `AddGlobalExceptionHandling` → `AddKernelMemory(...)` → `UseExceptionHandler` → `EnsureCosmosDbCreatedAsync` → `RunKernelMemoryAsync`.

See `Presentation/Program.cs` for the current implementation.

## How McpServer Connects

`McpServer.Infrastructure/Extensions/IoCExtension.cs` registers `IKernelMemory` as a `MemoryWebClient` pointed at the KernelMemory base URL from configuration. In Aspire, the URL is injected via service discovery.

## Configuration Shape (`appsettings.json`)

```json
{
  "KernelMemory": {
    "Services": {
      "AzureOpenAIEmbedding": { "Auth": "ApiKey", "Endpoint": "", "Deployment": "text-embedding-ada-002" },
      "AzureOpenAIText": { "Auth": "ApiKey", "Endpoint": "", "Deployment": "gpt-4o" },
      "SimpleVectorDb": { "StorageType": "Volatile" }
    },
    "DataIngestion": {
      "EmbeddingGeneratorTypes": ["AzureOpenAIEmbedding"],
      "MemoryDbTypes": ["SimpleVectorDb"]
    },
    "Retrieval": {
      "MemoryDbType": "SimpleVectorDb",
      "EmbeddingGeneratorType": "AzureOpenAIEmbedding"
    },
    "ServiceAuthorization": {
      "Enabled": true,
      "HttpHeaderName": "Authorization"
    }
  }
}
```

Use `dotnet user-secrets` for API keys and `AccessKey1`/`AccessKey2`. Never commit them.
