# Backend Project Structure

MyAIAgent is a modular AI agent backend built on .NET 10 with Clean Architecture. The solution exposes four independently deployable services orchestrated by .NET Aspire.

## Services

### Orchestrator (`MyAIAgent.Orchestrator.*`)

The chat API. Receives user messages, manages session history, routes through the Microsoft Agents Framework, calls KernelMemory for RAG context, and dispatches MCP tools as needed.

- Entry point: `POST /chat`, `GET|POST|DELETE /agents`
- AI abstraction: `IChatClient` from `Microsoft.Extensions.AI`
- Tool dispatch: connects to MCP Server via `McpClient`
- RAG: connects to KernelMemory via typed `HttpClient` (augments chat context)

### KernelMemory (`MyAIAgent.KernelMemory.*`)

The RAG daemon. A self-hosted KernelMemory service that handles document ingestion, embedding, vector storage, and semantic Q&A.

- Entry point: standard KernelMemory HTTP API (`POST /upload`, `POST /ask`, `GET /documents/{id}/status`)
- Package: `Microsoft.KernelMemory.Service.AspNetCore`
- Vector store: Azure Cosmos DB NoSQL (production), SQLite (development)
- Consumers connect via typed `HttpClient` — **not** `MemoryWebClient`

### MCP Server (`MyAIAgent.McpServer.*`)

The tool server. Exposes tools to LLMs via the Model Context Protocol (HTTP transport). Does **not** connect to KernelMemory — RAG is the Orchestrator's concern.

- Entry point: `GET /mcp/stream` (SSE), `POST /mcp`
- Tools are discovered via `WithToolsFromAssembly()`
- Each tool dispatches via MediatR (`ISender`)
- Current tools: `web_search`, `get_current_datetime`

### MainSubSys (`MyAIAgent.MainSubSys.*`)

The API gateway. Provides a unified REST surface for managing documents, agents, and tools. Routes to Orchestrator, KernelMemory, and MCP Server via typed HTTP clients.

- Entry point: `POST /chat`, `POST /admin/documents`, `GET /admin/documents/{id}/status`, `GET|POST|DELETE /admin/agents`, `GET /admin/tools`
- Document management: upload + status polling only — Q&A is the Orchestrator's concern

## Service Connection Topology

```
User → MainSubSys → Orchestrator → MCP Server
                 ↘              ↘
                  KernelMemory   KernelMemory
```

| From | To | Purpose |
|---|---|---|
| MainSubSys | Orchestrator | Forward chat, manage agents |
| MainSubSys | KernelMemory | Document upload + status check |
| MainSubSys | McpServer | List available tools |
| Orchestrator | McpServer | Tool dispatch during chat |
| Orchestrator | KernelMemory | Retrieve RAG context before generating reply |
| McpServer | KernelMemory | **Never** — not connected |

## Solution Layout

```
src/
  MyAIAgent.All/                    # .NET Aspire orchestration
  MyAIAgent.Common.Domain/          # ResponseModel<T>, DI markers, ServiceAuthorizationModel
  MyAIAgent.Common.Application/     # ValidationBehavior<TRequest,TResponse>
  MyAIAgent.Common.Infrastructure/  # Scrutor, Polly, GlobalExceptionHandler, HttpAuthEndpointFilter,
                                    # CosmosDbExtension, SqliteDbExtension
  MyAIAgent.McpServer.*/
  MyAIAgent.Orchestrator.*/
  MyAIAgent.KernelMemory.*/
  MyAIAgent.MainSubSys.*/
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

| Type | Layer | Purpose |
|---|---|---|
| `ResponseModel<T>` | Common.Domain | Standard result wrapper for all handlers |
| `ServiceAuthorizationModel` | Common.Domain | API key auth config (shared across services) |
| `ITransient` / `IScoped` / `ISingleton` | Common.Domain | DI lifetime marker interfaces |
| `ValidationBehavior<TRequest,TResponse>` | Common.Application | MediatR pipeline validation — always throws `ValidationException` |
| `HttpAuthEndpointFilter` | Common.Infrastructure | Shared API key endpoint filter |
| `SqliteDbExtension` | Common.Infrastructure | SQLite dev DB registration + `EnsureDbCreatedAsync` |

## Key Packages

| Package | Service | Purpose |
|---|---|---|
| `Microsoft.Agents.AI` v1.0.0-rc1 | Orchestrator | Microsoft Agents Framework |
| `Microsoft.Extensions.AI.OpenAI` | Orchestrator | `IChatClient` (OpenAI / Azure OpenAI) |
| `Microsoft.KernelMemory.Service.AspNetCore` | KernelMemory | Self-hosted RAG daemon |
| `ModelContextProtocol.AspNetCore` | McpServer | MCP server transport |
| `MediatR` v12.5.0 | All services | CQRS handler dispatch |
| `FluentValidation` v11.11.0 | All services | Request validation |
| `Microsoft.EntityFrameworkCore.Sqlite` | Common.Infrastructure | SQLite dev provider |

## Aspire Service Discovery

```csharp
var mcpServer    = builder.AddProject<...>("mcp-server").WithExternalHttpEndpoints();
var kernelMemory = builder.AddProject<...>("kernel-memory").WithExternalHttpEndpoints();

var orchestrator = builder.AddProject<...>("orchestrator")
    .WithExternalHttpEndpoints()
    .WithReference(mcpServer)
    .WithReference(kernelMemory);

builder.AddProject<...>("main-subsys")
    .WithExternalHttpEndpoints()
    .WithReference(orchestrator)
    .WithReference(kernelMemory)
    .WithReference(mcpServer);
```

Aspire injects service URLs as environment variables (e.g. `services__kernel-memory__http__0`), replacing hard-coded `appsettings.json` base URLs at runtime.
