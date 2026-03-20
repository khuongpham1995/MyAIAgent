# MyAIAgent

A modular AI agent backend built on **.NET 10** with Clean Architecture. The solution exposes three independently deployable services orchestrated by .NET Aspire.

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                   MyAIAgent.AppHost                 │
│              (.NET Aspire Orchestration)             │
└────────────┬──────────────────┬─────────────────────┘
             │                  │
             ▼                  ▼
┌────────────────────┐  ┌──────────────────────────┐
│    Orchestrator    │  │  KernelMemory (RAG)       │
│  POST /chat        │  │  Self-hosted daemon       │
│  Microsoft Agents  │  │  /upload, /ask, /search   │
│  Framework         │  │  Microsoft KernelMemory   │
└────────────┬───────┘  └──────────────────────────┘
             │
             ▼
┌────────────────────┐
│    MCP Server      │
│  .NET MCP Tools    │
│  GET /mcp/stream   │
│  POST /mcp         │
└────────────────────┘
```

### Services

| Service | Project | Purpose |
|---|---|---|
| **Orchestrator** | `MyAIAgent.Orchestrator.*` | Chat API — routes user messages to AI agent, dispatches MCP tools |
| **KernelMemory** | `MyAIAgent.KernelMemory.*` | RAG service — document ingestion, semantic search, Q&A |
| **MCP Server** | `MyAIAgent.McpServer.*` | MCP tool server — exposes tools to LLMs via Model Context Protocol |

Each service follows **Clean Architecture** across four layers:
`Domain` ← `Application` ← `Infrastructure` ← `Presentation`

## Tech Stack

| Area | Technology |
|---|---|
| Runtime | .NET 10 / C# 14 |
| Orchestration | .NET Aspire |
| AI Agent Framework | Microsoft Agents (`Microsoft.Agents.AI` v1.0.0-rc1) |
| AI Abstractions | `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` |
| RAG / Knowledge | Microsoft KernelMemory (self-hosted via `Microsoft.KernelMemory.Service.AspNetCore`) |
| MCP Protocol | ModelContextProtocol SDK v0.5.0-preview.1 |
| CQRS | MediatR v12.5.0 |
| Validation | FluentValidation v11.11.0 |
| HTTP Resilience | Polly via `Microsoft.Extensions.Http.Resilience` |
| DI Convention | Scrutor v7.0.0 |
| Testing | xUnit + Moq + FluentAssertions |

## Solution Structure

```
src/
  MyAIAgent.sln
  MyAIAgent.AppHost/                         # .NET Aspire — runs all three services
  Common/                                    # Shared base — Clean Architecture layers
    MyAIAgent.Common.Domain/                 # ResponseModel<T>, ITransient/IScoped/ISingleton
    MyAIAgent.Common.Application/            # ValidationBehavior, shared app extensions
    MyAIAgent.Common.Infrastructure/         # Scrutor DI scanning, Polly retry policy
  McpServer/
    MyAIAgent.McpServer.Domain/              # Entities, interfaces, MCP constants
    MyAIAgent.McpServer.Application/         # MediatR handlers, validators, pipeline
    MyAIAgent.McpServer.Infrastructure/      # HTTP clients, KernelMemory web client
    MyAIAgent.McpServer.Presentation/        # MCP server host, tool classes
    MyAIAgent.McpServer.Test/                # xUnit tests
  Orchestrator/
    MyAIAgent.Orchestrator.Domain/           # IChatOrchestratorService, chat models
    MyAIAgent.Orchestrator.Application/      # ChatHandler, validation pipeline
    MyAIAgent.Orchestrator.Infrastructure/   # ChatOrchestratorService (Agents Framework)
    MyAIAgent.Orchestrator.Presentation/     # ASP.NET Core host, POST /chat
  KernelMemory/
    MyAIAgent.KernelMemory.Domain/           # IDocumentService, document models
    MyAIAgent.KernelMemory.Application/      # Document handlers, validation pipeline
    MyAIAgent.KernelMemory.Infrastructure/   # KernelMemory builder configuration
    MyAIAgent.KernelMemory.Presentation/     # Self-hosted KernelMemory daemon
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- An AI provider credential — Azure OpenAI (recommended) or OpenAI API key
- _(Optional)_ SQL Server for long-term memory

### Run via Aspire (recommended)

```bash
dotnet restore
dotnet run --project src/MyAIAgent.AppHost
```

Aspire starts all three services and opens the dashboard with live endpoint URLs.

### Configure secrets

Set your AI provider credentials via user secrets (never commit these):

```bash
# For the Orchestrator
cd src/MyAIAgent.Orchestrator.Presentation
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-key>"
```

### Build and test

```bash
dotnet build src/MyAIAgent.sln
dotnet test src/MyAIAgent.McpServer.Test/MyAIAgent.McpServer.Test.csproj
dotnet format --verify-no-changes
```

## Service Details

### Orchestrator (`POST /chat`)

Accepts a chat message and returns the agent's reply:

```json
POST /chat
{ "userMessage": "What customers are registered?", "history": [] }

→ { "reply": "There are currently 3 customers: ..." }
```

The Orchestrator uses the Microsoft Agents Framework to dispatch the user message through the AI model. Tools registered on the MCP Server are available to the agent via `McpClient`.

### KernelMemory (RAG)

Self-hosted KernelMemory daemon exposing the standard KernelMemory HTTP API:

| Endpoint | Purpose |
|---|---|
| `POST /upload` | Ingest a document |
| `POST /ask` | Semantic Q&A against indexed documents |
| `GET /search` | Vector search |
| `GET /status` | Document indexing status |

Configuration (AI provider for embeddings, vector store) is driven by `appsettings.json`.

### MCP Server

Exposes tools to LLMs via the Model Context Protocol (HTTP transport):

| Endpoint | Purpose |
|---|---|
| `GET /mcp/stream` | SSE stream (tool listing, results) |
| `POST /mcp` | Tool invocation |

Tools are discovered automatically via `WithToolsFromAssembly()`.

## Dependency Direction

```
Common.Domain ← Common.Application ← Common.Infrastructure
     ↑                  ↑                     ↑
  [Service].Domain  [Service].Application  [Service].Infrastructure ← [Service].Presentation
```

`MyAIAgent.Common.*` is a shared Clean Architecture base. Each service layer references its Common counterpart — Domain→Common.Domain, Application→Common.Application, Infrastructure→Common.Infrastructure.

## Contributing

- Follow `.claude/rules/` coding standards
- Run `/plan` before any change touching 3+ files
- Run `/verify` (`dotnet build` + `dotnet test` + format check) before every PR
- Use conventional commit prefixes: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`
