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

The `MyAIAgent.KernelMemory.*` module is a **self-hosted KernelMemory daemon** — it IS the RAG service. Other services (McpServer, Orchestrator) connect to it via HTTP using `MemoryWebClient`.

The KernelMemory service also follows Clean Architecture across four layers.

## Project Structure

```
MyAIAgent.KernelMemory.Domain/
  Interfaces/IDocumentService.cs        ← service contract
  Models/DocumentUploadResult.cs        ← upload result record
  Models/DocumentQueryResult.cs         ← Q&A result record

MyAIAgent.KernelMemory.Application/
  Handlers/UploadDocumentHandler.cs     ← UploadDocumentCommand + handler + validator
  Handlers/AskDocumentQuestionHandler.cs← AskDocumentQuestionQuery + handler + validator
  Behaviors/ValidationBehavior.cs       ← MediatR pipeline behavior
  Extensions/IoCExtension.cs

MyAIAgent.KernelMemory.Infrastructure/
  Services/DocumentService.cs           ← implements IDocumentService via IKernelMemory
  Extensions/IoCExtension.cs            ← registers IKernelMemory, Scrutor scan

MyAIAgent.KernelMemory.Presentation/
  Program.cs                            ← self-hosted KernelMemory daemon bootstrap
  appsettings.json                      ← KernelMemory config (AI provider, storage)
```

## Domain Interface

```csharp
// KernelMemory.Domain/Interfaces/IDocumentService.cs
public interface IDocumentService
{
    Task<DocumentUploadResult> UploadDocumentAsync(
        string filePath, string documentId, CancellationToken ct);

    Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken ct);

    Task<DocumentQueryResult> AskAsync(string question, CancellationToken ct);
}
```

## Infrastructure Implementation

`DocumentService` wraps the `IKernelMemory` engine:

```csharp
public sealed class DocumentService(IKernelMemory kernelMemory)
    : IDocumentService, IScoped
{
    public async Task<DocumentUploadResult> UploadDocumentAsync(
        string filePath, string documentId, CancellationToken ct)
    {
        await kernelMemory.ImportDocumentAsync(filePath, documentId: documentId, cancellationToken: ct);
        return new DocumentUploadResult(documentId, IsSuccess: true);
    }

    public async Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken ct)
    {
        var status = await kernelMemory.GetDocumentStatusAsync(documentId, cancellationToken: ct);
        return status?.Completed ?? false;
    }

    public async Task<DocumentQueryResult> AskAsync(string question, CancellationToken ct)
    {
        var answer = await kernelMemory.AskAsync(question, cancellationToken: ct);
        return new DocumentQueryResult(answer.Result);
    }
}
```

## Self-Hosted Daemon (Presentation)

`Program.cs` bootstraps the full KernelMemory service:

```csharp
// KernelMemory.Presentation/Program.cs
var app = KernelMemoryWebApplicationBuilder.Build(args);
await app.RunAsync();
```

All configuration (AI provider, vector store, storage backend) is driven by `appsettings.json`.

### Standard KernelMemory HTTP Endpoints (auto-exposed)

| Endpoint | Purpose |
|---|---|
| `POST /upload` | Ingest a document |
| `POST /ask` | Semantic Q&A |
| `GET /search` | Vector search |
| `GET /documents/{id}/status` | Indexing status |

## Configuration (`appsettings.json`)

```json
{
  "KernelMemory": {
    "Services": {
      "AzureOpenAIEmbedding": {
        "Auth": "ApiKey",
        "Endpoint": "",
        "Deployment": "text-embedding-ada-002"
      },
      "AzureOpenAIText": {
        "Auth": "ApiKey",
        "Endpoint": "",
        "Deployment": "gpt-4o"
      },
      "SimpleVectorDb": {
        "StorageType": "Volatile"
      }
    },
    "DataIngestion": {
      "EmbeddingGeneratorTypes": ["AzureOpenAIEmbedding"],
      "MemoryDbTypes": ["SimpleVectorDb"]
    },
    "Retrieval": {
      "MemoryDbType": "SimpleVectorDb",
      "EmbeddingGeneratorType": "AzureOpenAIEmbedding"
    }
  }
}
```

Use `dotnet user-secrets` for API keys — never commit them.

## Upload + Poll Pattern (Application Handlers)

The `UploadDocumentHandler` shows the full upload → poll → ready flow:

```csharp
public sealed class UploadDocumentHandler(IDocumentService documentService)
    : IRequestHandler<UploadDocumentCommand, ResponseModel<DocumentUploadResult>>
{
    private const int MaxPollAttempts = 10;
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    public async Task<ResponseModel<DocumentUploadResult>> Handle(
        UploadDocumentCommand command, CancellationToken ct)
    {
        var result = await documentService.UploadDocumentAsync(
            command.FilePath, command.DocumentId, ct);

        for (var i = 0; i < MaxPollAttempts; i++)
        {
            if (await documentService.IsDocumentReadyAsync(command.DocumentId, ct))
                return ResponseModel<DocumentUploadResult>.Succeed(result);

            await Task.Delay(PollDelay, ct);
        }

        return ResponseModel<DocumentUploadResult>.Fail(
            message: $"Document '{command.DocumentId}' did not finish indexing within the timeout.");
    }
}
```

## How McpServer Connects to KernelMemory

The MCP Server's `Infrastructure` layer uses `MemoryWebClient` (a lightweight HTTP client) to call the KernelMemory daemon:

```csharp
// McpServer.Infrastructure/Extensions/IoCExtension.cs
var kernelMemoryApiUrl = configuration["KernelMemory:BaseUrl"]
    ?? throw new InvalidOperationException("KernelMemory:BaseUrl is required");
services.AddSingleton<IKernelMemory>(_ => new MemoryWebClient(kernelMemoryApiUrl));
```

In Aspire, the KernelMemory URL is automatically injected via service discovery — the McpServer reads it from the Aspire-provided environment variable.

## Anti-patterns

```csharp
// DON'T — call MemoryWebClient directly from a handler
public sealed class MyHandler(MemoryWebClient client) { }  // bypass the interface

// DON'T — forget to poll before asking (document not yet indexed)
await documentService.UploadDocumentAsync(path, id, ct);
var answer = await documentService.AskAsync(question, ct);  // race condition

// DON'T — drop CancellationToken in the poll loop
await Task.Delay(PollDelay);  // should pass ct

// DON'T — cross-reference the KernelMemory Domain from another service
// Services communicate via HTTP only
```
