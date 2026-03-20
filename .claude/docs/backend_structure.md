# Backend Project Structure

MyAIAgent is a modular AI agent backend built on .NET 10 with Clean Architecture. The solution exposes three independently deployable services orchestrated by .NET Aspire.

## Services

### Orchestrator (`MyAIAgent.Orchestrator.*`)

The chat API. Receives user messages, routes them through the Microsoft Agents Framework (`Microsoft.Agents.AI`), and dispatches MCP tools as needed.

- Entry point: `POST /chat`
- AI abstraction: `IChatClient` from `Microsoft.Extensions.AI`
- Tool dispatch: connects to MCP Server via `McpClient`

### KernelMemory (`MyAIAgent.KernelMemory.*`)

The RAG service. A self-hosted KernelMemory daemon that provides document ingestion, semantic search, and Q&A.

- Entry point: standard KernelMemory HTTP API (`POST /upload`, `POST /ask`, `GET /search`)
- Package: `Microsoft.KernelMemory.Service.AspNetCore`
- Clients connect via `MemoryWebClient`

### MCP Server (`MyAIAgent.McpServer.*`)

The tool server. Exposes tools to LLMs via the Model Context Protocol (HTTP transport).

- Entry point: `GET /mcp/stream` (SSE), `POST /mcp`
- Tools are discovered via `WithToolsFromAssembly()`
- Each tool dispatches via MediatR (`ISender`)

## Solution Layout

```
src/
  MyAIAgent.AppHost/                         # .NET Aspire — starts all three services
  MyAIAgent.Common/                          # Shared: ResponseModel<T>, DI marker interfaces
  McpServer/
    MyAIAgent.McpServer.Domain/
    MyAIAgent.McpServer.Application/
    MyAIAgent.McpServer.Infrastructure/
    MyAIAgent.McpServer.Presentation/
    MyAIAgent.McpServer.Test/
  Orchestrator/
    MyAIAgent.Orchestrator.Domain/
    MyAIAgent.Orchestrator.Application/
    MyAIAgent.Orchestrator.Infrastructure/
    MyAIAgent.Orchestrator.Presentation/
  KernelMemory/
    MyAIAgent.KernelMemory.Domain/
    MyAIAgent.KernelMemory.Application/
    MyAIAgent.KernelMemory.Infrastructure/
    MyAIAgent.KernelMemory.Presentation/
```

## Clean Architecture Per Service

Each service follows the same four-layer structure:

```
Domain          ← entities, interfaces, constants — no dependencies
Application     ← MediatR handlers, validators, pipeline behaviors
Infrastructure  ← implements Domain interfaces, registers SDK clients
Presentation    ← HTTP host, endpoints, MCP tools
```

Dependency direction: `Domain` ← `Application` ← `Infrastructure` ← `Presentation`

`MyAIAgent.Common` is the only cross-service shared project. Services never reference each other's projects — they communicate via HTTP only.

## Shared Types (`MyAIAgent.Common`)

| Type | Namespace | Purpose |
|---|---|---|
| `ResponseModel<T>` | `MyAIAgent.Common.Models` | Standard result wrapper for all handlers |
| `ITransient` | `MyAIAgent.Common.ServiceLifetime` | DI: transient lifetime |
| `IScoped` | `MyAIAgent.Common.ServiceLifetime` | DI: scoped lifetime |
| `ISingleton` | `MyAIAgent.Common.ServiceLifetime` | DI: singleton lifetime |

## Dependency Injection Convention

Types implement `ITransient`, `IScoped`, or `ISingleton` from `MyAIAgent.Common`. Scrutor auto-registers them during startup — no manual `AddScoped<T>()` calls needed.

```csharp
// Scrutor picks this up automatically
public sealed class DocumentService(IKernelMemory kernelMemory)
    : IDocumentService, IScoped { }
```

## Key Packages

| Package | Service | Purpose |
|---|---|---|
| `Microsoft.Agents.AI` v1.0.0-rc1 | Orchestrator | Microsoft Agents Framework |
| `Microsoft.Extensions.AI.OpenAI` | Orchestrator | IChatClient (OpenAI/Azure OpenAI) |
| `Microsoft.KernelMemory.Service.AspNetCore` | KernelMemory | Self-hosted RAG daemon |
| `Microsoft.KernelMemory.WebClient` | McpServer | HTTP client for KernelMemory |
| `ModelContextProtocol.AspNetCore` | McpServer | MCP server transport |
| `MediatR` v12.5.0 | All services | CQRS handler dispatch |
| `FluentValidation` v11.11.0 | All services | Request validation |

## Aspire Service Discovery

AppHost wires the three services together:

```csharp
var kernelMemory = builder.AddProject<Projects.MyAIAgent_KernelMemory_Presentation>("kernel-memory")
    .WithExternalHttpEndpoints();

var mcpServer = builder.AddProject<Projects.MyAIAgent_McpServer_Presentation>("mcp-server")
    .WithExternalHttpEndpoints()
    .WithReference(kernelMemory);

var orchestrator = builder.AddProject<Projects.MyAIAgent_Orchestrator_Presentation>("orchestrator")
    .WithExternalHttpEndpoints()
    .WithReference(mcpServer)
    .WithReference(kernelMemory);
```

Aspire injects service URLs as environment variables (e.g. `services__kernel-memory__http__0`), replacing hard-coded `appsettings.json` base URLs at runtime.
