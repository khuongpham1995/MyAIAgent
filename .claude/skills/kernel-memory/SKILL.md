---
name: kernel-memory
description: >
  Microsoft KernelMemory integration patterns for MyAIAgent. Covers document
  upload, readiness polling, semantic Q&A, the IKernelMemoryService abstraction,
  and KernelMemory client configuration.
  Load when: working on document tools, RAG features, KernelMemory service,
  or asked about document Q&A, semantic search, or knowledge base queries.
---

# KernelMemory Integration (MyAIAgent)

## Overview

MyAIAgent uses [Microsoft KernelMemory](https://github.com/microsoft/kernel-memory) as an external service for:
- **Document ingestion** — upload markdown/PDF files for indexing
- **Semantic search / Q&A** — ask natural language questions against indexed documents

The KernelMemory service runs separately at `http://localhost:9001` (configurable via `appsettings.json`).

## Domain Interface

```csharp
// Domain/Interfaces/IKernelMemoryService.cs
public interface IKernelMemoryService
{
    Task<string> AskAsync(string question, string documentId, CancellationToken ct);
    Task UploadDocumentAsync(string documentId, Stream content, CancellationToken ct);
    Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken ct);
}
```

## Infrastructure Implementation

`KernelMemoryService` wraps the `MemoryWebClient`:

```csharp
public sealed class KernelMemoryService(MemoryWebClient client, ILogger<KernelMemoryService> logger)
    : IKernelMemoryService, IScoped
{
    public async Task<string> AskAsync(string question, string documentId, CancellationToken ct)
    {
        var answer = await client.AskAsync(question, filter: MemoryFilters.ByDocument(documentId), ct: ct);
        return answer.Result;
    }

    public async Task UploadDocumentAsync(string documentId, Stream content, CancellationToken ct)
    {
        await client.ImportDocumentAsync(
            new Document(documentId).AddStream(documentId, content),
            cancellationToken: ct);
    }

    public async Task<bool> IsDocumentReadyAsync(string documentId, CancellationToken ct)
    {
        var status = await client.GetDocumentStatusAsync(documentId, cancellationToken: ct);
        return status?.Completed ?? false;
    }
}
```

## Document Upload + Poll Pattern

The `AskBackendQuestionHandler` shows the full flow: upload → poll → ask.

```csharp
public sealed class AskBackendQuestionHandler(IKernelMemoryService kernelMemory)
    : IRequestHandler<AskQuestionCommand, ResponseModel<string>>
{
    private const int MaxPollAttempts = 10;
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    public async Task<ResponseModel<string>> Handle(AskQuestionCommand command, CancellationToken ct)
    {
        // 1. Upload the document
        var documentStream = GetDocumentStream(command.DocumentName);
        await kernelMemory.UploadDocumentAsync(command.DocumentName, documentStream, ct);

        // 2. Poll until ready
        for (int i = 0; i < MaxPollAttempts; i++)
        {
            if (await kernelMemory.IsDocumentReadyAsync(command.DocumentName, ct))
                break;

            await Task.Delay(PollDelay, ct);
        }

        // 3. Ask the question
        var answer = await kernelMemory.AskAsync(command.Question, command.DocumentName, ct);
        return ResponseModel<string>.Success(answer);
    }
}
```

## Configuration

`appsettings.json`:

```json
{
  "KernelMemory": {
    "BaseUrl": "http://localhost:9001"
  }
}
```

IoC registration in `Infrastructure/Extensions/IoCExtension.cs`:

```csharp
services.AddSingleton(_ =>
    new MemoryWebClient(configuration["KernelMemory:BaseUrl"]!));
```

## Supported Documents (Current)

| Document ID | Source File | Purpose |
|---|---|---|
| `backend_structure` | `Documents/backend_structure.md` | Backend architecture Q&A |
| `backend_tests` | `Documents/backend_tests.md` | Unit testing guidelines Q&A |

To add a new document: add the markdown file to `Application/Documents/`, register a new tool constant in `McpServerConstant`, and add a handler using the upload → poll → ask pattern above.

## Anti-patterns

```csharp
// DON'T — call MemoryWebClient directly from a handler (bypasses the abstraction)
public sealed class MyHandler(MemoryWebClient client) { }

// DON'T — forget to poll before asking (document may not be indexed yet)
await kernelMemory.UploadDocumentAsync(docId, stream, ct);
var answer = await kernelMemory.AskAsync(question, docId, ct);  // race condition

// DON'T — ignore CancellationToken in poll loop
await Task.Delay(PollDelay);  // should be Task.Delay(PollDelay, ct)
```
